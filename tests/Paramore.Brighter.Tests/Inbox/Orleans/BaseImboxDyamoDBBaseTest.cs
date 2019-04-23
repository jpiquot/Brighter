using System;
using Newtonsoft.Json;
using Paramore.Brighter.Inbox.Orleans;

namespace Paramore.Brighter.Tests.Inbox.Orleans
{
    public abstract class BaseImboxDyamoDBBaseTest : IDisposable
    {
        protected readonly OrleansTestHelper OrleansTestHelper;

        protected BaseImboxDyamoDBBaseTest()
        {
            OrleansTestHelper = new OrleansTestHelper();
        }
        
        protected OrleansCommand<T> ConstructCommand<T>(T command, DateTime timeStamp, string contextKey) where T : class, IRequest
        {
            return new OrleansCommand<T>
            {
                CommandDate = $"{typeof(T).Name}+{timeStamp:yyyy-MM-dd}",
                Time = $"{timeStamp.Ticks}",
                CommandId = command.Id.ToString(),
                CommandType = typeof(T).Name,
                CommandBody = JsonConvert.SerializeObject(command),
                ContextKey = contextKey
            };
        }
                
        public void Dispose()
        {
            OrleansTestHelper.CleanUpCommandDb();
        }
    }
}
