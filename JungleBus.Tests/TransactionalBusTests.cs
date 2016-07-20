using System;
using System.Collections.Generic;
using System.Transactions;
using JungleBus.Interfaces.Serialization;
using JungleBus.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace JungleBus.Tests
{
    [TestClass]
    public class TransactionalBusTests
    {
        private const string SerializedMessage = "Serialized message";
        private TransactionalBus _busUnderTest;
        private Mock<IMessagePublisher> _messagePublisher;
        private Mock<IMessageSerializer> _messageSerializer;
        private Mock<IMessageQueue> _messageQueue;

        [TestInitialize]
        public void TestInitialize()
        {
            _messagePublisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
            _messagePublisher.Setup(x => x.Publish(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Dictionary<string, string>>()));
            _messagePublisher.Setup(x => x.Send(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<IMessageQueue>(), It.IsAny<Dictionary<string, string>>()));

            _messageSerializer = new Mock<IMessageSerializer>(MockBehavior.Strict);
            _messageSerializer.Setup(x => x.Serialize(It.IsAny<object>())).Returns(SerializedMessage);

            _messageQueue = new Mock<IMessageQueue>(MockBehavior.Strict);
            _messageQueue.SetupGet(x => x.QueueAddress).Returns("QueueName");

            _busUnderTest = new TransactionalBus(_messagePublisher.Object, _messageSerializer.Object, _messageQueue.Object);
        }

        #region PUBLISH MESSAGE TESTS

        [TestMethod]
        public void TransactionalBusTests_NoTransaction_Publishes_Message()
        {
            _busUnderTest.Publish(new TestMessage());
            _messageSerializer.Verify(x => x.Serialize(It.IsAny<TestMessage>()), Times.Once());
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.Is<Dictionary<string, string>>(m => m["sender"] == "QueueName")), Times.Once());
        }

        [TestMethod]
        public void TransactionalBusTests_NoInputQueue_Publishes_Message()
        {
            _busUnderTest = new TransactionalBus(_messagePublisher.Object, _messageSerializer.Object, null);
            _busUnderTest.Publish(new TestMessage());
            _messageSerializer.Verify(x => x.Serialize(It.IsAny<TestMessage>()), Times.Once());
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.Is<Dictionary<string, string>>(m => !m.ContainsKey("sender"))), Times.Once());
        }

        [TestMethod]
        public void TransactionalBusTests_NoTransaction_Publishes_Builds_Message()
        {
            _busUnderTest.Publish<TestMessage>(x => x.Name = "Name");
            _messageSerializer.Verify(x => x.Serialize(It.Is<TestMessage>(m => m.Name == "Name")), Times.Once());
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.Is<Dictionary<string, string>>(m => m["sender"] == "QueueName")), Times.Once());
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_Publishes_Message_Build_OnCommit()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.Publish<TestMessage>(x => x.Name = "Name1");
                _busUnderTest.Publish<TestMessage>(x => x.Name = "Name2");
                scope.Complete();
            }
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name1")), Times.Exactly(1));
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name2")), Times.Exactly(1));
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.Is<Dictionary<string, string>>(m => m["sender"] == "QueueName")), Times.Exactly(2));
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_Publishes_Message_Build_OnRollback()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.Publish<TestMessage>(x => x.Name = "Name1");
                _busUnderTest.Publish<TestMessage>(x => x.Name = "Name2");
                scope.Dispose();
            }
            _messageSerializer.Verify(x => x.Serialize(It.IsAny<object>()), Times.Never());
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), null), Times.Never());
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_Publishes_Message_Build_OnRollback_Then_Commit()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.Publish<TestMessage>(x => x.Name = "Name1");
                _busUnderTest.Publish<TestMessage>(x => x.Name = "Name2");
                scope.Dispose();
            }

            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.Publish<TestMessage>(x => x.Name = "Name11");
                _busUnderTest.Publish<TestMessage>(x => x.Name = "Name21");
                scope.Complete();
            }
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name11")), Times.Exactly(1));
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name21")), Times.Exactly(1));
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.Is<Dictionary<string, string>>(m => m["sender"] == "QueueName")), Times.Exactly(2));
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_Publishes_Message_OnCommit()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.Publish(new TestMessage() { Name = "Name1" });
                _busUnderTest.Publish(new TestMessage() { Name = "Name2" });
                scope.Complete();
            }
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name1")), Times.Exactly(1));
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name2")), Times.Exactly(1));
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.Is<Dictionary<string, string>>(m => m["sender"] == "QueueName")), Times.Exactly(2));
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_Publishes_Message_OnRollback()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.Publish(new TestMessage() { Name = "Name1" });
                _busUnderTest.Publish(new TestMessage() { Name = "Name2" });
                scope.Dispose();
            }
            _messageSerializer.Verify(x => x.Serialize(It.IsAny<object>()), Times.Never());
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), null), Times.Never());
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_Publishes_Message_OnRollback_Then_Commit()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.Publish(new TestMessage() { Name = "Name1" });
                _busUnderTest.Publish(new TestMessage() { Name = "Name2" });
                scope.Dispose();
            }

            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.Publish(new TestMessage() { Name = "Name11" });
                _busUnderTest.Publish(new TestMessage() { Name = "Name21" });
                scope.Complete();
            }
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name11")), Times.Exactly(1));
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name21")), Times.Exactly(1));
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.Is<Dictionary<string, string>>(m => m["sender"] == "QueueName")), Times.Exactly(2));
        }

        #endregion PUBLISH MESSAGE TESTS

        #region PUBLISH LOCAL TESTS

        [TestMethod]
        public void TransactionalBusTests_NoTransaction_PublishLocal_Message()
        {
            _busUnderTest.PublishLocal(new TestMessage());
            _messageSerializer.Verify(x => x.Serialize(It.IsAny<TestMessage>()), Times.Once());
            _messagePublisher.Verify(x => x.Send(SerializedMessage, typeof(TestMessage), It.IsAny<IMessageQueue>(), It.IsAny<Dictionary<string, string>>()), Times.Once());
            _messagePublisher.Verify(x => x.Publish(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Dictionary<string, string>>()), Times.Never());
        }

        [TestMethod]
        public void TransactionalBusTests_NoTransaction_PublishLocal_Builds_Message()
        {
            _busUnderTest.PublishLocal<TestMessage>(x => x.Name = "Name");
            _messageSerializer.Verify(x => x.Serialize(It.Is<TestMessage>(m => m.Name == "Name")), Times.Once());
            _messagePublisher.Verify(x => x.Send(SerializedMessage, typeof(TestMessage), It.IsAny<IMessageQueue>(), It.IsAny<Dictionary<string, string>>()), Times.Once());
            _messagePublisher.Verify(x => x.Publish(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Dictionary<string, string>>()), Times.Never());
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_PublishLocal_Message_Build_OnCommit()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.PublishLocal<TestMessage>(x => x.Name = "Name1");
                _busUnderTest.PublishLocal<TestMessage>(x => x.Name = "Name2");
                scope.Complete();
            }

            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name1")), Times.Exactly(1));
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name2")), Times.Exactly(1));
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.IsAny<Dictionary<string, string>>()), Times.Never());
            _messagePublisher.Verify(x => x.Send(SerializedMessage, typeof(TestMessage), It.IsAny<IMessageQueue>(), It.IsAny<Dictionary<string, string>>()), Times.Exactly(2));
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_PublishLocal_Message_Build_OnRollback()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.PublishLocal<TestMessage>(x => x.Name = "Name1");
                _busUnderTest.PublishLocal<TestMessage>(x => x.Name = "Name2");
                scope.Dispose();
            }
            _messageSerializer.Verify(x => x.Serialize(It.IsAny<object>()), Times.Never());
            _messagePublisher.Verify(x => x.Publish(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Dictionary<string, string>>()), Times.Never());
            _messagePublisher.Verify(x => x.Send(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<IMessageQueue>(), It.IsAny<Dictionary<string, string>>()), Times.Never());
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_PublishLocal_Message_Build_OnRollback_Then_Commit()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.PublishLocal<TestMessage>(x => x.Name = "Name1");
                _busUnderTest.PublishLocal<TestMessage>(x => x.Name = "Name2");
                scope.Dispose();
            }

            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.PublishLocal<TestMessage>(x => x.Name = "Name11");
                _busUnderTest.PublishLocal<TestMessage>(x => x.Name = "Name21");
                scope.Complete();
            }

            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name11")), Times.Exactly(1));
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name21")), Times.Exactly(1));
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.IsAny<Dictionary<string, string>>()), Times.Never());
            _messagePublisher.Verify(x => x.Send(SerializedMessage, typeof(TestMessage), It.IsAny<IMessageQueue>(), It.IsAny<Dictionary<string, string>>()), Times.Exactly(2));
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_PublishLocal_Message_OnCommit()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.PublishLocal(new TestMessage() { Name = "Name1" });
                _busUnderTest.PublishLocal(new TestMessage() { Name = "Name2" });
                scope.Complete();
            }

            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name1")), Times.Once());
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name2")), Times.Once());
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.IsAny<Dictionary<string, string>>()), Times.Never());
            _messagePublisher.Verify(x => x.Send(SerializedMessage, typeof(TestMessage), It.IsAny<IMessageQueue>(), It.IsAny<Dictionary<string, string>>()), Times.Exactly(2));
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_PublishLocal_Message_OnRollback()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.PublishLocal(new TestMessage() { Name = "Name1" });
                _busUnderTest.PublishLocal(new TestMessage() { Name = "Name2" });
                scope.Dispose();
            }

            _messageSerializer.Verify(x => x.Serialize(It.IsAny<object>()), Times.Never());
            _messagePublisher.Verify(x => x.Publish(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Dictionary<string, string>>()), Times.Never());
            _messagePublisher.Verify(x => x.Send(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<IMessageQueue>(), It.IsAny<Dictionary<string, string>>()), Times.Never());
        }

        [TestMethod]
        public void TransactionalBusTests_Transaction_PublishLocal_Message_OnRollback_Then_Commit()
        {
            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.PublishLocal(new TestMessage() { Name = "Name1" });
                _busUnderTest.PublishLocal(new TestMessage() { Name = "Name2" });
                scope.Dispose();
            }

            using (TransactionScope scope = new TransactionScope())
            {
                _busUnderTest.PublishLocal(new TestMessage() { Name = "Name11" });
                _busUnderTest.PublishLocal(new TestMessage() { Name = "Name21" });
                scope.Complete();
            }

            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name11")), Times.Exactly(1));
            _messageSerializer.Verify(x => x.Serialize(It.Is<object>(o => (o is TestMessage) && (o as TestMessage).Name == "Name21")), Times.Exactly(1));
            _messagePublisher.Verify(x => x.Publish(SerializedMessage, typeof(TestMessage), It.IsAny<Dictionary<string, string>>()), Times.Never());
            _messagePublisher.Verify(x => x.Send(SerializedMessage, typeof(TestMessage), It.IsAny<IMessageQueue>(), It.IsAny<Dictionary<string, string>>()), Times.Exactly(2));
        }

        #endregion PUBLISH LOCAL TESTS
    }
}
