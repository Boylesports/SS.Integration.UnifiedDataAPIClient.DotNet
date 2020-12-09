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
using System.Threading.Tasks;
using RestSharp;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public interface IConnectClient
    {
        IRestResponse Login();
        Task<IRestResponse> LoginAsync();

        IRestResponse<T> Request<T>(Uri uri, Method method) where T : new();
        IRestResponse Request(Uri uri, Method method, object body, string contentType, int timeout);
        IRestResponse Request(Uri uri, Method method);
        IRestResponse<T> Request<T>(Uri uri, Method method, int timeout) where T : new();
        IRestResponse<T> Request<T>(Uri uri, Method method, object body) where T : new();
        IRestResponse<T> Request<T>(Uri uri, Method method, object body, int timeout) where T : new();
        IRestResponse<T> Request<T>(Uri uri, Method method, object body, string contentType) where T : new();
        IRestResponse<T> Request<T>(Uri uri, Method method, object body, string contentType, int timeout) where T : new();

        Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method) where T : new();
        Task<IRestResponse> RequestAsync(Uri uri, Method method, object body, string contentType, int timeout);
        Task<IRestResponse> RequestAsync(Uri uri, Method method);
        Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method, int timeout) where T : new();
        Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method, object body) where T : new();
        Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method, object body, int timeout) where T : new();
        Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method, object body, string contentType) where T : new();
        Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method, object body, string contentType, int timeout) where T : new();
    }
}
