﻿//Copyright 2020 BoyleSports Ltd.
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

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using SportingSolutions.Udapi.Sdk.Model.Message;
using static SportingSolutions.Udapi.Sdk.Interfaces.IResource;

namespace SportingSolutions.Udapi.Sdk
{
    public class Resource : Endpoint, IResource, IDisposable, IConsumer
    {
        private const int DEFAULT_ECHO_INTERVAL_MS = 10000;
        private const int DEFAULT_ECHO_MAX_DELAY_MS = 3000;

        public event EventHandler Tick;

        public event AsyncEventHandler StreamConnected;
        public event AsyncEventHandler StreamDisconnected;
        public event AsyncEventHandler<StreamEventArgs> StreamEvent;

        private readonly ManualResetEvent _pauseStream;
        private string _virtualHost;

        public Resource(RestItem restItem, IConnectClient client, ILogger<Endpoint> logger)
            : base(restItem, client, logger)
        {
            Logger.LogDebug("Instantiated fixtureName=\"{0}\" fixtureId=\"{1}\"", restItem.Name, Id);

            _pauseStream = new ManualResetEvent(true);
        }

        public string Id
        {
            get { return State.Content.Id; }
        }

        public string Name
        {
            get { return State.Name; }
        }

        public Summary Content
        {
            get { return State.Content; }
        }

        public bool IsDisposed { get; internal set; }

        public string GetSnapshot()
        {

            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get snapshot for fixtureName=\"{0}\" fixtureId={1} - ", Name, Id);

            var result = FindRelationAndFollowAsString("http://api.sportingsolutions.com/rels/snapshot", "GetSnapshot HTTP error", loggingStringBuilder);
            Logger.LogDebug(loggingStringBuilder.ToString());
            return result;
        }

        public async Task<string> GetSnapshotAsync()
        {

            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get snapshot for fixtureName=\"{0}\" fixtureId={1} - ", Name, Id);

            var result = await FindRelationAndFollowAsStringAsync("http://api.sportingsolutions.com/rels/snapshot", "GetSnapshot HTTP error", loggingStringBuilder);
            Logger.LogDebug(loggingStringBuilder.ToString());
            return result;
        }


        public void StartStreaming()
        {
            SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new NewConsumerMessage { Consumer = this });
            Logger.LogDebug("Streaming request queued for fixtureName=\"{0}\" fixtureId=\"{1}\"", Name, Id);
        }

        public void PauseStreaming()
        {
            Logger.LogDebug("Streaming paused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            _pauseStream.Reset();
        }

        public void UnPauseStreaming()
        {
            Logger.LogDebug("Streaming unpaused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            _pauseStream.Set();
        }

        public void StopStreaming()
        {
            //StreamController.Instance.RemoveConsumer(this);
            Logger.LogDebug("Streaming stopped for fixtureName=\"{0}\" fixtureId=\"{1}\"", Name, Id);

            SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new RemoveConsumerMessage() { Consumer = this });
        }

        public void Dispose()
        {
            StopStreaming();
            IsDisposed = true;
        }

        public virtual async Task OnStreamDisconnected()
        {
            Logger.LogDebug("Resource \"{0}\" OnStreamDisconnected()", Id);

            if (StreamDisconnected != null)
                await StreamDisconnected(this, EventArgs.Empty);
        }

        public virtual async Task OnStreamConnected()
        {
            Logger.LogDebug("Resource \"{0}\" OnStreamConnected()", Id);

            if (StreamConnected != null)
                await StreamConnected(this, EventArgs.Empty);
        }

        public virtual async Task OnStreamEvent(StreamEventArgs e)
        {
            Logger.LogDebug("Resource \"{0}\" OnStreamEvent()", Id);

            if (StreamEvent != null)
                await StreamEvent(this, e);
        }

        public QueueDetails GetQueueDetails()
        {

            var loggingStringBuilder = new StringBuilder();
            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/stream/amqp", "GetAmqpStream HTTP error", loggingStringBuilder);

            if (restItems == null)
            {
                return null;
            }
            var amqpLink = restItems.SelectMany(restItem => restItem.Links).First(restLink => restLink.Relation == "amqp");

            var amqpUri = new Uri(amqpLink.Href);

            var queueDetails = new QueueDetails { Host = amqpUri.Host };

            var userInfo = amqpUri.UserInfo;
            userInfo = HttpUtility.UrlDecode(userInfo);
            if (!String.IsNullOrEmpty(userInfo))
            {
                var userPass = userInfo.Split(':');
                if (userPass.Length > 2)
                {
                    throw new ArgumentException(string.Format("Bad user info in AMQP URI: {0}", userInfo));
                }
                queueDetails.UserName = userPass[0];
                if (userPass.Length == 2)
                {
                    queueDetails.Password = userPass[1];
                }
            }

            var path = amqpUri.AbsolutePath;
            if (!String.IsNullOrEmpty(path))
            {
                queueDetails.Name = path.Substring(path.IndexOf('/', 1) + 1);
                var virtualHost = path.Substring(1, path.IndexOf('/', 1) - 1);

                queueDetails.VirtualHost = virtualHost;
                _virtualHost = queueDetails.VirtualHost;
            }

            var port = amqpUri.Port;
            if (port != -1)
            {
                queueDetails.Port = port;
            }

            return queueDetails;
        }

        public async Task SendEcho()
        {
            if (string.IsNullOrEmpty(_virtualHost))
                throw new Exception("virtualHost is not defined");

            var link = State.Links.First(restLink => restLink.Relation == "http://api.sportingsolutions.com/rels/stream/batchecho");
            var echouri = new Uri(link.Href);

            var streamEcho = new StreamEcho
            {
                Host = _virtualHost,
                Message = Guid.NewGuid() + ";" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            var response = await ConnectClient.RequestAsync(echouri, RestSharp.Method.POST, streamEcho, UDAPI.Configuration.ContentType, 3000);
            if (response.ErrorException != null || response.Content == null)
            {
                RestErrorHelper.LogRestError(Logger, response, "Error sending echo request");
                throw new Exception(string.Format("Error calling {0}", echouri), response.ErrorException);
            }
        }
    }
}
