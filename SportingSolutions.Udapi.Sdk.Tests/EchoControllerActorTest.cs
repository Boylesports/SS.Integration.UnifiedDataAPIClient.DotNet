﻿using System;
using Akka.TestKit;
using Akka.TestKit.NUnit;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    public class EchoControllerActorTest : SdkTestKit
    {

        //private EchoControllerActor testing;

        private const string id1 = "Id1";
        private const string id2 = "Id2";
        private const string id3 = "Id3";

        [SetUp]
        public void Initialise()
        {
            ((Configuration)UDAPI.Configuration).UseEchos = true;
            ((Configuration)UDAPI.Configuration).EchoWaitInterval = int.MaxValue;
            SdkActorSystem.Init(Sys, false);
            
            
        }

        private EchoControllerActor GetEchoControllerActorWith1Consumer(string consumerId)
        {
            var testing = new EchoControllerActor();

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);
            
            testing.AddConsumer(subscriber.Object);

            return testing;
        }

        

        private void AddConsumer(EchoControllerActor actor, string consumerId)
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);

            actor.AddConsumer(subscriber.Object);

        }

        private TestActorRef<MockedEchoControllerActor> GetMockedEchoControllerActorWith1Consumer(string consumerId)
        {
            var mock = ActorOfAsTestActorRef<MockedEchoControllerActor>(() => new MockedEchoControllerActor(), MockedEchoControllerActor.ActorName);
            
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);
            
            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);

            mock.UnderlyingActor.AddConsumer(subscriber.Object);

            return mock;
        }

        private void AddConsumer(TestActorRef<MockedEchoControllerActor> actor, string consumerId)
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);

            actor.UnderlyingActor.AddConsumer(subscriber.Object);

        }


        [Test]
        public void AddConsumerWithNullTest()
        {
            var testing = new EchoControllerActor();
            testing.AddConsumer(null);
            testing.ConsumerCount.ShouldBeEquivalentTo(0);

        }

        [Test]
        public void Add1ConsumerTest()
        {
            var testing = new EchoControllerActor();
            
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);
            
            testing.AddConsumer(subscriber.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);
            

        }

        [Test]
        public void AddSameConsumerTest()
        {
            var testing = GetEchoControllerActorWith1Consumer(id1);
            AddConsumer(testing, id1);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);
        }

        [Test]
        public void RemoveConsumerPositiveTest()
        {
            var testing = new EchoControllerActor();

            
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);



            testing.AddConsumer(subscriber.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);

            testing.RemoveConsumer(subscriber.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(0);

        }

        [Test]
        public void RemoveConsumerNegativeTest()
        {
            var testing = new EchoControllerActor();

            Mock<IConsumer> consumer1 = new Mock<IConsumer>();
            consumer1.Setup(x => x.Id).Returns(id1);

            Mock<IConsumer> consumer2 = new Mock<IConsumer>();
            consumer2.Setup(x => x.Id).Returns(id2);


            Mock<IStreamSubscriber> subscriber1 = new Mock<IStreamSubscriber>();
            subscriber1.Setup(x => x.Consumer).Returns(consumer1.Object);

            Mock<IStreamSubscriber> subscriber2 = new Mock<IStreamSubscriber>();
            subscriber2.Setup(x => x.Consumer).Returns(consumer2.Object);


            testing.AddConsumer(subscriber1.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);
            testing.RemoveConsumer(subscriber2.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);

        }

        [Test]
        public void GetDefaultEchosCountDownTest()
        {
            var testing = new EchoControllerActor();


            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);
            
            testing.AddConsumer(subscriber.Object);
            testing.GetEchosCountDown(id1).ShouldBeEquivalentTo(UDAPI.Configuration.MissedEchos);
        }

        [Test]
        public void CheckEchosDecteaseEchosCountDownTest()
        {
            var testing = GetMockedEchoControllerActorWith1Consumer(id1);
            var message = new EchoControllerActor.SendEchoMessage();

            testing.Tell(message);
            AwaitAssert(() =>
                {
                    testing.UnderlyingActor.GetEchosCountDown(id1).ShouldBeEquivalentTo(UDAPI.Configuration.MissedEchos - 1);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        public void CheckEchosUntillAllSubscribersClearTest()
        {
            var testing = GetMockedEchoControllerActorWith1Consumer(id1);
            AddConsumer(testing, id2);

            AwaitAssert(() =>
                {
                    testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(2);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            var message = new EchoControllerActor.SendEchoMessage();
            for (int i = 0; i < UDAPI.Configuration.MissedEchos; i++)
            {
                testing.Tell(message);
            }

            AwaitAssert(() =>
                {
                    testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(0);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        public void CheckEchosWithProcessEchoClearTest()
        {
            var testing = GetMockedEchoControllerActorWith1Consumer(id1);
            AddConsumer(testing, id2);

            AwaitAssert(() =>
                {
                    testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(2);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            var sendEchoMessage = new EchoControllerActor.SendEchoMessage();
            var echoMessage = new EchoMessage() {Id = id2};

            testing.Tell(sendEchoMessage);
            testing.Tell(echoMessage);
            for (int i = 0; i < UDAPI.Configuration.MissedEchos - 1; i++)
            {
                testing.Tell(sendEchoMessage);
            }

            AwaitAssert(() =>
                {
                    testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);
                    testing.UnderlyingActor.GetEchosCountDown(id2).ShouldBeEquivalentTo(1);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }


        [Test]
        public void SendEchoCallTest()
        {
            var testing = ActorOfAsTestActorRef<MockedEchoControllerActor>(() => new MockedEchoControllerActor(), MockedEchoControllerActor.ActorName);

            Mock<IConsumer> consumer1 = new Mock<IConsumer>();
            consumer1.Setup(x => x.Id).Returns(id1);
            int sendEchoCallCaount1 = 0;
            consumer1.Setup(x => x.SendEcho()).Callback(() => sendEchoCallCaount1++);

            Mock<IConsumer> consumer2 = new Mock<IConsumer>();
            consumer2.Setup(x => x.Id).Returns(id2);
            int sendEchoCallCaount2 = 0;
            consumer2.Setup(x => x.SendEcho()).Callback(() => sendEchoCallCaount2++);

            Mock<IStreamSubscriber> subscriber1 = new Mock<IStreamSubscriber>();
            subscriber1.Setup(x => x.Consumer).Returns(consumer1.Object);
            Mock<IStreamSubscriber> subscriber2 = new Mock<IStreamSubscriber>();
            subscriber2.Setup(x => x.Consumer).Returns(consumer2.Object);

            testing.UnderlyingActor.AddConsumer(subscriber1.Object);
            testing.UnderlyingActor.AddConsumer(subscriber2.Object);

            AwaitAssert(() =>
                {
                    testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(2);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            var sendEchoMessage = new EchoControllerActor.SendEchoMessage();
            var echoMessage = new EchoMessage() { Id = id2 };

            testing.Tell(sendEchoMessage);
            testing.Tell(echoMessage);
            for (int i = 0; i < UDAPI.Configuration.MissedEchos - 1; i++)
            {
                testing.Tell(sendEchoMessage);
            }

            AwaitAssert(() =>
                {
                    sendEchoCallCaount1.ShouldBeEquivalentTo(UDAPI.Configuration.MissedEchos - 1);
                    sendEchoCallCaount2.ShouldBeEquivalentTo(1);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

    }
}
