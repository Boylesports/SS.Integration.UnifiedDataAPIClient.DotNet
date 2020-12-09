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
using Akka.Configuration;
using Akka.Dispatch;
using Microsoft.Extensions.Logging;

namespace SportingSolutions.Udapi.Sdk.Actors.MailBox
{
    public class StreamControllerActorMailBox: UnboundedPriorityMailbox
    {
        private ILogger<StreamControllerActorMailBox> Logger { get; }

        public StreamControllerActorMailBox(Settings settings, Config config, ILogger<StreamControllerActorMailBox> log) : base(settings, config)
        {
            Logger = log;
        }

        protected override int PriorityGenerator(object message)
        {
            var messageType = message.GetType();
            Logger.LogDebug($"StreamControlerActorMailBox.PriorityGenerator entry with messageType={messageType}");
            if (message is StreamControllerActor.DisconnectedMessage)
            {
                Logger.LogDebug($"StreamControlerActorMailBox.PriorityGenerator priority=0 with messageType={messageType}");
                return 0;
            }
            Logger.LogDebug($"StreamControlerActorMailBox.PriorityGenerator priority=1 with messageType={messageType}");
            return 1;
        }
    }
}
