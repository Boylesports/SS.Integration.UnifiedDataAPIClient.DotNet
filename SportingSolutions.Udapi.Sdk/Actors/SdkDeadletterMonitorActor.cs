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

namespace SportingSolutions.Udapi.Sdk.Actors
{
    // A dead letter handling actor specifically for messages of type "DeadLetter"
    public class SdkDeadletterMonitorActor : ReceiveActor
    {
        private ILogger<SdkDeadletterMonitorActor> Logger { get; }

        public SdkDeadletterMonitorActor(ILogger<SdkDeadletterMonitorActor> log)
        {
            Logger = log;
            Receive<DeadLetter>(dl => HandleDeadletter(dl));
        }

        private void HandleDeadletter(DeadLetter dl)
        {
            Logger.LogDebug($"DeadLetter captured: {dl.Message}, sender: {dl.Sender}, recipient: {dl.Recipient}");
        }
    }
}
