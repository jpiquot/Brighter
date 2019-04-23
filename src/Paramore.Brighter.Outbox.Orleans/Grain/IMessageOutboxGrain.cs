using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;

namespace Paramore.Brighter.Outbox.Orleans.Grain
{
    public interface IMessageOutboxGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Add a new serialized command to the inbox
        /// </summary>
        /// <param name="message">Serialized message</param>
        /// <returns>returns false if already exists</returns>
        Task<bool> Add(string message);
        /// <summary>
        /// Get the serialized command
        /// </summary>
        /// <returns></returns>
        Task<string> Get();
        Task<string> GetContextKey();
    }
}
