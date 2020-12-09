//Copyright 2020 BoyleSports Ltd.
//Copyright 2017 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace SportingSolutions.Udapi.Sdk
{
    public class Feature : Endpoint, IFeature
    {
        private IMemoryCache Cache { get; }
        public string Name => State.Name;

        internal Feature(RestItem restItem, IConnectClient connectClient, ILogger<Endpoint> logger, IMemoryCache cache = default)
            : base(restItem, connectClient, logger)
        {
            Cache = cache;
            Logger.LogDebug("Instantiated feature={0}", restItem.Name);
        }

        public IEnumerable<IResource> GetResources()
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.Append($"Get all available resources for feature={Name} - ");

            try
            {
                return GetResourcesList(Name, loggingStringBuilder);
            }
            finally
            {
                Logger.LogDebug(loggingStringBuilder.ToString());
            }
        }

        public IResource GetResource(string name)
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat($"Get resource={name} for feature={Name} - ");

            try
            {
                var resourcesList = GetResourcesList(Name, loggingStringBuilder);
                return resourcesList?.FirstOrDefault(r => r.Name.Equals(name));
            }
            finally
            {
                Logger.LogDebug(loggingStringBuilder.ToString());
            }
        }

        private IEnumerable<RestItem> GetResourcesListFromApi(StringBuilder loggingStringBuilder)
        {
            loggingStringBuilder.Append("Getting resources list from API - ");

            return FindRelationAndFollow(
                "http://api.sportingsolutions.com/rels/resources/list",
                "GetResources HTTP error",
                loggingStringBuilder);
        }

        private IEnumerable<IResource> GetResourcesList(string sport, StringBuilder loggingStringBuilder)
        {
            IEnumerable<IResource> GetResourceListInternal()
            {
                loggingStringBuilder.AppendLine(
                                $"resources cache is empty for feature={Name} - going to retrieve list of resources from the API now - ");
                var resources = GetResourcesListFromApi(loggingStringBuilder);
                if (resources == null)
                {
                    Logger.LogWarning($"Return Method=GetResourcesListFromApi is NULL");
                    return null;
                }
                else
                {
                    loggingStringBuilder.AppendLine(
                        $"list with {resources.Count()} resources has been cached for feature={Name} - ");
                    return resources
                        .Select(restItem => new Resource(restItem, ConnectClient, Logger))
                        .Cast<IResource>();
                }
            }

            if (Cache != default)
            {
                return Cache.GetOrCreate($"{sport}_Resource", entry =>
                {
                    var res = GetResourceListInternal();
                    if (res != default)
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);
                    }
                    return res;
                });
            }
            return GetResourceListInternal();
        }

        private ValueTask<IEnumerable<RestItem>> GetResourcesListFromApiAsync(StringBuilder loggingStringBuilder)
        {
            loggingStringBuilder.Append("Getting resources list from API - ");

            return FindRelationAndFollowAsync(
                "http://api.sportingsolutions.com/rels/resources/list",
                "GetResources HTTP error",
                loggingStringBuilder);
        }

        private async Task<IEnumerable<IResource>> GetResourcesListAsync(string sport, StringBuilder loggingStringBuilder)
        {
            async Task<IEnumerable<IResource>> GetResourceListInternal()
            {
                loggingStringBuilder.AppendLine(
                                $"resources cache is empty for feature={Name} - going to retrieve list of resources from the API now - ");
                var resources = await GetResourcesListFromApiAsync(loggingStringBuilder);
                if (resources == null)
                {
                    Logger.LogWarning($"Return Method=GetResourcesListFromApi is NULL");
                    return null;
                }
                else
                {
                    loggingStringBuilder.AppendLine(
                        $"list with {resources.Count()} resources has been cached for feature={Name} - ");
                    return resources
                        .Select(restItem => new Resource(restItem, ConnectClient, Logger))
                        .Cast<IResource>();
                }
            }

            if (Cache != default)
            {
                return await Cache.GetOrCreateAsync($"{sport}_Resource", async entry =>
                {
                    var res = await GetResourceListInternal();
                    if (res != default)
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);
                    }
                    return res;
                });
            }
            return await GetResourceListInternal();
        }

        public Task<IEnumerable<IResource>> GetResourcesAsync()
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.Append($"Get all available resources for feature={Name} - ");

            try
            {
                return GetResourcesListAsync(Name, loggingStringBuilder);
            }
            finally
            {
                Logger.LogDebug(loggingStringBuilder.ToString());
            }
        }

        public async Task<IResource> GetResourceAsync(string name)
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat($"Get resource={name} for feature={Name} - ");

            try
            {
                var resourcesList = await GetResourcesListAsync(Name, loggingStringBuilder);
                return resourcesList?.FirstOrDefault(r => r.Name.Equals(name));
            }
            finally
            {
                Logger.LogDebug(loggingStringBuilder.ToString());
            }
        }
    }
}
