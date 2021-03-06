﻿//Copyright 2020 BoyleSports Ltd.
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

using System.Collections.Generic;
using System.Threading.Tasks;

namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    public interface IFeature
    {
        /// <summary>
        ///     Feature's name
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     Returns the list of published resources
        ///     for this feature
        /// </summary>
        /// <returns></returns>
        IEnumerable<IResource> GetResources();

        /// <summary>
        ///     Returns the list of published resources
        ///     for this feature
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<IResource>> GetResourcesAsync();

        /// <summary>
        ///     Returns the resource associated
        ///     to the given name.
        /// 
        ///     It returns null if the the 
        ///     resource doesn't exist
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IResource GetResource(string name);

        /// <summary>
        ///     Returns the resource associated
        ///     to the given name.
        /// 
        ///     It returns null if the the 
        ///     resource doesn't exist
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Task<IResource> GetResourceAsync(string name);
    }
}
