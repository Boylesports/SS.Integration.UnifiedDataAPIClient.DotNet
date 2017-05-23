﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using log4net;
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    /// <summary>
    /// 
    ///     The StreamController is responsible for managing the connection
    ///     to the RabbitMQ streaming server.
    /// 
    ///     There is only ONE streaming connection, independently of how
    ///     many resources/consumers are added.
    /// 
    ///     Each consumer has its own queue, but the connection is shared
    ///     among all the consumers. If the connection goes down, all
    ///     the consumers get disconnected. 
    /// 
    ///     There is no automatic re-connection. A connection is (re)-established
    ///     when the first consumer is added.
    /// 
    ///     Once a connection is established the StreamSubscriber object
    ///     is set to read from the connection for any up coming messages.
    /// 
    ///     The StreamSubscriber then passed this object to the IDispatch
    ///     object whose task it to dispatch the messages to the correct
    ///     consumer.
    /// 
    /// </summary>
    internal class StreamControllerActor : ReceiveActor, IWithUnboundedStash
    {
        public const string ActorName = "StreamControllerActor";

        internal enum ConnectionState
        {
            DISCONNECTED = 0,
            CONNECTING = 1,
            CONNECTED = 2
        }

        private static readonly ILog _logger = LogManager.GetLogger(typeof(StreamControllerActor));
        
        private IConnection _streamConnection;
        private volatile ConnectionState _state;
        private ICancelable _connectionCancellation = new Cancelable(Context.System.Scheduler);
        

        public StreamControllerActor(IActorRef dispatcherActor)
        {
            if (dispatcherActor == null)
                throw new ArgumentNullException("dispatcher");

            Dispatcher = dispatcherActor;

            //Start in Disconnected state
            DisconnectedState();
            
            AutoReconnect = UDAPI.Configuration.AutoReconnect;
            
            _logger.DebugFormat("StreamController initialised");
        }

        private void DisconnectedState()
        {
            Receive<ConnectStreamMessage>(x => ConnectStream(x));
            Receive<ValidateMessage>(x => ValidateConnection());

            Receive<ConnectedMessage>(x => Become(ConnectedState));

            Receive<NewConsumerMessage>(x =>
            {
                Stash.Stash();
                Connect(x.Consumer);

            });
            Receive<RemoveConsumerMessage>(x => RemoveConsumer(x.Consumer));
            Receive<DisposeMessage>(x => Dispose());

            State = ConnectionState.DISCONNECTED;
        }



        private void ConnectedState()
        {
            Receive<NewConsumerMessage>(x => ProcessNewConsumer(x));
            Receive<DisconnectedMessage>(x => Become(DisconnectedState));
            Receive<RemoveConsumerMessage>(x => RemoveConsumer(x.Consumer));
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateMessage>(x => ValidateConnection());

            Stash.UnstashAll();
        }

        private void ProcessNewConsumer(NewConsumerMessage newConsumerMessage)
        {
            AddConsumerToQueue(newConsumerMessage.Consumer);
        }
        
        /// <summary>
        /// Is AutoReconnect enabled
        /// </summary>
        public bool AutoReconnect { get; private set; }

        

        /// <summary>
        /// 
        ///     Returns the IDispatcher object that is responsible
        ///     of dispatching messages to the consumers.
        /// 
        /// </summary>
        internal IActorRef Dispatcher { get; private set; }

        public IStash Stash { get; set; }

        internal Exception ConnectionError;

        internal ConnectionState State
        {
            get
            {
                // as it is volatile, reading this property is thread-safe
                return _state;
            }
            private set
            {
                _state = value;
            }
        }
        
        #region Connection management

        protected virtual void CloseConnection()
        {
            try
            {
                if (_streamConnection != null)
                {
                    _streamConnection.ConnectionShutdown -= OnConnectionShutdown;
                    if (_streamConnection.IsOpen)
                        _streamConnection.Close();
                    _streamConnection = null;
                }
            }
            // }
            catch
            {
            }
            finally
            {
                _streamConnection = null;
            }

            OnConnectionStatusChanged(ConnectionState.DISCONNECTED);
            

        }

        protected virtual void EstablishConnection(ConnectionFactory factory)
        {
            // this method doesn't quit until
            // 1) a connection is established
            // 2) Dispose() is called
            //
            // therefore we will NOT quit from this method
            // when the StreamController has State = CONNECTING
            //
            // it must be called in mutual exclusion:
            // _connectionLock must be acquire before calling
            // this method.

            var newstate = ConnectionState.DISCONNECTED;

            try
            {
                _logger.DebugFormat("Connecting to the streaming server");

                long attempt = 1;
                bool result = false;
                while (!_connectionCancellation.IsCancellationRequested && !result)
                {
                    _logger.DebugFormat("Establishing connection to the streaming server, attempt={0}", attempt);

                    try
                    {
                        _streamConnection = factory.CreateConnection();
                        _streamConnection.ConnectionShutdown += OnConnectionShutdown;

                        _logger.Info("Connection to the streaming server correctly established");

                        newstate = ConnectionState.CONNECTED;
                        result = true;

                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error connecting to the streaming server", ex);
                        Thread.Sleep(100);
                    }

                    attempt++;
                }
            }
            finally
            {
                // notify any sleeping threads
                OnConnectionStatusChanged(newstate);
            }
        }

        private void ConnectStream(ConnectStreamMessage connectStreamMessage)
        {
            Connect(connectStreamMessage.Consumer);
        }

        private void Connect(IConsumer consumer)
        {
            if (State == ConnectionState.CONNECTED) return;
            
            if (State == ConnectionState.CONNECTED || _connectionCancellation.IsCancellationRequested)
                return;

            State = ConnectionState.CONNECTING;
            
            // GetQueueDetails() returns the credentials for connecting to the AMQP server
            // but it also asks the server to create an AMQP queue for the caller.
            // As the time to establish a connection could take a while (just think about
            // a not reliable network), the created AMQP queue could expire before we 
            // call BasicConsume() on it. 
            //
            // To prevent this situation, we call GetQueueDetails() twice for the consumer
            // who establish the connection, one here, and the second time on  
            // AddConsumeToQueue()

            QueueDetails queue = null;
            try
            {
                queue = consumer.GetQueueDetails();
                if (queue == null || string.IsNullOrEmpty(queue.Name))
                {
                    var e = new Exception("queue's name is not valid for consumerId=" + consumer.Id);
                    ConnectionError = e;
                    throw e;
                }
            }
            catch (Exception e)
            {
                _logger.Error("Error acquiring queue details for consumerId=" + consumer.Id, e);
                OnConnectionStatusChanged(ConnectionState.DISCONNECTED);
                ConnectionError = e;
                throw;
            }

            var factory = new ConnectionFactory
            {
                RequestedHeartbeat = UDAPI.Configuration.AMQPMissedHeartbeat,
                HostName = queue.Host,
                AutomaticRecoveryEnabled = UDAPI.Configuration.AutoReconnect,
                Port = queue.Port,
                UserName = queue.UserName,
                Password = queue.Password,
                VirtualHost = "/" + queue.VirtualHost // this is not used anymore, we keep it for retro-compatibility
            };

            EstablishConnection(factory);
        }
        
        protected virtual void OnConnectionShutdown(object sender, ShutdownEventArgs sea)
        {
            _logger.ErrorFormat("The AMQP connection was shutdown: {0}. AutoReconnect is enabled={1}", sea, AutoReconnect);

            if (!AutoReconnect)
            {
                CloseConnection();
            }
            else
            {
                StartConnectionValidation();
            }
        }

        public void StartConnectionValidation()
        {
            //if autoreconnect is enabled and connection is closed we should proceed with the wait otherwise the connection is valid
            if (!AutoReconnect || _streamConnection.IsOpen)
            {
                CloseConnection();
            }

            //schedule check in the near future (10s by default) whether the connection has recovered
            //DO NOT use Context in here as this code is likely going to be called as a result of event being raised on a separate thread 
            //Calling Context.Scheduler will result in exception as it's not run within Actor context - this code communicates with the actor via ActorSystem instead
            SdkActorSystem.ActorSystem.Scheduler.ScheduleTellOnce(
                TimeSpan.FromSeconds(UDAPI.Configuration.DisconnectionDelay)
                ,SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath)
                ,new ValidateMessage()
                ,ActorRefs.NoSender);
        }

        public void ValidateConnection()
        {
            //validate whether the reconnection was successful 
            _logger.InfoFormat("Starting validation for reconnection connHash={0}", _streamConnection.GetHashCode());

            //in case the connection is swapped by RMQ library while the check is running
            var testConnection = _streamConnection;

            if (testConnection == null)
            {
                _logger.WarnFormat(
                    "Reconnection failed, connection has been disposed, the disconnection event needs to be raised");
                return;
            }

            _logger.InfoFormat("Veryfing that connection is open open={0}", testConnection.IsOpen);

            if (testConnection.IsOpen)
            {
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new ResetAllEchoesMessage());
                _logger.InfoFormat("Reconnection successful, disconnection event will not be raised");
            }
            else
            {
                CloseConnection();
            }
        }

        protected virtual void OnConnectionStatusChanged(ConnectionState newState)
        {
            State = newState;
                  
            switch (newState)
            {
                case ConnectionState.DISCONNECTED:
                    Self.Tell(new DisconnectedMessage());
                    break;
                case ConnectionState.CONNECTED:
                    Self.Tell(new ConnectedMessage());
                    ConnectionError = null;
                    break;
                case ConnectionState.CONNECTING:
                    break;
            }
        }

        #endregion

        #region Consumer

        protected void AddConsumer(IConsumer consumer, int echoInterval, int echoMaxDelay)
        {
            if (consumer == null)
                throw new ArgumentNullException("consumer");

            // Note that between Connect() and AddConsumerToQueue()
            // error could be raised (i.e. connection went down), 
            // so by the time we reach AddConsumerToQueue(),
            // nothing can be assumed

            Connect(consumer);

            if (_connectionCancellation.IsCancellationRequested)
                throw new InvalidOperationException("StreamController is shutting down");

            if (State != ConnectionState.CONNECTED)
                throw new Exception("Connection is not open - cannot register consumerId=" + consumer.Id);

            var subscriberResponseResult = Dispatcher.Ask(new RetrieveSubscriberMessage() {Id = consumer.Id}).Result;

            if (subscriberResponseResult is StreamSubscriber)
                throw new InvalidOperationException("consumerId=" + consumer.Id + " cannot be registred twice");

            AddConsumerToQueue(consumer);
        }

        protected virtual void AddConsumerToQueue(IConsumer consumer)
        {

            var queue = consumer.GetQueueDetails();
            if (queue == null || string.IsNullOrEmpty(queue.Name))
                throw new Exception("Invalid queue details");
            
            var model = _streamConnection.CreateModel();
            StreamSubscriber subscriber = null;

            try
            {
                subscriber = new StreamSubscriber(model, consumer, Dispatcher);
                subscriber.StartConsuming(queue.Name);
            }
            catch
            {
                if (subscriber != null)
                    subscriber.Dispose();

                throw;
            }
        }

        public void RemoveConsumer(IConsumer consumer)
        {
            if (consumer == null)
                throw new ArgumentNullException("consumer");

            RemoveConsumerFromQueue(consumer);
        }

        protected virtual void RemoveConsumerFromQueue(IConsumer consumer)
        {
            var sub = Dispatcher.Ask(new RetrieveSubscriberMessage {Id = consumer.Id }).Result as IStreamSubscriber;
            if (sub != null)
                sub.StopConsuming();
        }

        #endregion

        #region IDisposable Members

        public virtual void Shutdown()
        {
            _logger.Debug("Shutting down StreamController");
            _connectionCancellation.Cancel();

            Dispatcher.Tell(new DisposeMessage());
            CloseConnection();

            _logger.Info("StreamController correctly disposed");
        }

        public void Dispose()
        {
            Shutdown();
        }

        #endregion
        
        #region Private messages
        private class ConnectedMessage
        {

        }

        private class DisconnectedMessage
        {

        }

        private class ValidateMessage
        {
        
        }

        #endregion

        
    }

    #region Messages

    #endregion
}
