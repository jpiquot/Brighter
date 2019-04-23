using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;

namespace Paramore.Brighter.Inbox.Orleans.Grain
{
    public interface ICommandInboxGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Add a new serialized command to the inbox
        /// </summary>
        /// <param name="command">Serialized command</param>
        /// <param name="commandType">Command type name</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <returns>returns false if already exists</returns>
        Task<bool> Add(string command, string commandType, string contextKey);
        /// <summary>
        /// Get the serialized command
        /// </summary>
        /// <returns></returns>
        Task<CommandAndContext> Get();
        Task<string> GetContextKey();
    }
}
