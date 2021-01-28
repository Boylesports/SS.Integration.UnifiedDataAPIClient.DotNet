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
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    internal class EchoControllerActor : ReceiveActor, IEchoController
    {
        private class EchoEntry
        {
            public IStreamSubscriber Subscriber;
            public int EchosCountDown;
        }

        public const string ActorName = "EchoControllerActor";

        private ILogger Logger { get; }

        private readonly ConcurrentDictionary<string, EchoEntry> _consumers;
        private readonly ICancelable _echoCancellation = new Cancelable(Context.System.Scheduler);

        public EchoControllerActor(ILogger<EchoControllerActor> logger)
        {
            Logger = logger;
            Enabled = UDAPI.Configuration.UseEchos;
            _consumers = new ConcurrentDictionary<string, EchoEntry>();

            if (Enabled)
            {
                //this will send Echo Message to the EchoControllerActor (Self) at the specified interval
                Context.System.Scheduler.ScheduleTellRepeatedly(
                    TimeSpan.FromSeconds(0),
                    TimeSpan.FromMilliseconds(UDAPI.Configuration.EchoWaitInterval),
                    Self,
                    new SendEchoMessage(),
                    ActorRefs.Nobody);
            }

            Logger.LogDebug("EchoSender is {0}", Enabled ? "enabled" : "disabled");

            Receive<NewSubscriberMessage>(x => AddConsumer(x.Subscriber));
            Receive<RemoveSubscriberMessage>(x => RemoveConsumer(x.Subscriber));
            Receive<EchoMessage>(x => ProcessEcho(x.Id));
            ReceiveAsync<SendEchoMessage>(x => CheckEchos());
            Receive<DisposeMessage>(x => Dispose());
        }

        protected override void PreRestart(Exception reason, object message)
        {
            Logger.LogError(
                $"Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));

            StopConsumingAll();
            Context.ActorSelection(SdkActorSystem.FaultControllerActorPath).Tell(new CriticalActorRestartedMessage() { ActorName = ActorName });

            base.PreRestart(reason, message);
        }

        private void StopConsumingAll()
        {
            if (_consumers != null)
                RemoveSubribers(_consumers.Values.Select(_ => _.Subscriber));
        }

        public bool Enabled { get; private set; }

        public virtual void AddConsumer(IStreamSubscriber subscriber)
        {
            if (!Enabled || subscriber == null)
                return;

            _consumers[subscriber.Consumer.Id] = new EchoEntry
            {
                Subscriber = subscriber,
                EchosCountDown = UDAPI.Configuration.MissedEchos
            };

            Logger.LogDebug("consumerId={0} added to echos manager", subscriber.Consumer.Id);
        }

        public void ResetAll()
        {
            //this is used on disconnection when auto-reconnection is used
            //some echoes will usually be missed in the process 
            //once the connection is restarted it makes sense to reset echo counter
            foreach (var echoEntry in _consumers.Values)
            {
                echoEntry.EchosCountDown = UDAPI.Configuration.MissedEchos;
            }

        }

        public void RemoveConsumer(IStreamSubscriber subscriber)
        {
            if (!Enabled || subscriber == null)
                return;

            EchoEntry tmp;
            if (_consumers.TryRemove(subscriber.Consumer.Id, out tmp))
                Logger.LogDebug("consumerId={0} removed from echos manager", subscriber.Consumer.Id);
        }

        public void RemoveAll()
        {
            if (!Enabled)
                return;

            _consumers.Clear();
        }

        public void ProcessEcho(string subscriberId)
        {
            EchoEntry entry;
            if (!string.IsNullOrEmpty(subscriberId) && _consumers.TryGetValue(subscriberId, out entry))
            {
                if (UDAPI.Configuration.VerboseLogging)
                    Logger.LogDebug("Resetting echo information for fixtureId={0}", subscriberId);

                entry.EchosCountDown = UDAPI.Configuration.MissedEchos;
            }
        }

        private async Task CheckEchos()
        {
            try
            {
                List<IStreamSubscriber> invalidConsumers = new List<IStreamSubscriber>();

                // acquiring the consumer here prevents to put another lock on the
                // dictionary

                Logger.LogInformation($"CheckEchos consumersCount={_consumers.Count}");

                IStreamSubscriber sendEchoConsumer = _consumers.Values.FirstOrDefault(_ => _.EchosCountDown > 0)?.Subscriber;

                foreach (var consumer in _consumers)
                {
                    if (consumer.Value.EchosCountDown < UDAPI.Configuration.MissedEchos)
                    {
                        var msg = $"consumerId={consumer.Key} missed count={UDAPI.Configuration.MissedEchos - consumer.Value.EchosCountDown} echos";
                        if (consumer.Value.EchosCountDown < 1)
                        {
                            Logger.LogWarning($"{msg} and it will be disconnected");
                            invalidConsumers.Add(consumer.Value.Subscriber);
                        }
                        else
                        {
                            Logger.LogInformation(msg);
                        }
                    }
                    consumer.Value.EchosCountDown--;
                }

                // this wil force indirectly a call to EchoManager.RemoveConsumer(consumer)
                // for the invalid consumers
                RemoveSubribers(invalidConsumers);

                invalidConsumers.Clear();

                await SendEchos(sendEchoConsumer);
            }
            catch (Exception ex)
            {
                Logger.LogError("Check Echos has experienced a failure", ex);
            }
        }

        private void RemoveSubribers(IEnumerable<IStreamSubscriber> subscribers)
        {
            foreach (var s in subscribers)
            {
                Context.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new RemoveConsumerMessage() { Consumer = s.Consumer });
                Self.Tell(new RemoveSubscriberMessage() { Subscriber = s });
            }
        }

        private async Task SendEchos(IStreamSubscriber item)
        {
            if (item == null)
            {
                return;
            }
            try
            {
                Logger.LogDebug("Sending batch echo");
                await item.Consumer.SendEcho();
            }
            catch (Exception e)
            {
                Logger.LogError("Error sending echo-request", e);
            }

        }

        public void Dispose()
        {
            Logger.LogDebug("Disposing EchoSender");
            _echoCancellation.Cancel();

            RemoveAll();

            Logger.LogInformation("EchoSender correctly disposed");
        }

        internal class SendEchoMessage
        {
        }

        internal int? GetEchosCountDown(string subscriberId)
        {
            EchoEntry entry;
            if (!string.IsNullOrEmpty(subscriberId) && _consumers.TryGetValue(subscriberId, out entry))
            {
                return entry.EchosCountDown;
            }
            return null;
        }

        internal int ConsumerCount => _consumers.Count;
    }
}
