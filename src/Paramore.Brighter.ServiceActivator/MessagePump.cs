﻿#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.ServiceActivator
{
    public class MessagePump<TRequest> : MessagePumpBase<TRequest>, IAmAMessagePump where TRequest : class, IRequest
    {
        public MessagePump(IAmACommandProcessor commandProcessor, IAmAMessageMapper<TRequest> messageMapper)
            : base(commandProcessor, messageMapper)
        {}

        public bool IsAsync => false;

        // doesn't have async keyword, so won't actually run asynchronously / generate state machine
        protected override Task DispatchRequest(MessageHeader messageHeader, TRequest request)
        {
            var tcs = new TaskCompletionSource<object>();

            _logger.Value.DebugFormat("MessagePump: Dispatching message {0} from {2} on thread # {1}", request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            if (messageHeader.MessageType == MessageType.MT_COMMAND && request is IEvent)
            {
                throw new ConfigurationException(string.Format("Message {0} mismatch. Message type is '{1}' yet mapper produced message of type IEvent", request.Id, MessageType.MT_COMMAND));
            }
            if (messageHeader.MessageType == MessageType.MT_EVENT && request is ICommand)
            {
                throw new ConfigurationException(string.Format("Message {0} mismatch. Message type is '{1}' yet mapper produced message of type ICommand", request.Id, MessageType.MT_EVENT));
            }

            switch (messageHeader.MessageType)
            {
                case MessageType.MT_COMMAND:
                    {
                        _commandProcessor.Send(request);
                        break;
                    }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                    {
                        _commandProcessor.Publish(request);
                        break;
                    }
            }

            tcs.SetResult(new object());
            return tcs.Task;
        }

        protected override void SynchronizationContextHook()
        {
            //we use the default synchronization context i.e. the thread pool
            return;
        }
    }
}
