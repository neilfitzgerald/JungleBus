﻿// <copyright file="IMessageQueue.cs">
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
using System.Threading;
using System.Threading.Tasks;

namespace JungleBus.Messaging
{
    /// <summary>
    /// Client for talking to queues
    /// </summary>
    public interface IMessageQueue
    {
        /// <summary>
        /// Gets or sets the message parser for inbound messages
        /// </summary>
        IMessageParser MessageParser { get; set; }

        /// <summary>
        /// Retrieve messages from the underlying queue
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Messages or empty</returns>
        Task<IEnumerable<TransportMessage>> GetMessages(CancellationToken cancellationToken);

        /// <summary>
        /// Removes a message from the queue
        /// </summary>
        /// <param name="message">Message to remove</param>
        void RemoveMessage(TransportMessage message);

        /// <summary>
        /// Subscribe the queue to the given message types
        /// </summary>
        /// <param name="messageTypes">Message to subscribe to</param>
        /// <param name="subscriptionNameBuilder">Function to format the type into a name for the queue to subscribe to</param>
        void Subscribe(IEnumerable<Type> messageTypes, Func<Type, string> subscriptionNameBuilder);

        /// <summary>
        /// Adds the message to the queue
        /// </summary>
        /// <param name="message">Message to add to the queue</param>
        void AddMessage(string message);
    }
}
