using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;

namespace Paramore.Brighter.Outbox.Orleans.Grain
{
    public class MessageOutboxGrain : Grain<MessageOutboxGrainState>, IMessageOutboxGrain
    {
        public async Task<bool> Add(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                State.Message = message;
                State.DateTime = DateTime.UtcNow;
                await WriteStateAsync();
                return true;
            }
            return false;
        }
        public Task<string> Get() 
            => Task.FromResult(State.Message);
    }
}
