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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;

namespace SportingSolutions.Udapi.Sdk
{
    public class Service : Endpoint, IService
    {
        private const string FeaturesCacheKey = "Features";

        private IMemoryCache Cache { get; }

        internal Service(RestItem restItem, IConnectClient connectClient, ILogger<Endpoint> logger, IMemoryCache cache = default)
            : base(restItem, connectClient, logger)
        {
            Cache = cache;
        }

        public string Name => State.Name;

        public bool IsServiceCacheEnabled => Cache != default;

        public int ServiceCacheInvalidationInterval { get; set; }

        public List<IFeature> GetFeatures()
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.Append("Get all available features - ");

            try
            {
                var featuresList = GetFeaturesList(loggingStringBuilder);
                return featuresList;
            }
            finally
            {
                Logger.LogDebug(loggingStringBuilder.ToString());
            }
        }

        public IFeature GetFeature(string name)
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get feature={0} - ", name);

            try
            {
                var featuresList = GetFeaturesList(loggingStringBuilder);
                var feature = featuresList?.FirstOrDefault(f => f.Name.Equals(name));
                return feature;
            }
            finally
            {
                Logger.LogDebug(loggingStringBuilder.ToString());
            }
        }

        private IEnumerable<RestItem> GetFeaturesListFromApi(StringBuilder loggingStringBuilder)
        {
            loggingStringBuilder.Append("Getting features list from API - ");

            return FindRelationAndFollow(
                "http://api.sportingsolutions.com/rels/features/list",
                "GetFeature Http error",
                loggingStringBuilder);
        }

        private ValueTask<IEnumerable<RestItem>> GetFeaturesListFromApiAsync(StringBuilder loggingStringBuilder)
        {
            loggingStringBuilder.Append("Getting features list from API - ");

            return FindRelationAndFollowAsync(
                "http://api.sportingsolutions.com/rels/features/list",
                "GetFeature Http error",
                loggingStringBuilder);
        }

        private List<IFeature> GetFeaturesList(StringBuilder loggingStringBuilder)
        {
            List<IFeature> GetFeaturesListInternal()
            {
                loggingStringBuilder.AppendLine(
                                "features cache is empty - going to retrieve the list of features from the API now - ");
                var resources = GetFeaturesListFromApi(loggingStringBuilder);
                if (resources == null)
                {
                    Logger.LogWarning($"Return Method=GetFeaturesListFromApi is NULL");
                    return null;
                }

                loggingStringBuilder.AppendLine(
                    $"list with {resources.Count()} features has been cached - ");

                return resources
                        .Select(restItem => new Feature(restItem, ConnectClient, Logger))
                        .Cast<IFeature>()
                        .ToList();
            }

            if (IsServiceCacheEnabled)
            {
                return Cache.GetOrCreate(FeaturesCacheKey, entry =>
                {
                    var ret = GetFeaturesListInternal();
                    //if value returned ok, adjust expire time, 
                    //otherwise leave expire time as-is
                    if (ret != default)
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ServiceCacheInvalidationInterval);
                    }
                    return ret;
                });
            }
            else
            {
                return GetFeaturesListInternal();
            }
        }

        private async Task<List<IFeature>> GetFeatureListAsync(StringBuilder loggingStringBuilder)
        {
            async Task<List<IFeature>> GetFeaturesListInternal()
            {
                loggingStringBuilder.AppendLine(
                                "features cache is empty - going to retrieve the list of features from the API now - ");
                var resources = await GetFeaturesListFromApiAsync(loggingStringBuilder);
                if (resources == null)
                {
                    Logger.LogWarning($"Return Method=GetFeaturesListFromApi is NULL");
                    return null;
                }

                loggingStringBuilder.AppendLine(
                    $"list with {resources.Count()} features has been cached - ");

                return resources
                        .Select(restItem => new Feature(restItem, ConnectClient, Logger))
                        .Cast<IFeature>()
                        .ToList();
            }

            if (IsServiceCacheEnabled)
            {
                return await Cache.GetOrCreateAsync(FeaturesCacheKey, async entry =>
                {
                    var ret = await GetFeaturesListInternal();
                    //if value returned ok, adjust expire time, 
                    //otherwise leave expire time as-is
                    if (ret != default)
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ServiceCacheInvalidationInterval);
                    }
                    return ret;
                });
            } 
            else
            {
                return await GetFeaturesListInternal();
            }
        }

        public Task<List<IFeature>> GetFeaturesAsync()
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.Append("Get all available features - ");

            try
            {
                return GetFeatureListAsync(loggingStringBuilder);
            }
            finally
            {
                Logger.LogDebug(loggingStringBuilder.ToString());
            }
        }

        public async Task<IFeature> GetFeatureAsync(string name)
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get feature={0} - ", name);

            try
            {
                var featuresList = await GetFeatureListAsync(loggingStringBuilder);
                return featuresList?.FirstOrDefault(f => f.Name.Equals(name));
            }
            finally
            {
                Logger.LogDebug(loggingStringBuilder.ToString());
            }
        }
    }
}
