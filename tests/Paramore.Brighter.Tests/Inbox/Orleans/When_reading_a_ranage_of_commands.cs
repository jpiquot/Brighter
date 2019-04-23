using System;
using System.Linq;
using Amazon.Orleansv2.DataModel;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.Inbox.Orleans;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.Inbox.Orleans
{
    [Trait("Category", "Orleans")]
    public class OrleansRangeOfCommandsTests : IDisposable
    {
        private readonly Guid[] _guids;
        private readonly MyCommand _commandEarliest, _command2, _commandLatest;
        private readonly OrleansCommand<MyCommand> _storedCommandEarliest, _storedCommand2, _storedCommandLatest;
        private readonly OrleansInbox _OrleansInbox;
        private readonly DateTime _timeStamp = new DateTime(2018, 7, 5, 12, 0, 0);
        private readonly OrleansTestHelper _OrleansTestHelper;
        
        public OrleansRangeOfCommandsTests()
        {            
            _guids = new[] {Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()};
            
            _commandEarliest = new MyCommand { Id = _guids[0], Value = "Test Earliest"};
            _storedCommandEarliest = ConstructCommand(_commandEarliest, _timeStamp.AddHours(-4));
            
            _command2 = new MyCommand { Id = _guids[1], Value = "Test Message 2"};
            _storedCommand2 = ConstructCommand(_command2, _timeStamp.AddHours(-2));
            
            _commandLatest = new MyCommand { Id = _guids[2], Value = "Test Latest"};
            _storedCommandLatest = ConstructCommand(_commandLatest, _timeStamp.AddHours(-1));
            
            var nonTopicCommand = ConstructCommand(new DifferentCommand { Id = _guids[3], Value = "Different Command "}, _timeStamp.AddHours(-2));

            _OrleansTestHelper = new OrleansTestHelper();
            var createTableRequest = new OrleansInboxBuilder(_OrleansTestHelper.OrleansInboxTestConfiguration.TableName).CreateInboxTableRequest();
            
            _OrleansTestHelper.CreateInboxTable(createTableRequest);
            _OrleansInbox = new OrleansInbox(_OrleansTestHelper.OrleansContext, _OrleansTestHelper.OrleansInboxTestConfiguration);

            var config = new OrleansOperationConfig
            {
                OverrideTableName = _OrleansTestHelper.OrleansInboxTestConfiguration.TableName,
                ConsistentRead = false
            };
            
            var dbContext = _OrleansTestHelper.OrleansContext;
            dbContext.SaveAsync(_storedCommandEarliest, config).GetAwaiter().GetResult();
            dbContext.SaveAsync(_storedCommand2, config).GetAwaiter().GetResult();
            dbContext.SaveAsync(_storedCommandLatest, config).GetAwaiter().GetResult();
            dbContext.SaveAsync(nonTopicCommand, config).GetAwaiter().GetResult();
        }

        private OrleansCommand<T> ConstructCommand<T>(T command, DateTime timeStamp) where T : class, IRequest
        {                                               
            return new OrleansCommand<T>
            {
                CommandDate = $"{typeof(T).Name}+{timeStamp:yyyy-MM-dd}",
                Time = $"{timeStamp.Ticks}",
                CommandId = command.Id.ToString(),
                CommandType = typeof(T).Name,
                CommandBody = JsonConvert.SerializeObject(command),       
            };
        }        

        [Fact]
        public void When_reading_messages_by_numerical_range()
        {
            var retrievedMessages = _OrleansInbox.Get<MyCommand>(_timeStamp, _timeStamp.AddHours(-3), _timeStamp.AddHours(-2));
            
            //_should_read_the_last_two_messages_from_the_store
            retrievedMessages.Should().HaveCount(1);
            retrievedMessages.Single().Should().BeEquivalentTo(_command2);                                  
        }        
        
        [Fact]
        public void When_reading_message_from_time()
        {
            var retrievedMessages = _OrleansInbox.Get<MyCommand>(_timeStamp, _timeStamp.AddHours(-2));

            //_should_read_the_last_two_messages_from_the_store
            retrievedMessages.Should().HaveCount(2);
            retrievedMessages.First().Should().BeEquivalentTo(_command2);
            retrievedMessages.Last().Should().BeEquivalentTo(_commandLatest);
        }
        
        [Fact]
        public void When_reading_message_until_time()
        {
            var retrievedMessages = _OrleansInbox.Get<MyCommand>(_timeStamp, endTime: _timeStamp.AddHours(-2));

            //_should_read_the_last_two_messages_from_the_store
            retrievedMessages.Should().HaveCount(2);
            retrievedMessages.First().Should().BeEquivalentTo(_commandEarliest);
            retrievedMessages.Last().Should().BeEquivalentTo(_command2);
        }
        
        public void Dispose()
        {
            _OrleansTestHelper.CleanUpCommandDb();
        }
    }

    internal class DifferentCommand : ICommand
    {
        public DifferentCommand()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; set; }

        public string Value { get; set; }
    }
}
