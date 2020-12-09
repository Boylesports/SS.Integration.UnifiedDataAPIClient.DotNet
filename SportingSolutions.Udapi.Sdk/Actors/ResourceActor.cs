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
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;
using System;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    public class ResourceActor : ReceiveActor
    {
        private ILogger<ResourceActor> Logger { get; }
        private IConsumer Consumer { get; }
        public string Id => GetHashCode().ToString();
        public bool IsDisposed { get; internal set; }

        public ResourceActor(IConsumer resource, ILogger<ResourceActor> log)
        {
            Logger = log;
            Consumer = resource;

            Logger.LogDebug($"resourceActorId={Id} Instantiated fixtureName=\"{resource}\" fixtureId=\"{Id}\"");

            Become(DisconnectedState);
        }

        protected override void PreRestart(Exception reason, object message)
        {
            Logger.LogError(
                $"resourceActorId={Id} Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));
            base.PreRestart(reason, message);
        }

        private void DisconnectedState()
        {
            Receive<ConnectMessage>(connectMsg => Connected(connectMsg));
            Receive<DisconnectMessage>(msg => Disconnect(msg));
        }

        private void Disconnect(DisconnectMessage msg)
        {
            Logger.LogDebug($"resourceActorId={Id} Disconnection message raised for {msg.Id}");
            Consumer.OnStreamDisconnected();
        }

        private void StreamUpdate(StreamUpdateMessage streamMsg)
        {
            Logger.LogDebug($"resourceActorId={Id} New update arrived for {streamMsg.Id}");
            Consumer.OnStreamEvent(new StreamEventArgs(streamMsg.Message, streamMsg.ReceivedAt));
        }

        private void Connected(ConnectMessage connectMsg)
        {
            Consumer.OnStreamConnected();
            Become(BecomeConnected);
        }

        private void BecomeConnected()
        {
            Receive<StreamUpdateMessage>(streamMsg => StreamUpdate(streamMsg));
            Receive<DisconnectMessage>(msg => Disconnect(msg));
        }

        public void StartStreaming(int echoInterval, int echoMaxDelay)
        {
            Logger.LogDebug($"resourceActorId={Id} REQUESTING Streaming request for fixtureName=\"{((IResource)Consumer)?.Name}\" fixtureId=\"{Id}\"");
            SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new NewConsumerMessage() { Consumer = Consumer });
            Logger.LogDebug($"resourceActorId={Id} REQUESTED Streaming request queued for fixtureName=\"{((IResource)Consumer)?.Name}\" fixtureId=\"{Id}\"");
        }

        public void PauseStreaming()
        {
            //Logger.Debug("Streaming paused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            //_pauseStream.Reset();
        }

        public void UnPauseStreaming()
        {
            //Logger.Debug("Streaming unpaused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            //_pauseStream.Set();
        }

        public void StopStreaming()
        {
            SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new RemoveConsumerMessage() { Consumer = Consumer });

            //StreamController.Instance.RemoveConsumer(_resource);
            Logger.LogDebug($"resourceActorId={Id} REQUESTED Streaming stopped for fixtureName=\"{((IResource)Consumer)?.Name}\" fixtureId=\"{Id}\"");
        }

        public void Dispose()
        {
            StopStreaming();
            IsDisposed = true;
        }
    }
}