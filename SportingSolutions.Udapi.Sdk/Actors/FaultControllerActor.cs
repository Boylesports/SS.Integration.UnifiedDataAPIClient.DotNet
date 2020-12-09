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
using SportingSolutions.Udapi.Sdk.Model.Message;
using SdkErrorMessage = SportingSolutions.Udapi.Sdk.Events.SdkErrorMessage;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    public class FaultControllerActor : ReceiveActor
    {
        private IActorRef subscriber;

        private ILogger<FaultControllerActor> Logger { get; }

        public const string ActorName = "FaultControllerActor";

        public FaultControllerActor(ILogger<FaultControllerActor> log)
        {
            Logger = log;

            Receive<CriticalActorRestartedMessage>(message => OnActorRestarted(message, true));
            Receive<PathMessage>
            
            (message =>
            {
                Logger.LogInformation($"Registering subscriber {subscriber}");
                subscriber = Sender;
                subscriber.Tell(new PathMessage());
                
            });
        }

        //public 

        public void OnActorRestarted(CriticalActorRestartedMessage message, bool isCritical)
        {
            subscriber.Tell(new SdkErrorMessage($"Actor restarted {message.ActorName}", isCritical) );
        }
    }
}
