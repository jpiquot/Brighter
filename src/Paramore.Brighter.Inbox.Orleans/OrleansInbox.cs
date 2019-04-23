using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Orleans;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Inbox.Orleans.Grain;

namespace Paramore.Brighter.Inbox.Orleans
{
    /// <summary>
    ///     Class MsSqlInbox.
    /// </summary>
    public class OrleansInbox : IAmAnInbox, IAmAnInboxAsync
    {
        private static readonly Lazy<ILog> s_logger = new Lazy<ILog>(LogProvider.For<OrleansInbox>);
        private readonly IGrainFactory _grainFactory;


        /// <summary>
        ///     Initializes a new instance of the <see cref="OrleansInbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public OrleansInbox(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
            ContinueOnCapturedContext = false;
        }

        /// <summary>
        ///     Adds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        public void Add<T>(T command, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest 
            => AddAsync(command, contextKey, timeoutInMilliseconds).GetAwaiter().GetResult();

        /// <summary>
        ///     Adds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken">Allow the sender to cancel the request, optional</param>
        /// <returns><see cref="Task" />.</returns>
        public async Task AddAsync<T>(T command, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken=default) where T : class, IRequest
        {
            try
            {
                if (timeoutInMilliseconds != -1)
                {
                    s_logger.Value.WarnFormat("OrleansInbox/Add: Timeout parameter is not supported. Value is ignored.");
                }
                if (! await _grainFactory.GetGrain<ICommandInboxGrain>(command.Id)
                     .Add(JsonConvert.SerializeObject(command), typeof(T).Name, contextKey))
                {
                    s_logger.Value.WarnFormat(
                            "OrleansInbox/Add: A duplicate Command with the CommandId {0} was inserted into the Message Store, ignoring and continuing",
                            command.Id);
                }
            }
            catch (Exception e)
            {
                s_logger.Value.ErrorFormat(
                        "OrleansInbox/Add: An error occurred while adding the Command into the Message Store : " + e.Message + "\nCommand : " + JsonConvert.SerializeObject(command));
                throw;
            }
        }

        /// <summary>
        ///     Finds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <returns>T.</returns>
        public T Get<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
            => GetAsync<T>(id, contextKey, timeoutInMilliseconds).GetAwaiter().GetResult();
        /// <summary>
        ///     Awaitably finds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken">Allow the sender to cancel the request</param>
        /// <returns><see cref="Task{T}" />.</returns>
        public async Task<T> GetAsync<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default)
            where T : class, IRequest
        {
            try
            {
                if (timeoutInMilliseconds != -1)
                {
                    s_logger.Value.WarnFormat("OrleansInbox/Get: Timeout parameter is not supported. Value is ignored.");
                }
                CommandAndContext result = await _grainFactory
                    .GetGrain<ICommandInboxGrain>(id)
                    .Get();
                if (contextKey == result.ContextKey)
                {
                    return JsonConvert.DeserializeObject<T>(result.Command);
                }
                else
                {
                    throw new Exception($"The stored context key '{contextKey}' is invalid for this command.");
                }
            }
            catch (Exception e)
            {
                s_logger.Value.ErrorFormat(
                        "OrleansInbox/Get: An error occurred while retreiving the Command {0} from the Message Store : " + e.Message, id);
                throw;
            }
        }

        /// <summary>
        /// Checks whether a command with the specified identifier exists in the store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds"></param>
        /// <returns>True if it exists, False otherwise</returns>
        public bool Exists<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
            => ExistsAsync<T>(id, contextKey, timeoutInMilliseconds).GetAwaiter().GetResult();


        /// <summary>
        /// Checks whether a command with the specified identifier exists in the store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds"></param>
        /// <returns>True if it exists, False otherwise</returns>
        public async Task<bool> ExistsAsync<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default) where T : class, IRequest
        {
            try
            {
                if (timeoutInMilliseconds != -1)
                {
                    s_logger.Value.WarnFormat("OrleansInbox/Exists: Timeout parameter is not supported. Value is ignored.");
                }
                return (await _grainFactory
                    .GetGrain<ICommandInboxGrain>(id)
                    .GetContextKey()) == contextKey;
            }
            catch (Exception e)
            {
                s_logger.Value.ErrorFormat(
                        "OrleansInbox/Exists: An error occurred while checking if Command {0} with ContextKey {1} exists in the Message Store : " + e.Message, id, contextKey);
                throw;
            }
        }

        public bool ContinueOnCapturedContext { get; set; }
    }
}
