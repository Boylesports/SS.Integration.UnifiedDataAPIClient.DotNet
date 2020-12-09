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

using Akka.Dispatch;
using Akka.Actor;
using Akka.Configuration;

namespace SportingSolutions.Udapi.Sdk.Actors.MailBox
{
    public class EchoControllerActorMailBox : UnboundedPriorityMailbox
    {
        public EchoControllerActorMailBox(Settings settings, Config config) : base(settings, config)
        {
        }

        protected override int PriorityGenerator(object message)
        {
            if (message is Model.Message.EchoMessage)
                return 0;

            if (message is EchoControllerActor.SendEchoMessage)
                return 2;

            return 1;
        }
    }
}
