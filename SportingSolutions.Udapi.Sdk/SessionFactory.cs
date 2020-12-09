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

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using ICredentials = SportingSolutions.Udapi.Sdk.Interfaces.ICredentials;

namespace SportingSolutions.Udapi.Sdk
{
    public class SessionFactory
    {
        private ILoggerFactory LoggerFactory { get; }
        private IMemoryCache Cache { get; }
        private ConcurrentDictionary<string, ISession> _sessions { get; } = new ConcurrentDictionary<string, ISession>();

        /// <summary>
        /// DI Class
        /// </summary>
        /// <param name="log"></param>
        public SessionFactory(ILoggerFactory log, IServiceProvider services)
        {
            LoggerFactory = log;
            Cache = services.GetService(typeof(IMemoryCache)) as IMemoryCache; //Optional
            SdkActorSystem.LoggerFactory = log;
        }

        public ISession GetSession(Uri serverUri, ICredentials credentials)
        {
            if (_sessions.TryGetValue(serverUri + credentials.UserName, out ISession session))
            {
                return session;
            } 
            else
            {
                var connectClient = new ConnectClient(LoggerFactory.CreateLogger<ConnectClient>(), serverUri, new Clients.Credentials(credentials.UserName, credentials.Password));
                var newSession = new Session(connectClient, LoggerFactory.CreateLogger<Session>(), Cache);
                if(_sessions.TryAdd(serverUri + credentials.UserName, newSession))
                {
                    return newSession;
                }
            }

            return default;
        }
    }
}
