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
    public interface ISession
    {
        /// <summary>
        ///     Returns the service associated to 
        ///     the given name.
        /// 
        ///     It returns null if the service
        ///     doesn't exist
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IService GetService(string name);

        /// <summary>
        ///     Returns the service associated to 
        ///     the given name.
        /// 
        ///     It returns null if the service
        ///     doesn't exist
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Task<IService> GetServiceAsync(string name);

        /// <summary>
        ///     Returns the list of Sporting Solutions services
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IEnumerable<IService> GetServices();

        /// <summary>
        ///     Returns the list of Sporting Solutions services
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Task<IEnumerable<IService>> GetServicesAsync();
    }
}
