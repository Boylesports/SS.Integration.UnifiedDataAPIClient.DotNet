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
using Akka.Event;
using Microsoft.Extensions.Logging;
using SportingSolutions.Udapi.Sdk.Actors;
using System.Threading.Tasks;

namespace SportingSolutions.Udapi.Sdk
{
    public static class SdkActorSystem
    {
        public static ILoggerFactory LoggerFactory { internal get; set; }
        public static ActorSystem ActorSystem { get; internal set; } = ActorSystem.Create("SDKSystem");

        private const string UserSystemPath = "/user/";

        public static bool InitializeActors { get; set; } = true;
        
        public static readonly string UpdateDispatcherPath = UserSystemPath + UpdateDispatcherActor.ActorName;
        public static readonly string StreamControllerActorPath = UserSystemPath + StreamControllerActor.ActorName;
        public static readonly string EchoControllerActorPath = UserSystemPath + EchoControllerActor.ActorName;
        public static readonly string FaultControllerActorPath = UserSystemPath + FaultControllerActor.ActorName;

        public static ICanTell FaultControllerActorRef { set; get; }

        public static async ValueTask Init(bool needToRecreateActorSystem = false)
        {
            if (InitializeActors)
            {
                if (needToRecreateActorSystem)
                {
                    if (ActorSystem != null)
                        await TerminateActorSystem();

                    ActorSystem = ActorSystem.Create("SDKSystem");
                    //Logger.Debug($"UDAPI ActorSystem is created, creating actors...");
                }

                var dispatcher = ActorSystem.ActorOf(
                    Props.Create(() => new UpdateDispatcherActor(LoggerFactory)),
                    UpdateDispatcherActor.ActorName);

               ActorSystem.ActorOf(
                    Props.Create(() => new StreamControllerActor(dispatcher, LoggerFactory)),
                    StreamControllerActor.ActorName);
                
                ActorSystem.ActorOf(
                    Props.Create(() => new EchoControllerActor(LoggerFactory.CreateLogger<EchoControllerActor>())).WithMailbox("echocontrolleractor-mailbox"),
                    EchoControllerActor.ActorName);

                FaultControllerActorRef = ActorSystem.ActorOf(
                    Props.Create(() =>new FaultControllerActor(LoggerFactory.CreateLogger<FaultControllerActor>())),
                    FaultControllerActor.ActorName);

                var deadletterWatchActorRef = ActorSystem.ActorOf(
                    Props.Create(() => new SdkDeadletterMonitorActor(LoggerFactory.CreateLogger<SdkDeadletterMonitorActor>())),
                    "SdkDeadletterMonitorActor");

                // subscribe to the event stream for messages of type "DeadLetter"
                ActorSystem.EventStream.Subscribe(deadletterWatchActorRef, typeof(DeadLetter));

                InitializeActors = false;
            }
        }

        private static async Task TerminateActorSystem()
        {
            //Logger.Debug("Terminating UDAPI ActorSystem...");
            await ActorSystem.Terminate();
            //Logger.Debug("UDAPI ActorSystem has been terminated");
        }
    }
}
