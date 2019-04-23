using System;
using Amazon.Orleansv2.Model;
using Paramore.Brighter.Inbox.Orleans;
using Paramore.Brighter.Outbox.Orleans;

namespace Paramore.Brighter.Tests.Outbox.Orleans
{
    public class BaseOrleansOutboxTests : IDisposable
    {
        protected readonly ProvisionedThroughput _throughput = new ProvisionedThroughput(2, 1);
        protected readonly OrleansTestHelper _OrleansTestHelper;
        protected readonly OrleansOutbox OrleansOutbox;

        public BaseOrleansOutboxTests()
        {           
            _OrleansTestHelper = new OrleansTestHelper();

            var createTableRequest = new OrleansOutboxBuilder(_OrleansTestHelper.OrleansMessageStoreTestConfiguration.TableName)
                .CreateOutboxTableRequest(_throughput, _throughput);

            _OrleansTestHelper.CreateOutboxTable(createTableRequest);

            OrleansOutbox = new OrleansOutbox(_OrleansTestHelper.OrleansContext, _OrleansTestHelper.OrleansMessageStoreTestConfiguration);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Release();
        }

        private void Release()
        {
            _OrleansTestHelper.CleanUpOutboxDb();
        }

        ~BaseOrleansOutboxTests()
        {
            Release();
        }
    }
}
