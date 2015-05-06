﻿//Copyright 2012 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    /// <summary>
    ///     Simple StreamController that mocks out the AMPQ 
    ///     stream controller
    /// </summary>
    internal class MockedStreamController : StreamController
    {
        private MockedStreamController(IDispatcher dispatcher)
            : base(dispatcher)
        {
        }

        public static void Register(IDispatcher dispatcher)
        {
            Instance = new MockedStreamController(dispatcher);
        }

        public IEchoController EchoManager
        {
            get
            {
                if (Dispatcher is UpdateDispatcher)
                    return ((UpdateDispatcher)Dispatcher).EchoManager;

                return null;
            }
        }

        public void ForceCloseConnection()
        {
            base.CloseConnection();
        }
        
        protected override void EstablishConnection(ConnectionFactory factory)
        {
            CanPerformChannelOperations = true;
            OnConnectionStatusChanged(ConnectionState.CONNECTED);
        }

        protected override void AddConsumerToQueue(IConsumer consumer)
        {
            new MockedStreamSubscriber(consumer, Dispatcher).StartConsuming("");
        }

        protected override void RemoveConsumerFromQueue(IConsumer consumer)
        {
            var s = Dispatcher.GetSubscriber(consumer.Id);
            if(s == null)
                throw new Exception("Subscriber with Id=" + consumer.Id + " not found");

            s.StopConsuming();
        }
    }
}
