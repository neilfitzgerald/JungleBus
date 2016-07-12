﻿using System;
using Amazon.SQS.Model;
using JungleBus.Exceptions;
using JungleBus.Interfaces.Serialization;
using JungleBus.Serialization;

namespace JungleBus.Messaging
{
    /// <summary>
    /// Parses out SNS messages from the input queue
    /// </summary>
    internal class MessageParser : IMessageParser
    {
        /// <summary>
        /// Instance of the message serializer
        /// </summary>
        private readonly IMessageSerializer _messageSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageParser" /> class.
        /// </summary>
        /// <param name="serializer">Instance of the message serializer</param>
        public MessageParser(IMessageSerializer serializer)
        {
            _messageSerializer = serializer;
        }

        /// <summary>
        /// Parse the Amazon SQS message
        /// </summary>
        /// <param name="message">Message to parse</param>
        /// <returns>Parsed message</returns>
        public TransportMessage ParseMessage(Message message)
        {
            TransportMessage parsedMessage = new TransportMessage()
            {
                ReceiptHandle = message.ReceiptHandle
            };

            try
            {
                if (message.Attributes != null && message.Attributes.ContainsKey("ApproximateReceiveCount"))
                {
                    parsedMessage.RetryCount = int.Parse(message.Attributes["ApproximateReceiveCount"]);
                }

                SnsMessage snsMessage = _messageSerializer.Deserialize(message.Body, typeof(SnsMessage)) as SnsMessage;
                parsedMessage.Body = snsMessage.Message;

                parsedMessage.MessageTypeName = snsMessage.MessageAttributes["messageType"].Value;
                parsedMessage.MessageType = Type.GetType(parsedMessage.MessageTypeName, false, true);
                if (parsedMessage.MessageType == null)
                {
                    parsedMessage.MessageParsingSucceeded = false;
                    parsedMessage.Exception = new JungleBusException("Unable to find message type " + parsedMessage.MessageTypeName);
                }
                else
                {
                    parsedMessage.Message = _messageSerializer.Deserialize(parsedMessage.Body, parsedMessage.MessageType);
                    parsedMessage.MessageParsingSucceeded = true;
                }
            }
            catch (Exception ex)
            {
                parsedMessage.MessageParsingSucceeded = false;
                parsedMessage.Exception = new JungleBusException("Failed to parse message", ex);
            }

            return parsedMessage;
        }
    }
}