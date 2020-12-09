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

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using SportingSolutions.Udapi.Sdk.Exceptions;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public partial class ConnectClient : IConnectClient
    {
        private async Task<bool> AuthenticateAsync(IRestResponse response)
        {
            var authenticated = false;
            var restItems = response.Content.FromJson<List<RestItem>>();

            var loginUri = FindLoginUri(restItems);

            var loginRequest = CreateRequest(loginUri, Method.POST, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);

            loginRequest.AddHeader(XAuthUser, Credentials.ApiUser);
            loginRequest.AddHeader(XAuthKey, Credentials.ApiKey);

            response = await CreateClient().ExecuteAsync<List<RestItem>>(loginRequest);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                SessionToken = response.Headers.FirstOrDefault(h => h.Name == XAuthToken)?.Value as string;
                authenticated = !string.IsNullOrEmpty(SessionToken);
            }
            return authenticated;
        }


        public Task<IRestResponse> LoginAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method) where T : new()
        {
            throw new NotImplementedException();
        }

        public async Task<IRestResponse> RequestAsync(Uri uri, Method method, object body, string contentType, int timeout)
        {
            var connectionClosedRetryCounter = 0;
            IRestResponse response = null;
        retry:
            while (connectionClosedRetryCounter < DEFAULT_REQUEST_RETRY_ATTEMPTS)
            {
                var request = CreateRequest(uri, method, body, contentType, timeout);

                var client = CreateClient();
                var oldAuth = client.DefaultParameters.FirstOrDefault(x => x.Name == XAuthToken)?.Value as string;

                response = await client.ExecuteAsync(request);

                if (response.ResponseStatus == ResponseStatus.Error &&
                    response.ErrorException is WebException &&
                    ((WebException)response.ErrorException).Status == WebExceptionStatus.KeepAliveFailure)
                {
                    //retry
                    connectionClosedRetryCounter++;
                    Logger.LogWarning("Request failed due underlying connection closed URL={0}", uri);
                    goto retry;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    RestErrorHelper.LogRestWarn(Logger, response, string.Format("Unauthenticated when using authToken={0}", SessionToken));

                    var authenticated = false;
                    try
                    {
                        await SessionLock.WaitAsync();
                        if (SessionToken == null || oldAuth == SessionToken)
                        {
                            authenticated = await AuthenticateAsync(response);
                        }
                        else
                        {
                            authenticated = true;
                        }
                    }
                    finally
                    {
                        SessionLock.Release();
                    }

                    if (authenticated)
                    {
                        connectionClosedRetryCounter++;
                        goto retry;
                    }
                    else
                    {
                        throw new NotAuthenticatedException(string.Format("Not Authenticated for url={0}", uri));
                    }
                }

                connectionClosedRetryCounter = DEFAULT_REQUEST_RETRY_ATTEMPTS;
            }

            return response;
        }

        public Task<IRestResponse> RequestAsync(Uri uri, Method method)
        {
            throw new NotImplementedException();
        }

        public Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method, int timeout) where T : new()
        {
            throw new NotImplementedException();
        }

        public Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method, object body) where T : new()
        {
            throw new NotImplementedException();
        }

        public Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method, object body, int timeout) where T : new()
        {
            throw new NotImplementedException();
        }

        public Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method, object body, string contentType) where T : new()
        {
            throw new NotImplementedException();
        }

        public async Task<IRestResponse<T>> RequestAsync<T>(Uri uri, Method method, object body, string contentType, int timeout) where T : new()
        {
            var restResponse = await RequestAsync(uri, method, body, contentType, timeout);
            var response = new RestResponse<T>
            {
                Request = restResponse.Request,
                StatusCode = restResponse.StatusCode,
                Content = restResponse.Content
            };

            if (restResponse.ErrorException != null)
            {
                response.ErrorException = restResponse.ErrorException;
            }
            else
            {
                try
                {
                    response.Data = restResponse.Content.FromJson<T>();
                }
                catch (JsonSerializationException ex)
                {
                    throw new JsonSerializationException($"Serialization exception from JSON={restResponse.Content}",
                        ex);
                }
                catch (Exception ex)
                {
                    response.ErrorException = ex;
                }
            }

            return response;
        }
    }
}
