﻿// <copyright file="SnsClient.cs">
//     The MIT License (MIT)
//
// Copyright(c) 2016 Ryan Fleming
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// </copyright>
using System;
using System.Collections.Generic;
using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using JungleBus.Messaging;

namespace JungleBus.Aws.Sns
{
    /// <summary>
    /// Client for talking to SNS
    /// </summary>
    public sealed class SnsClient : IDisposable, ISnsClient
    {
        /// <summary>
        /// Cached list of topic ARNs
        /// </summary>
        private readonly Dictionary<string, string> _topicArns;

        /// <summary>
        /// Connection to SNS
        /// </summary>
        private IAmazonSimpleNotificationService _sns;

        /// <summary>
        /// Initializes a new instance of the <see cref="SnsClient" /> class.
        /// </summary>
        /// <param name="endpoint">Amazon Endpoint we are connecting to</param>
        public SnsClient(RegionEndpoint endpoint)
        {
            _sns = new AmazonSimpleNotificationServiceClient(endpoint);
            _topicArns = new Dictionary<string, string>();
        }

        /// <summary>
        /// Format a message type into a topic name
        /// </summary>
        /// <param name="messageType">Message type</param>
        /// <returns>Formatted Topic name</returns>
        public static string GetTopicName(Type messageType)
        {
            if (messageType == null)
            {
                throw new ArgumentNullException("messageType");
            }

            return messageType.FullName.Replace('.', '_');
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, 
        /// or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_sns != null)
            {
                _sns.Dispose();
                _sns = null;
            }
        }

        /// <summary>
        /// Publishes the serialized message
        /// </summary>
        /// <param name="message">Serialized Message</param>
        /// <param name="type">Payload type</param>
        /// <param name="metadata">Message metadata to embed</param>
        public void Publish(string message, Type type, Dictionary<string, string> metadata)
        {
            string topicName = GetTopicName(type);
            if (!_topicArns.ContainsKey(topicName))
            {
                Topic topic = _sns.FindTopic(topicName);
                if (topic == null)
                {
                    throw new InvalidParameterException("Unknown topic name " + topicName);
                }
                else
                {
                    _topicArns[topicName] = topic.TopicArn;
                }
            }

            PublishRequest request = new PublishRequest(_topicArns[topicName], message);
            foreach (var kvp in metadata)
            {
                request.MessageAttributes[kvp.Key] = new MessageAttributeValue() { StringValue = kvp.Value, DataType = "String", };
            }

            request.MessageAttributes[MessageConstants.MessageTypeAttribute] = new MessageAttributeValue() { StringValue = type.AssemblyQualifiedName, DataType = "String" };
            request.MessageAttributes["fromSns"] = new MessageAttributeValue() { StringValue = "True", DataType = "String" };

            _sns.Publish(request);
        }

        /// <summary>
        /// Setups the bus for publishing the given message types
        /// </summary>
        /// <param name="messageTypes">Message types</param>
        public void SetupMessagesForPublishing(IEnumerable<Type> messageTypes)
        {
            foreach (Type messageType in messageTypes)
            {
                string topicName = GetTopicName(messageType);
                if (!_topicArns.ContainsKey(topicName))
                {
                    Topic topic = _sns.FindTopic(topicName);
                    if (topic == null)
                    {
                        _topicArns[topicName] = CreateTopic(topicName);
                    }
                    else
                    {
                        _topicArns[topicName] = topic.TopicArn;
                    }
                }
            }
        }

        /// <summary>
        /// Creates a topic with the given name
        /// </summary>
        /// <param name="topicName">Topic name</param>
        /// <returns>Created topic ARN</returns>
        private string CreateTopic(string topicName)
        {
            CreateTopicResponse response = _sns.CreateTopic(topicName);
            return response.TopicArn;
        }
    }
}
