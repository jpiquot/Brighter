using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Orleans;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Outbox.Orleans.Grain;

namespace Paramore.Brighter.Outbox.Orleans
{
    /// <summary>
    ///     Class MySqlMessageStore.
    /// </summary>
    public class OrleansOutbox : IAmAnOutbox<Message>,
                                 IAmAnOutboxAsync<Message>,
                                 IAmAnOutboxViewer<Message>,
                                 IAmAnOutboxViewerAsync<Message>
    {
        private static readonly Lazy<ILog> SLogger = new Lazy<ILog>(LogProvider.For<OrleansOutbox>);
        private readonly IGrainFactory _grainFactory;


        /// <summary>
        ///     Initializes a new instance of the <see cref="OrleansOutbox" /> class.
        /// </summary>
        public OrleansOutbox(IGrainFactory grainFactory)
        {
            _grainFactory             = grainFactory;
            ContinueOnCapturedContext = false;
        }

        public void Add(Message message, int outBoxTimeout = -1)
        {
            AddAsync(message, outBoxTimeout)
               .GetAwaiter()
               .GetResult();
        }

        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            return GetAsync(messageId, outBoxTimeout)
                  .GetAwaiter()
                  .GetResult();
        }

        public async Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default)
        {
            try
            {
                if (outBoxTimeout != -1)
                {
                    SLogger.Value.WarnFormat("OrleansOutbox/Add: Timeout parameter is not supported. Value is ignored.");
                }

                if (!await _grainFactory.GetGrain<IMessageOutboxGrain>(message.Id)
                                        .Add(JsonConvert.SerializeObject(message)))
                {
                    SLogger.Value.WarnFormat
                        (
                         "OrleansOutbox/Add: A duplicate message with the Id {0} was inserted into the Message Store, ignoring and continuing",
                         message.Id
                        );
                }
            }
            catch (Exception e)
            {
                SLogger.Value.ErrorFormat
                    (
                     "OrleansOutbox/Add: An error occurred while adding the message into the Message Store : " + e.Message
                   + "\nCommand : "
                   + JsonConvert.SerializeObject(message)
                    );

                throw;
            }
        }

        public async Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default)
        {
            try
            {
                if (outBoxTimeout != -1)
                {
                    SLogger.Value.WarnFormat("OrleansOutbox/Get: Timeout parameter is not supported. Value is ignored.");
                }

                string result = await _grainFactory.GetGrain<IMessageOutboxGrain>(messageId)
                                                   .Get();

                return JsonConvert.DeserializeObject<Message>(result);
            }
            catch (Exception e)
            {
                SLogger.Value.ErrorFormat
                    (
                     "OrleansOutbox/Get: An error occurred while retreiving the Command {0} from the Message Store : "
                   + e.Message, messageId
                    );

                throw;
            }
        }


        public bool ContinueOnCapturedContext { get; set; }

        public IList<Message> Get(int pageSize = 100, int pageNumber = 1)
        {
            return GetAsync(pageSize, pageNumber)
                  .GetAwaiter()
                  .GetResult();
        }

        public Task<IList<Message>> GetAsync
            (int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Orleans outbox");
        }
    }
}
