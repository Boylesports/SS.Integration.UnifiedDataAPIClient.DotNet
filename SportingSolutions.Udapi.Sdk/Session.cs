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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Exceptions;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace SportingSolutions.Udapi.Sdk
{
    public class Session : Endpoint, ISession
    {
        private IMemoryCache Cache { get; }

        internal Session(IConnectClient connectClient, ILogger<Endpoint> logger, IMemoryCache cache = default)
            : base(connectClient, logger)
        {
            Cache = cache;
        }

        public IEnumerable<IService> GetServices()
        {
            Logger.LogDebug("Get all available services...");
            var links = GetRoot();
            return links?.Select(serviceRestItem => new Service(serviceRestItem, ConnectClient, Logger))?.Cast<IService>() ?? Enumerable.Empty<IService>();
        }

        public IService GetService(string name)
        {
            Logger.LogDebug("Get service={0}", name);
            var links = GetRoot();
            return links?.Select(serviceRestItem => new Service(serviceRestItem, ConnectClient, Logger))?.FirstOrDefault(service => service.Name == name);
        }

        private IEnumerable<RestItem> GetRoot()
        {
            var stopwatch = new Stopwatch();
            var messageStringBuilder = new StringBuilder("GetRoot request...");
            try
            {
                stopwatch.Start();

                var getRootResponse = ConnectClient.Login();
                messageStringBuilder.AppendFormat("took {0}ms", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();

                if (getRootResponse.ErrorException != null || getRootResponse.Content == null)
                {
                    RestErrorHelper.LogRestError(Logger, getRootResponse, "GetRoot HTTP error");
                    throw new Exception("Error calling GetRoot", getRootResponse.ErrorException);
                }

                if (getRootResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new NotAuthenticatedException("Username or password are incorrect");
                }

                if (getRootResponse.Content != null)
                    return getRootResponse.Content.FromJson<List<RestItem>>();
            }
            catch (NotAuthenticatedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("GetRoot exception", ex);
                throw;
            }
            finally
            {
                Logger.LogError(messageStringBuilder.ToString());
                stopwatch.Stop();
            }

            return null;
        }

        private async Task<IEnumerable<RestItem>> GetRootAsync()
        {
            var stopwatch = new Stopwatch();
            var messageStringBuilder = new StringBuilder("GetRoot request...");
            try
            {
                stopwatch.Start();

                var getRootResponse = await ConnectClient.LoginAsync();
                messageStringBuilder.AppendFormat("took {0}ms", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();

                if (getRootResponse.ErrorException != null || getRootResponse.Content == null)
                {
                    RestErrorHelper.LogRestError(Logger, getRootResponse, "GetRoot HTTP error");
                    throw new Exception("Error calling GetRoot", getRootResponse.ErrorException);
                }

                if (getRootResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new NotAuthenticatedException("Username or password are incorrect");
                }

                if (getRootResponse.Content != null)
                    return getRootResponse.Content.FromJson<List<RestItem>>();
            }
            catch (NotAuthenticatedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("GetRoot exception", ex);
                throw;
            }
            finally
            {
                Logger.LogError(messageStringBuilder.ToString());
                stopwatch.Stop();
            }

            return null;
        }

        public async Task<IService> GetServiceAsync(string name)
        {
            Logger.LogDebug("Get service={0}", name);
            var links = await GetRootAsync();
            return links?.Select(serviceRestItem => new Service(serviceRestItem, ConnectClient, Logger))?.FirstOrDefault(service => service.Name == name);
        }

        public async Task<IEnumerable<IService>> GetServicesAsync()
        {
            Logger.LogDebug("Get all available services...");
            var links = await GetRootAsync();
            return links?.Select(serviceRestItem => new Service(serviceRestItem, ConnectClient, Logger))?.Cast<IService>() ?? Enumerable.Empty<IService>();
        }
    }
}
