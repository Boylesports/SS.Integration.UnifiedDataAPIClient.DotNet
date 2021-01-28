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

using System;
using System.Threading.Tasks;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Model;

namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    public interface IResource
    {
        /// <summary>
        ///     Resource's Id
        /// </summary>
        string Id { get; }

        /// <summary>
        ///     Resource's name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// This is set once the clean up is completed
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        ///     Returns the resource's summary
        /// </summary>
        Summary Content { get; }

        /// <summary>
        /// Event raised when the resource succesfully connects 
        /// to the streaming server
        /// </summary>
        event AsyncEventHandler StreamConnected;

        /// <summary>
        /// 
        ///     Event raised when the resource gets disconnected
        ///     from the streaming server. This can happen if
        /// 
        ///     1) StopStreaming() is called
        ///     2) A network error occured and/or the communication channel 
        ///        with the streaming server went down
        /// 
        /// </summary>
        event AsyncEventHandler StreamDisconnected;

        /// <summary>
        ///     Event raised when a new update has arrived.
        /// </summary>
        /// 
        event AsyncEventHandler<StreamEventArgs> StreamEvent;

        /// <summary>
        ///     Retrieves the current resource's snapshot
        /// </summary>
        /// <returns></returns>
        string GetSnapshot();

        /// <summary>
        ///     Retrieves the current resource's snapshot
        /// </summary>
        /// <returns></returns>
        Task<string> GetSnapshotAsync();

        /// <summary>
        ///     Connect the resource to the streaming service.
        /// </summary>
        void StartStreaming();

        void PauseStreaming();

        void UnPauseStreaming();

        void StopStreaming();

        /// <summary>
        /// And async version of <see cref="EventHandler"/>
        /// </summary>
        /// <returns></returns>
        delegate Task AsyncEventHandler(object sender, EventArgs e);

        /// <summary>
        /// And async version of <see cref="EventHandler"/>
        /// </summary>
        /// <returns></returns>
        delegate Task AsyncEventHandler<T>(object sender, T v) where T : EventArgs;
    }
}
