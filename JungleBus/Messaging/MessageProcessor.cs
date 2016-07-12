﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Transactions;
using Common.Logging;
using JungleBus.Interfaces;
using JungleBus.Interfaces.IoC;

namespace JungleBus.Messaging
{
    /// <summary>
    /// Responsible for handling transport messages and calling the appropriate message handlers
    /// </summary>
    internal class MessageProcessor : IMessageProcessor
    {
        /// <summary>
        /// Instance of the logger
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(MessageProcessor));

        /// <summary>
        /// Collection of known message handlers
        /// </summary>
        private readonly Dictionary<Type, HashSet<Type>> _handlerTypes;

        /// <summary>
        /// Configured object builder
        /// </summary>
        private readonly IObjectBuilder _objectBuilder;

        /// <summary>
        /// Collection of message fault handlers organized by message type
        /// </summary>
        private readonly Dictionary<Type, HashSet<Type>> _faultHandlers;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageProcessor" /> class.
        /// </summary>
        /// <param name="objectBuilder">Use to construct the message handlers</param>
        /// <param name="handlers">Collection of message handlers organized by message type</param>
        /// <param name="faultHandlers">Collection of message fault handlers organized by message type</param>
        public MessageProcessor(IObjectBuilder objectBuilder, Dictionary<Type, HashSet<Type>> handlers, Dictionary<Type, HashSet<Type>> faultHandlers)
        {
            _objectBuilder = objectBuilder;
            _handlerTypes = handlers;
            _faultHandlers = faultHandlers;
        }

        /// <summary>
        /// Process the given message
        /// </summary>
        /// <param name="message">Transport message to be dispatched to the event handlers</param>
        /// <param name="busInstance">Instance of the send bus to inject into the event handlers</param>
        /// <returns>True is all event handlers processed successfully</returns>
        public MessageProcessingResult ProcessMessage(TransportMessage message, IBus busInstance)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            MessageProcessingResult result = new MessageProcessingResult();
            if (_handlerTypes.ContainsKey(message.MessageType))
            {
                Log.TraceFormat("Processing {0} handlers for message type {1}", _handlerTypes[message.MessageType].Count, message.MessageTypeName);
                var handlerMethod = typeof(IHandleMessage<>).MakeGenericType(message.MessageType).GetMethod("Handle");
                using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required))
                {
                    foreach (Type handlerType in _handlerTypes[message.MessageType])
                    {
                        using (IObjectBuilder childBuilder = _objectBuilder.GetNestedBuilder())
                        {
                            if (busInstance != null)
                            {
                                childBuilder.RegisterInstance<IBus>(busInstance);
                            }

                            childBuilder.RegisterInstance(LogManager.GetLogger(handlerType));
                            Log.TraceFormat("Running handler {0}", handlerType.Name);
                            try
                            {
                                var handler = childBuilder.GetValue(handlerType);
                                handlerMethod.Invoke(handler, new object[] { message.Message });
                            }
                            catch (TargetInvocationException ex)
                            {
                                Log.ErrorFormat("Error in handling {0} with {1}", ex, message.MessageType.Name, handlerType.Name);
                                result.Exception = ex.InnerException ?? ex;
                            }
                            catch (Exception ex)
                            {
                                Log.ErrorFormat("Error in handling {0} with {1}", ex, message.MessageType.Name, handlerType.Name);
                                result.Exception = ex;
                            }
                        }
                    }

                    result.WasSuccessful = result.Exception == null;
                    transactionScope.Complete();
                }
            }
            else
            {
                Log.WarnFormat("Could not find a handler for {0}", message.MessageTypeName);
                result.Exception = new Exception(string.Format("Could not find a handler for {0}", message.MessageTypeName));
            }

            return result;
        }

        /// <summary>
        /// Processes inbound message that have faulted more than the retry limit
        /// </summary>
        /// <param name="message">Message to process</param>
        /// <param name="busInstance">Instance of the bus to pass to event handlers</param>
        /// <param name="ex">Exception caused by the message</param>
        public void ProcessFaultedMessage(TransportMessage message, IBus busInstance, Exception ex)
        {
            ProcessFaultedMessageHandlers(message, busInstance, ex);
            if (message.MessageParsingSucceeded)
            {
                ProcessFaultedMessageHandlers(message.Message, busInstance, ex);
            }
        }

        /// <summary>
        /// Calls the fault handler on the message object
        /// </summary>
        /// <param name="message">Message to process</param>
        /// <param name="busInstance">Instance of the bus to pass to event handlers</param>
        /// <param name="messageException">Exception caused by the message</param>
        private void ProcessFaultedMessageHandlers(object message, IBus busInstance, Exception messageException)
        {
            Type messageType = message.GetType();
            if (_faultHandlers.ContainsKey(messageType))
            {
                Log.TraceFormat("Processing {0} fault handlers for message type {1}", _faultHandlers[messageType].Count, messageType.FullName);
                var handlerMethod = typeof(IHandleMessageFaults<>).MakeGenericType(messageType).GetMethod("Handle");
                foreach (Type handlerType in _faultHandlers[messageType])
                {
                    using (IObjectBuilder childBuilder = _objectBuilder.GetNestedBuilder())
                    {
                        if (busInstance != null)
                        {
                            childBuilder.RegisterInstance<IBus>(busInstance);
                        }

                        childBuilder.RegisterInstance(LogManager.GetLogger(handlerType));
                        Log.TraceFormat("Running handler {0}", handlerType.Name);
                        try
                        {
                            var handler = childBuilder.GetValue(handlerType);
                            handlerMethod.Invoke(handler, new object[] { message, messageException });
                        }
                        catch (Exception ex)
                        {
                            Log.ErrorFormat("Error in handling message fault {0} with {1}", ex, messageType.Name, handlerType.Name);
                        }
                    }
                }
            }
        }
    }
}