//Copyright 2020 BoyleSports Ltd.
//Copyright 2012 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using Akka.Actor;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;
using System;
using System.Collections.Generic;
using System.Threading;

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
        internal enum ConnectionState
        {
            DISCONNECTED = 0,
            CONNECTING = 1,
            CONNECTED = 2
        }

        public const string ActorName = "StreamControllerActor";
        public const int NewConsumerErrorLimit = 10;
        public const int NewConsumerErrorLimitForConsumer = 3;

        private ILoggerFactory LoggerFactory { get; }
        private ILogger<StreamControllerActor> Logger { get; }
        private int ProcessNewConsumerErrorCounter { get; set; }
        private ICancelable ValidateCancellation { get; set; }

        private ConnectionFactory ConnectionFactory { get; set; }
        private IConnection StreamConnection { get; set; }
        private IModel Model { get; set; }
        private bool IsModelDisposed { get; set; } = true;

        /// <summary>
        /// Is AutoReconnect enabled
        /// </summary>
        public bool AutoReconnect { get; private set; }

        /// <summary>
        /// Returns the IDispatcher object that is responsible
        /// of dispatching messages to the consumers.
        /// </summary>
        internal IActorRef Dispatcher { get; private set; }
        public IStash Stash { get; set; }
        private ICancelable ConnectionCancellation { get; } = new Cancelable(Context.System.Scheduler);
        private Dictionary<string, int> NewConsumerErrorsCount { get; } = new Dictionary<string, int>();

        private ConnectionState State { get; set; }
        private bool NeedRaiseDisconnect => State == ConnectionState.CONNECTED && (StreamConnection == null || !StreamConnection.IsOpen);
        private bool IsModelValid => Model != null && !IsModelDisposed && Model.IsOpen;
        private string ConnectionStatus => StreamConnection == null ? "NULL" : (StreamConnection.IsOpen ? "open" : "closed");

        public StreamControllerActor(IActorRef dispatcherActor, ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
            Logger = loggerFactory.CreateLogger<StreamControllerActor>();

            Dispatcher = dispatcherActor ?? throw new ArgumentNullException("dispatcher");
            ProcessNewConsumerErrorCounter = 0;
            //Start in Disconnected state
            DisconnectedState();

            AutoReconnect = UDAPI.Configuration.AutoReconnect;
            CancelValidationMessages();
            ValidateCancellation = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(10000, 10000, Self, new ValidateStateMessage(), Self);

            Logger.LogDebug("StreamController initialised, AutoReconnect={0}", AutoReconnect);
        }

        private void CancelValidationMessages()
        {

            if (ValidateCancellation == null)
                return;
            Logger.LogDebug("CancelValidationMessages triggered");
            ValidateCancellation.Cancel();
            ValidateCancellation = null;
        }

        protected override void PreRestart(Exception reason, object message)
        {
            Logger.LogError(
                $"Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));

            CancelValidationMessages();

            base.PreRestart(reason, new DisconnectedMessage { IDConnection = StreamConnection?.GetHashCode() });
        }

        /// <summary>
        /// This is the state when the connection is closed
        /// </summary>
        private void DisconnectedState()
        {
            Logger.LogInformation("Moved to DisconnectedState");

            Receive<ValidateConnectionMessage>(x => ValidateConnection(x));
            Receive<ConnectedMessage>(x => Become(ConnectedState));

            Receive<NewConsumerMessage>(x =>
            {
                Stash.Stash();
                GetQueueDetailsAndEstablisConnection(x.Consumer);

            });
            Receive<RemoveConsumerMessage>(x => RemoveConsumer(x.Consumer));
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateStateMessage>(x => ValidateState(x));
            Receive<DisconnectedMessage>(x => DisconnecteOnDisconnectedHandler(x));
            Receive<CreateModelMessage>(x => CreateModel());
            State = ConnectionState.DISCONNECTED;
        }




        /// <summary>
        /// this is the state when the connection is being automatically recovered by RabbitMQ
        /// </summary>
        private void ValidationState()
        {
            Logger.LogInformation("Moved to ValidationState");

            Receive<DisconnectedMessage>(x => DisconnectedHandler(x));
            Receive<ValidateConnectionMessage>(x => ValidateConnection(x));
            Receive<ValidationSucceededMessage>(x => Become(ConnectedState));
            Receive<NewConsumerMessage>(x => Stash.Stash());
            Receive<RemoveConsumerMessage>(x => Stash.Stash());
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateStateMessage>(x => ValidateState(x));
            Receive<CreateModelMessage>(x => CreateModel());

            State = ConnectionState.DISCONNECTED;
        }



        /// <summary>
        /// this is the state when the connection is open
        /// </summary>
        private void ConnectedState()
        {
            Logger.LogInformation("Moved to ConnectedState");

            Receive<ValidationStartMessage>(x => ValidationStart(x));
            Receive<NewConsumerMessage>(x =>
            {
                if (ValidateNewConsumerCanBeProcessed(x.Consumer))
                {
                    NewConsumerHandler(x);
                }
                else
                {
                    Stash.Stash();
                }
            });
            Receive<DisconnectedMessage>(x => DisconnectedHandler(x));
            Receive<RemoveConsumerMessage>(x => RemoveConsumer(x.Consumer));
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateConnectionMessage>(x => ValidateConnection(x));
            Receive<ValidateStateMessage>(x => ValidateState(x));

            State = ConnectionState.CONNECTED;
            Stash.UnstashAll();

        }

        private void ValidationStart(ValidationStartMessage validationStartMessage)
        {
            Logger.LogDebug("ValidationStart triggered");
            Become(ValidationState);

            //schedule check in the near future (10s by default) whether the connection has recovered
            //DO NOT use Context in here as this code is likely going to be called as a result of event being raised on a separate thread 
            //Calling Context.Scheduler will result in exception as it's not run within Actor context - this code communicates with the actor via ActorSystem instead
            SdkActorSystem.ActorSystem.Scheduler.ScheduleTellOnce(
                TimeSpan.FromSeconds(UDAPI.Configuration.DisconnectionDelay)
                , SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath)
                , new ValidateConnectionMessage()
                , ActorRefs.NoSender);
        }



        private DisconnectedMessage DefaultDisconnectedMessage => new DisconnectedMessage { IDConnection = StreamConnection?.GetHashCode() };




        private void NewConsumerHandler(NewConsumerMessage newConsumerMessage)
        {
            if (ProcessNewConsumer(newConsumerMessage.Consumer))
            {
                HandleNewConsumerMessageProcessed(newConsumerMessage);
            }
            else
            {
                HandleNewConsumerMessageUnprocessed(newConsumerMessage);
            }
        }

        private void HandleNewConsumerMessageProcessed(NewConsumerMessage newConsumerMessage)
        {
            var fixtureId = newConsumerMessage?.Consumer?.Id;

            if (fixtureId == null)
            {
                Logger.LogWarning("HandleNewConsumerMessageProcessed failed as fixtureId=NULL");
                return;
            }

            if (NewConsumerErrorsCount.ContainsKey(fixtureId))
            {
                NewConsumerErrorsCount.Remove(fixtureId);
            }
        }

        private void HandleNewConsumerMessageUnprocessed(NewConsumerMessage newConsumerMessage)
        {
            var fixtureId = newConsumerMessage?.Consumer?.Id;

            if (fixtureId == null)
            {
                Logger.LogWarning("HandleNewConsumerMessageUnprocessed failed as fixtureId=NULL");
                return;
            }

            if (NewConsumerErrorsCount.ContainsKey(fixtureId))
            {
                NewConsumerErrorsCount[fixtureId] = NewConsumerErrorsCount[fixtureId] + 1;
            }
            else
            {
                NewConsumerErrorsCount[fixtureId] = 1;
            }


            if (NewConsumerErrorsCount[fixtureId] > NewConsumerErrorLimitForConsumer)
            {
                Logger.LogWarning($"HandleNewConsumerMessageUnprocessed message will not be resend for fixtureId={fixtureId}");
            }
            else
            {
                Logger.LogWarning($"HandleNewConsumerMessageUnprocessed message will be resend for fixtureId={fixtureId}");
                SdkActorSystem.ActorSystem.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(10), Self, newConsumerMessage, Self);

            }
        }

        private bool ProcessNewConsumer(IConsumer consumer)
        {
            Logger.LogDebug($"Method=ProcessNewConsumer triggered fixtureId={consumer?.Id} currentState={State.ToString()} connectionStatus={ConnectionStatus}");
            var queue = consumer.GetQueueDetails();
            Logger.LogDebug($"Method=ProcessNewConsumer queueName={queue?.Name}");

            if (string.IsNullOrEmpty(queue?.Name))
            {
                Logger.LogWarning($"Method=ProcessNewConsumer Invalid queue details, fixtureId={consumer?.Id}");
                return false;
            }

            if (!IsModelValid)
            {
                Logger.LogWarning($"Method=ProcessNewConsumer AMQP model not initialized, fixtureId={consumer?.Id}");
                Self.Tell(new CreateModelMessage());
                return false;
            }

            StreamSubscriber subscriber = null;

            try
            {
                subscriber = new StreamSubscriber(Model, consumer, Dispatcher, LoggerFactory.CreateLogger<StreamSubscriber>());
                subscriber.StartConsuming(queue.Name);
            }
            catch (Exception e)
            {
                ProcessNewConsumerErrorCounter++;
                Logger.LogWarning(
                    $"Method=ProcessNewConsumer StartConsuming errored errorsCout={ProcessNewConsumerErrorCounter} for fixtureId={consumer.Id} {e}");
                if (ProcessNewConsumerErrorCounter > NewConsumerErrorLimit)
                    ProcessNewConsumerErrorHandler(e);
                return false;
            }

            Logger.LogDebug($"Method=ProcessNewConsumer successfully executed fixtureId={consumer.Id}");

            ProcessNewConsumerErrorCounter = 0;
            return true;
        }

        private bool ValidateNewConsumerCanBeProcessed(IConsumer consumer)
        {
            if (consumer == null)
            {
                Logger.LogWarning("Method=ProcessNewConsumer Consumer is null");
                return false;
            }

            if (StreamConnection == null || !StreamConnection.IsOpen)
            {
                Logger.LogWarning(
                    $"Method=ProcessNewConsumer connectionStatus={ConnectionStatus} {(StreamConnection == null ? "this should not happening" : "")}");
                DisconnectedHandler(DefaultDisconnectedMessage);
                return false;
            }

            return true;
        }


        private void ProcessNewConsumerErrorHandler(Exception e)
        {
            Logger.LogError($"ProcessNewConsumer limit exceeded with errorsCout={ProcessNewConsumerErrorCounter}  disconnected event will be raised  {e}");
            DisconnectedHandler(DefaultDisconnectedMessage);
            ProcessNewConsumerErrorCounter = 0;
        }

        protected virtual void CloseConnection()
        {
            if (StreamConnection != null)
            {
                Logger.LogDebug($"CloseConnection triggered {StreamConnection}");
                try
                {
                    {
                        StreamConnection.ConnectionShutdown -= OnConnectionShutdown;
                        if (StreamConnection.IsOpen)
                        {
                            StreamConnection.Close();
                            Logger.LogDebug("Connection Closed");
                        }

                    }
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Failed to close connection {e}");
                }

                try
                {
                    {
                        DisposeModel();
                        StreamConnection.Dispose();
                        Logger.LogDebug("Connection Disposed");
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Failed to dispose connection {e}");
                }
                StreamConnection = null;

            }
            else
            {
                Logger.LogDebug("No need to CloseConnection");
            }



        }

        protected void NotifyDispatcherConnectionError()
        {
            try
            {
                Dispatcher.Tell(new RemoveAllSubscribers());
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Failed to tell diapstcher RemoveAllSubscribers diapstcher={Dispatcher}");
            }
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

            CloseConnection();

            Logger.LogDebug("Connecting to the streaming server");

            if (factory == null)
            {
                Logger.LogWarning("Connecting to the streaming server Failed as connectionFactory=NULL");
                return;
            }

            State = ConnectionState.CONNECTING;

            long attempt = 1;
            while (!ConnectionCancellation.IsCancellationRequested)
            {
                Logger.LogDebug("Establishing connection to the streaming server, attempt={0}", attempt);

                try
                {
                    StreamConnection = factory.CreateConnection();
                    StreamConnection.ConnectionShutdown += OnConnectionShutdown;
                    Logger.LogInformation("Connection to the streaming server correctly established");
                    CreateModel();
                    Self.Tell(new ConnectedMessage());
                    return;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error connecting to the streaming server", ex);
                    Thread.Sleep(100);
                }

                attempt++;
            }

            Logger.LogWarning("EstablishConnection connection Cancelled");

            State = ConnectionState.DISCONNECTED;


        }

        private void GetQueueDetailsAndEstablisConnection(IConsumer consumer)
        {
            Logger.LogDebug($"GetQueueDetailsAndEstablisConnection triggered state={State} isCancellationRequested={ConnectionCancellation.IsCancellationRequested}");


            if (State == ConnectionState.CONNECTED || State == ConnectionState.CONNECTING ||
                ConnectionCancellation.IsCancellationRequested)
            {
                Logger.LogInformation($"GetQueueDetailsAndEstablisConnection will not be executed state={State} isCancellationRequested={ConnectionCancellation.IsCancellationRequested}");
                return;
            }

            CreateConectionFactory(consumer);
            EstablishConnection(ConnectionFactory);
        }

        private void CreateConectionFactory(IConsumer consumer)
        {
            QueueDetails queue = null;
            try
            {
                queue = consumer.GetQueueDetails();
                if (queue == null || string.IsNullOrEmpty(queue.Name))
                {
                    var e = new Exception("queue's name is not valid for fixtureId=" + consumer.Id);
                    throw e;
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Error acquiring queue details for fixtureId=" + consumer.Id, e);
                throw;
            }

            //Logger.LogInformation($"ConnectionFactory h={queue.Host} u={queue.UserName} p={queue.Password} ch={queue.VirtualHost}");

            ConnectionFactory = new ConnectionFactory
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(UDAPI.Configuration.AMQPMissedHeartbeat),
                HostName = queue.Host,
                AutomaticRecoveryEnabled = AutoReconnect,
                Port = queue.Port,
                UserName = queue.UserName,
                Password = queue.Password,
                VirtualHost = "/" + queue.VirtualHost // this is not used anymore, we keep it for retro-compatibility
            };
        }

        internal virtual void OnConnectionShutdown(object sender, ShutdownEventArgs sea)
        {
            Logger.LogError($"The AMQP connection was shutdown. AutoReconnect is enabled={AutoReconnect}, sender={sender} {sea}");


            if (!AutoReconnect)
            {
                SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(DefaultDisconnectedMessage);
            }
            else
            {
                SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new ValidationStartMessage());
            }
        }

        private void ValidateState(ValidateStateMessage validateStateMessage)
        {
            var message = $"Method=ValidateState  currentState={State.ToString()} connectionStatus={ConnectionStatus} ";

            if (NeedRaiseDisconnect)
            {
                Logger.LogWarning($"{message} disconnected event will be raised");
                DisconnectedHandler(DefaultDisconnectedMessage);
            }
            else if (State != ConnectionState.DISCONNECTED && !IsModelValid)
            {
                Logger.LogInformation($"{message}. AMQP model will be recreated");
                ReCreateModel();
            }
            else
            {
                Logger.LogDebug(message);
            }
        }

        private void DisconnectedHandler(DisconnectedMessage disconnectedMessage)
        {
            Logger.LogInformation($"Disconnect message received");
            if (State == ConnectionState.DISCONNECTED || State == ConnectionState.CONNECTING)
            {
                Logger.LogWarning($"DisconnectedHandler will not be executed as currentState={State}");
            }

            if (disconnectedMessage.IDConnection != null && disconnectedMessage.IDConnection != StreamConnection?.GetHashCode())
            {
                Logger.LogWarning($"DisconnectedHandler will not be executed as we are already in connection with connectionHash={StreamConnection?.GetHashCode()}, messageConnectionHash={disconnectedMessage?.IDConnection}");
            }


            Become(DisconnectedState);
            NotifyDispatcherConnectionError();
            EstablishConnection(ConnectionFactory);
        }

        private void DisconnecteOnDisconnectedHandler(DisconnectedMessage disconnectedMessage)
        {
            Logger.LogWarning($"Disconnect message On Disconnected state received messageConnectionHash={disconnectedMessage.IDConnection}");
        }

        private void ValidateConnection(ValidateConnectionMessage validateConnectionMessage)
        {
            //validate whether the reconnection was successful 
            Logger.LogInformation("Starting validation for reconnection connHash={0}",
                StreamConnection?.GetHashCode().ToString() ?? "null");

            //in case the connection is swapped by RMQ library while the check is running
            var testConnection = StreamConnection;

            if (testConnection == null)
            {
                Logger.LogWarning("Reconnection failed, connection has been disposed, the disconnection event needs to be raised");
                Self.Tell(new DisconnectedMessage { IDConnection = StreamConnection?.GetHashCode() });

                return;
            }

            Logger.LogInformation("Veryfing that connection is open ? {0}", testConnection.IsOpen);

            if (testConnection.IsOpen)
            {
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new ResetAllEchoesMessage());
                Logger.LogInformation("Reconnection successful, disconnection event will not be raised");

                Self.Tell(new ValidationSucceededMessage());
            }
            else
            {
                Logger.LogWarning("Connection validation failed, connection is not open - calling CloseConnection() to dispose it");
                Self.Tell(new DisconnectedMessage { IDConnection = StreamConnection?.GetHashCode() });
            }
        }

        private void ReCreateModel()
        {
            DisposeModel();
            CreateModel();
        }

        private void CreateModel()
        {
            if (StreamConnection == null || !StreamConnection.IsOpen)
            {
                Logger.LogWarning($"Connection is closed");
                return;
            }

            try
            {
                if (IsModelDisposed)
                {
                    Model = StreamConnection.CreateModel();
                    IsModelDisposed = false;
                    Logger.LogInformation($"AMQP model sucessfully created, channelNo={Model.ChannelNumber}");
                }
                else
                    Logger.LogDebug($"AMQP model already created, channelNo={Model.ChannelNumber}");
            }
            catch (Exception e)
            {
                throw new Exception($"Creating AMQP model errored, errorsCout={ProcessNewConsumerErrorCounter} {e}", e);
            }
        }

        private void DisposeModel()
        {
            if (!IsModelDisposed)
            {
                Model.Dispose();
                Model = null;
                IsModelDisposed = true;
                Logger.LogDebug("AMQP model sucessfully disposed");
            }
            else
                Logger.LogDebug("AMQP model has already disposed");
        }

        protected virtual void AddConsumerToQueue(IConsumer consumer)
        {
            if (consumer == null)
            {
                Logger.LogWarning("Method=AddConsumerToQueue Consumer is null");
                return;
            }
            Logger.LogDebug($"Method=AddConsumerToQueue triggered fixtureId={consumer.Id}");

            var queue = consumer.GetQueueDetails();

            if (string.IsNullOrEmpty(queue?.Name))
            {
                Logger.LogWarning("Method=AddConsumerToQueue Invalid queue details");
                return;
            }

            if (StreamConnection == null)
            {
                Logger.LogWarning($"Method=AddConsumerToQueue StreamConnection is null currentState={State.ToString()}");
                Self.Tell(new DisconnectedMessage { IDConnection = StreamConnection?.GetHashCode() });
                Stash.Stash();
                return;
            }

            if (!StreamConnection.IsOpen)
            {
                Logger.LogWarning($"Method=AddConsumerToQueue StreamConnection is closed currentState={State.ToString()}");
                Self.Tell(new DisconnectedMessage { IDConnection = StreamConnection?.GetHashCode() });
                Stash.Stash();
                return;
            }

            if (!IsModelValid)
            {
                Logger.LogWarning($"Method=ProcessNewConsumer AMQP model not initialized, fixtureId={consumer?.Id}");
                Self.Tell(new CreateModelMessage());
                return;
            }

            StreamSubscriber subscriber = null;

            try
            {
                subscriber = new StreamSubscriber(Model, consumer, Dispatcher, LoggerFactory.CreateLogger<StreamSubscriber>());
                subscriber.StartConsuming(queue.Name);
                Logger.LogDebug($"Consumer with id={consumer.Id} added to queueName={queue.Name}");
            }
            catch (Exception e)
            {
                ProcessNewConsumerErrorCounter++;
                Logger.LogWarning($"Method=AddConsumerToQueue StartConsuming errored for fixtureId={consumer.Id} {e}");
                if (ProcessNewConsumerErrorCounter > NewConsumerErrorLimit)
                    throw;
            }
            Logger.LogDebug($"Method=AddConsumerToQueue successfully executed fixtureId={consumer.Id}");
        }

        public void RemoveConsumer(IConsumer consumer)
        {
            if (consumer == null)
                throw new ArgumentNullException("consumer");

            RemoveConsumerFromQueue(consumer);
        }

        protected virtual void RemoveConsumerFromQueue(IConsumer consumer)
        {
            var subscriber = Dispatcher.Ask(new RetrieveSubscriberMessage { Id = consumer.Id }).Result as IStreamSubscriber;
            if (subscriber != null)
            {
                subscriber.StopConsuming();
                Dispatcher.Tell(new RemoveSubscriberMessage { Subscriber = subscriber });
            }
        }

        public void Dispose()
        {
            Logger.LogDebug("Shutting down StreamController");
            ConnectionCancellation.Cancel();
            DisposeModel();
            CancelValidationMessages();
            Dispatcher.Tell(new DisposeMessage());
            Self.Tell(new DisconnectedMessage { IDConnection = StreamConnection?.GetHashCode() });

            Logger.LogInformation("StreamController correctly disposed");
        }

        public class ConnectedMessage
        {

        }

        public class DisconnectedMessage
        {
            public int? IDConnection { get; set; }
        }

        private class ValidateConnectionMessage
        {
        }

        private class ValidationStartMessage
        {
        }

        private class ValidationSucceededMessage
        {
        }

        private class ValidateStateMessage
        {
        }

        private class CreateModelMessage
        {
        }
    }
}

