using System;
using Amazon.Orleansv2.DataModel;
using FluentAssertions;
using Paramore.Brighter.Inbox.Orleans;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.Inbox.Orleans
{
    [Trait("Category", "Orleans")]
    public class OrleansCommandExistsTests : BaseImboxDyamoDBBaseTest
    {       
        private readonly MyCommand _command;
       
        private readonly OrleansInbox _OrleansInbox;
        private readonly Guid _guid = Guid.NewGuid();
        private string _contextKey;

        public OrleansCommandExistsTests()
        {                        
            _command = new MyCommand { Id = _guid, Value = "Test Earliest"};
            _contextKey = "test-context-key";

            var createTableRequest = new OrleansInboxBuilder(OrleansTestHelper.OrleansInboxTestConfiguration.TableName).CreateInboxTableRequest();
            
            OrleansTestHelper.CreateInboxTable(createTableRequest);
            _OrleansInbox = new OrleansInbox(OrleansTestHelper.OrleansContext, OrleansTestHelper.OrleansInboxTestConfiguration);

            var config = new OrleansOperationConfig
            {
                OverrideTableName = OrleansTestHelper.OrleansInboxTestConfiguration.TableName,
                ConsistentRead = false
            };
            
            var dbContext = OrleansTestHelper.OrleansContext;
            dbContext.SaveAsync(ConstructCommand(_command, DateTime.UtcNow, _contextKey), config).GetAwaiter().GetResult();
        }

        [Fact]
        public void When_checking_a_command_exist()
        {
            var commandExists = _OrleansInbox.Exists<MyCommand>(_command.Id, _contextKey);

            commandExists.Should().BeTrue("because the command exists.", commandExists);
        }

        [Fact]
        public void When_checking_a_command_exist_different_context_key()
        {
            var commandExists = _OrleansInbox.Exists<MyCommand>(_command.Id, "some-other-context-key");

            commandExists.Should().BeFalse("because the command exists for a different context key.", commandExists);
        }

        [Fact]
        public void When_checking_a_command_does_not_exist()
        {
            var commandExists = _OrleansInbox.Exists<MyCommand>(Guid.Empty, _contextKey);

            commandExists.Should().BeFalse("because the command doesn't exists.", commandExists);
        }
    }
}
