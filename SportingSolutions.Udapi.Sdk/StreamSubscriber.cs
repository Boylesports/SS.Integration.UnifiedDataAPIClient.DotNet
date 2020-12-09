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
using RabbitMQ.Client.Exceptions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;
using System;
using System.Text;


namespace SportingSolutions.Udapi.Sdk
{

    internal class StreamSubscriber : DefaultBasicConsumer, IStreamSubscriber
    {
        public IConsumer Consumer { get; }
        public IActorRef Dispatcher { get; }
        private ILogger<StreamSubscriber> Logger { get; }
        private string ConsumerTag => Consumer.Id;

        internal bool IsStreamingStopped => Model == null || !Model.IsOpen || _isCanceled;
        private bool _isCanceled = false;

        public StreamSubscriber(IModel model, IConsumer consumer, IActorRef dispatcher, ILogger<StreamSubscriber> log)
            : base(model)
        {
            Consumer = consumer;
            Dispatcher = dispatcher;
        }

        public void StartConsuming(string queueName)
        {
            try
            {
                Model.BasicConsume(queueName, true, ConsumerTag, this);
                _isCanceled = false;
                //_logger.Debug($"Streaming started for consumerId={ConsumerTag}");
            }
            catch (Exception e)
            {
                Logger.LogError("Error starting stream for consumerId=" + ConsumerTag, e);
                throw;
            }
        }

        public void StopConsuming()
        {
            try
            {
                if (!IsStreamingStopped)
                {
                    Model.BasicCancel(ConsumerTag);
                    _isCanceled = true;
                }
            }
            catch (AlreadyClosedException e)
            {
                Logger.LogWarning($"Connection already closed for consumerId={ConsumerTag} , \n {e}");
            }

            catch (ObjectDisposedException e)
            {
                Logger.LogWarning("Stop stream called for already disposed object for consumerId=" + ConsumerTag, e);
            }

            catch (TimeoutException e)
            {
                Logger.LogWarning($"RabbitMQ timeout on StopStreaming for consumerId={ConsumerTag} {e}");
            }

            catch (Exception e)
            {
                Logger.LogError("Error stopping stream for consumerId=" + ConsumerTag, e);
            }
            finally
            {
                Logger.LogDebug("Streaming stopped for consumerId={0}", ConsumerTag);
            }
        }

        public override void HandleBasicConsumeOk(string consumerTag)
        {
            Logger.LogDebug($"HandleBasicConsumeOk consumerTag={consumerTag ?? "null"}");
            Dispatcher.Tell(new NewSubscriberMessage { Subscriber = this });

            base.HandleBasicConsumeOk(consumerTag);
        }

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            if (!IsRunning)
                return;

            Logger.LogDebug(
                "HandleBasicDeliver" +
                $" consumerTag={consumerTag ?? "null"}" +
                $" deliveryTag={deliveryTag}" +
                $" redelivered={redelivered}" +
                $" exchange={exchange ?? "null"}" +
                $" routingKey={routingKey ?? "null"}" +
                (body.IsEmpty ? " body=null" : $" bodyLength={body.Length}"));

            Dispatcher.Tell(new StreamUpdateMessage { Id = consumerTag, Message = Encoding.UTF8.GetString(body.ToArray()), ReceivedAt = DateTime.UtcNow });
        }

        public override void HandleBasicCancel(string consumerTag)
        {
            Logger.LogDebug($"HandleBasicCancel consumerTag={consumerTag ?? "null"}");
            base.HandleBasicCancel(consumerTag);
            Dispatcher.Tell(new RemoveSubscriberMessage { Subscriber = this });
        }
    }
}
