﻿#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Outbox.Sqlite
{
    /// <summary>
    ///     Class SqliteOutbox.
    /// </summary>
    public class SqliteOutbox :
        IAmAnOutbox<Message>,
        IAmAnOutboxAsync<Message>,
        IAmAnOutboxViewer<Message>,
        IAmAnOutboxViewerAsync<Message>
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<SqliteOutbox>);

        private const int SqliteDuplicateKeyError = 1555;
        private const int SqliteUniqueKeyError = 19;
        private readonly SqliteOutboxConfiguration _configuration;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SqliteOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public SqliteOutbox(SqliteOutboxConfiguration configuration)
        {
            _configuration = configuration;
            ContinueOnCapturedContext = false;
        }

        /// <summary>
        ///     Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout"></param>
        /// <returns>Task.</returns>
        public void Add(Message message, int outBoxTimeout = -1)
        {
            var parameters = InitAddDbParameters(message);

            using (var connection = GetConnection())
            {
                connection.Open();
                var sql = GetAddSql();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddParamtersParamArrayToCollection(parameters.ToArray(), command);

                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch (SqliteException sqlException)
                    {
                        if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                        {
                            _logger.Value.WarnFormat(
                                "MsSqlOutbox: A duplicate Message with the MessageId {0} was inserted into the Outbox, ignoring and continuing",
                                message.Id);
                            return;
                        }

                        throw;
                    }
                };
            }
        }

        private static bool IsExceptionUnqiueOrDuplicateIssue(SqliteException sqlException)
        {
            return sqlException.SqliteErrorCode == SqliteDuplicateKeyError ||
                   sqlException.SqliteErrorCode == SqliteUniqueKeyError;
        }

        private string GetAddSql()
        {
            var sql =
                string.Format(
                    "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @HeaderBag, @Body)",
                    _configuration.OutboxTableName);
            return sql;
        }

        /// <summary>
        ///     Gets the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId",
                _configuration.OutboxTableName);
            var parameters = new[]
            {
                CreateSqlParameter("@MessageId", messageId.ToString())
            };

            return ExecuteCommand(command => MapFunction(command.ExecuteReader()), sql, outBoxTimeout, parameters);
        }

        public async Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = InitAddDbParameters(message);

            using (var connection = GetConnection())
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                using (var command = connection.CreateCommand())
                {
                    var sql = GetAddSql();
                    command.CommandText = sql;
                    AddParamtersParamArrayToCollection(parameters.ToArray(), command);

                    try
                    {
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                    }
                    catch (SqliteException sqlException)
                    {
                        if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                        {
                            _logger.Value.WarnFormat("MsSqlOutbox: A duplicate Message with the MessageId {0} was inserted into the Outbox, ignoring and continuing",
                                message.Id);
                            return;
                        }

                        throw;
                    }
                }
            }
        }

        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the orginating synchronization context if you need to access
        ///     thread specific storage
        ///     such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        /// get as an asynchronous operation.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task{Message}" />.</returns>
        public async Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId", _configuration.OutboxTableName);
            var parameters = new[]
            {
                CreateSqlParameter("@MessageId", messageId.ToString())
            };

            return await ExecuteCommandAsync(
                async command => MapFunction(await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext)),
                sql,
                outBoxTimeout,
                cancellationToken,
                parameters)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <summary>
        ///     Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <returns></returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                SetPagingCommandFor(command, pageSize, pageNumber);

                connection.Open();

                using (var dbDataReader = command.ExecuteReader())
                {
                    var messages = new List<Message>();
                    while (dbDataReader.Read())
                    {
                        messages.Add(MapAMessage(dbDataReader));
                    }
                    return messages;
                }
            }
        }

        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns></returns>
        public async Task<IList<Message>> GetAsync(int pageSize = 100, int pageNumber = 1,CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                SetPagingCommandFor(command, pageSize, pageNumber);

                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                var messages = new List<Message>();
                using (var dbDataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                {
                    while (await dbDataReader.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                    {
                        messages.Add(MapAMessage(dbDataReader));
                    }
                };
                return messages;
            }
        }

        private SqliteParameter CreateSqlParameter(string parameterName, object value)
        {
            return new SqliteParameter(parameterName, value);
        }

        private T ExecuteCommand<T>(Func<SqliteCommand, T> execute, string sql, int outboxTimeout,
            params SqliteParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                if (outboxTimeout != -1) command.CommandTimeout = outboxTimeout;

                connection.Open();
                var item = execute(command);
                return item;
            }
        }

        private async Task<T> ExecuteCommandAsync<T>(
            Func<SqliteCommand, Task<T>> execute,
            string sql,
            int timeoutInMilliseconds,
            CancellationToken cancellationToken = default(CancellationToken),
            params SqliteParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                var item = await execute(command).ConfigureAwait(ContinueOnCapturedContext);
                return item;
            }
        }

        private SqliteConnection GetConnection()
        {
            return new SqliteConnection(_configuration.ConnectionString);
        }

        private SqliteParameter[] InitAddDbParameters(Message message)
        {
            var bagJson = JsonConvert.SerializeObject(message.Header.Bag);
            return new[]
            {
                new SqliteParameter("@MessageId", SqliteType.Text) { Value = message.Id.ToString()},
                new SqliteParameter("@MessageType", SqliteType.Text) { Value = message.Header.MessageType.ToString()},
                new SqliteParameter("@Topic", SqliteType.Text) { Value =message.Header.Topic},
                new SqliteParameter("@Timestamp", SqliteType.Text) { Value =message.Header.TimeStamp.ToString("s")},
                new SqliteParameter("@HeaderBag",SqliteType.Text) { Value = bagJson},
                new SqliteParameter("@Body", SqliteType.Text) { Value = message.Body.Value}
            };
        }

        private Message MapAMessage(IDataReader dr)
        {
            //var id = dr.GetGuid(dr.GetOrdinal("MessageId"));
            var id = Guid.Parse(dr.GetString(0));
            var messageType =
                (MessageType) Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
            var topic = dr.GetString(dr.GetOrdinal("Topic"));

            var header = new MessageHeader(id, topic, messageType);

            if (dr.FieldCount > 4)
            {
                //new schema....we've got the extra header information
                var ordinal = dr.GetOrdinal("Timestamp");
                var timeStamp = dr.IsDBNull(ordinal)
                    ? DateTime.MinValue
                    : dr.GetDateTime(ordinal);
                header = new MessageHeader(id, topic, messageType, timeStamp, 0, 0);

                var i = dr.GetOrdinal("HeaderBag");
                var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
                var dictionaryBag = JsonConvert.DeserializeObject<Dictionary<string, string>>(headerBag);
                if (dictionaryBag != null)
                {
                    foreach (var key in dictionaryBag.Keys)
                    {
                        header.Bag.Add(key, dictionaryBag[key]);
                    }
                }
            }

            var body = new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

            return new Message(header, body);
        }

        private Message MapFunction(IDataReader dr)
        {
            using (dr)
            {
                if (dr.Read())
                {
                    return MapAMessage(dr);
                }

                return new Message();
            }
        }

        private void SetPagingCommandFor(SqliteCommand command, int pageSize, int pageNumber)
        {
            SqliteParameter[] parameters = new[]
            {
                CreateSqlParameter("PageNumber", pageNumber-1),
                CreateSqlParameter("PageSize", pageSize)
            };

            var sql = string.Format("SELECT * FROM {0} ORDER BY Timestamp DESC limit @PageSize OFFSET @PageNumber", _configuration.OutboxTableName);

            command.CommandText = sql;
            AddParamtersParamArrayToCollection(parameters, command);
        }

        public void AddParamtersParamArrayToCollection(SqliteParameter[] parameters, SqliteCommand command)
        {
            command.Parameters.AddRange(parameters);
        }
    }
}
