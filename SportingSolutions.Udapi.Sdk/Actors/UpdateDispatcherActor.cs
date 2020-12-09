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
using Akka.Util.Internal;
using Microsoft.Extensions.Logging;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;
using System;
using System.Collections.Generic;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    public class UpdateDispatcherActor : ReceiveActor
    {
        public const string ActorName = "UpdateDispatcherActor";

        private readonly Dictionary<string, ResourceSubscriber> _subscribers;

        internal int SubscribersCount => _subscribers.Count;

        private ILoggerFactory LoggerFactory { get; }
        private ILogger<UpdateDispatcherActor> Logger { get; }

        public UpdateDispatcherActor(ILoggerFactory factory)
        {
            LoggerFactory = factory;
            Logger = factory.CreateLogger<UpdateDispatcherActor>();

            _subscribers = new Dictionary<string, ResourceSubscriber>();
            Receive<StreamUpdateMessage>(message => ProcessMessage(message));

            Receive<DisconnectMessage>(message => Disconnect(message));
            Receive<NewSubscriberMessage>(message => AddSubscriber(message.Subscriber));
            Receive<RemoveSubscriberMessage>(message => RemoveSubscriber(message.Subscriber));

            Receive<RetrieveSubscriberMessage>(x => AskForSubscriber(x.Id));
            Receive<SubscribersCountMessage>(x => AskSubsbscribersCount());
            Receive<RemoveAllSubscribers>(x => RemoveAll());

            Receive<DisposeMessage>(x => Dispose());

            Logger.LogInformation("UpdateDispatcherActor was created");
        }

        protected override void PreRestart(Exception reason, object message)
        {
            Logger.LogError(
                $"Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));
            base.PreRestart(reason, message);
        }

        private void Disconnect(DisconnectMessage message)
        {
            Logger.LogDebug($"subscriberId={message.Id} disconnect message received");
            if (!_subscribers.ContainsKey(message.Id)) return;

            try
            {
                var subscriber = _subscribers[message.Id];
                _subscribers.Remove(message.Id);

                subscriber.Resource.Tell(message);

                Logger.LogDebug(
                    $"subscriberId={message.Id} removed from UpdateDispatcherActor and stream disconnected; subscribersCount={_subscribers.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"failed removing/disconnecting subscriberId={message.Id}", ex);
            }
        }

        private void Connect(IStreamSubscriber subscriber)
        {
            var newResourceSubscriber = new ResourceSubscriber
            {
                Resource = Context.ActorOf(Props.Create<ResourceActor>(subscriber.Consumer, LoggerFactory.CreateLogger<ResourceActor>())),
                StreamSubscriber = subscriber
            };

            _subscribers[subscriber.Consumer.Id] = newResourceSubscriber;
            newResourceSubscriber.Resource.Tell(new ConnectMessage() { Id = subscriber.Consumer.Id, Consumer = subscriber.Consumer });
        }

        private void ProcessMessage(StreamUpdateMessage message)
        {
            // is this an echo message?
            if (message.Message.StartsWith("{\"Relation\":\"http://api.sportingsolutions.com/rels/stream/echo\""))
            {
                Logger.LogDebug($"Echo arrived for fixtureId={message.Id}");
                Context.ActorSelection(SdkActorSystem.EchoControllerActorPath)
                    .Tell(new EchoMessage { Id = message.Id, Message = message.Message });
            }
            else if (_subscribers.ContainsKey(message.Id))
            {
                //stream update is passed to the resource
                _subscribers[message.Id].Resource.Tell(message);
            }
            else
            {
                Logger.LogWarning($"ProcessMessage subscriberId={message.Id} was not found");
            }
        }

        private void AddSubscriber(IStreamSubscriber subscriber)
        {
            if (subscriber == null)
                return;

            Connect(subscriber);

            Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new NewSubscriberMessage { Subscriber = subscriber });

            Logger.LogInformation($"consumerId={subscriber.Consumer.Id} added to the dispatcher, count={_subscribers.Count}");
        }

        private void AskSubsbscribersCount()
        {
            Sender.Tell(_subscribers.Count);
        }

        private void AskForSubscriber(string subscriberId)
        {
            ResourceSubscriber resourceSubscriber;
            if (_subscribers.TryGetValue(subscriberId, out resourceSubscriber) && resourceSubscriber != null)
                Sender.Tell(resourceSubscriber.StreamSubscriber);
            else
                Sender.Tell(new NotFoundMessage());
        }

        private void RemoveSubscriber(IStreamSubscriber subscriber)
        {
            if (subscriber == null)
                return;

            Logger.LogDebug($"Removing consumerId={subscriber.Consumer.Id} from dispatcher");

            ResourceSubscriber resourceSubscriber;
            _subscribers.TryGetValue(subscriber.Consumer.Id, out resourceSubscriber);
            if (resourceSubscriber == null)
            {
                Logger.LogWarning(
                    $"consumerId={subscriber.Consumer.Id} can't be removed from the dispatcher as it was not found.");
                return;
            }

            try
            {
                var disconnectMsg = new DisconnectMessage { Id = subscriber.Consumer.Id };
                Self.Tell(disconnectMsg);
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath)
                    .Tell(new RemoveSubscriberMessage { Subscriber = subscriber });
            }
            catch (Exception ex)
            {
                Logger.LogError($"RemoveSubscriber for consumerId={subscriber.Consumer?.Id ?? "null"} failed", ex);
            }
        }

        private void RemoveAll()
        {
            if (_subscribers.Count > 0)
            {
                Logger.LogDebug("Sending disconnection to count={0} consumers", _subscribers.Count);
                _subscribers.Values.ForEach(
                    x => Self.Tell(new DisconnectMessage { Id = x.StreamSubscriber.Consumer.Id }));
                Logger.LogInformation("All consumers are notified about disconnection");
            }
        }

        private void Dispose()
        {
            Logger.LogDebug("Disposing dispatcher");

            try
            {
                RemoveAll();
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new DisposeMessage());
            }
            catch (Exception ex)
            {
                Logger.LogError("Dispose failed", ex);
            }
        }

        private class ResourceSubscriber
        {
            internal IActorRef Resource { get; set; }
            internal IStreamSubscriber StreamSubscriber { get; set; }
        }
    }

    internal class RetrieveSubscriberMessage
    {
        public string Id { get; set; }
    }

    //Message used to get count if subscribers
    internal class SubscribersCountMessage
    {
    }

    internal class RemoveAllSubscribers
    {
    }
}
