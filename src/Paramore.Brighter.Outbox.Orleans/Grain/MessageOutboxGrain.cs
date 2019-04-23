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
        public async Task<bool> Add(string command, string commandType, string contextKey)
        {
            if (string.IsNullOrEmpty(command) && string.IsNullOrEmpty(contextKey))
            {
                State.Command = command;
                State.CommandType = commandType;
                State.ContextKey = contextKey;
                State.SubmittedDateTime = DateTime.UtcNow;
                await WriteStateAsync();
                return true;
            }
            return false;
        }
        public Task<CommandAndContext> Get() 
            => Task.FromResult(new CommandAndContext { ContextKey = State.ContextKey, Command = State.Command });
        public Task<string> GetContextKey() => Task.FromResult(State.ContextKey);
    }
}
