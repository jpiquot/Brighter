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
using Npgsql;
using Newtonsoft.Json;
using System.Data;
using NpgsqlTypes;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    public class PostgreSqlOutbox : IAmAnOutbox<Message>,IAmAnOutboxViewer<Message>
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<PostgreSqlOutbox>);
        private const string PostgreSqlDuplicateKeyError_UniqueConstraintViolation = "23505";
        private readonly PostgreSqlOutboxConfiguration _configuration;

        public bool ContinueOnCapturedContext { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Initialises a new instance of <see cref="PostgreSqlOutbox"> class.
        /// </summary>
        /// <param name="configuration">PostgreSql Configuration.</param>
        public PostgreSqlOutbox(PostgreSqlOutboxConfiguration configuration)
        {
            _configuration = configuration;
        }
        public void Add(Message message, int outBoxTimeout = -1)
        {
            var parameters = InitAddDbParameters(message);
            using (var connection = GetConnection())
            {
                connection.Open();
                using (var command = InitAddDbCommand(connection, parameters))
                {
                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch(PostgresException sqlException)
                    {
                        if (sqlException.SqlState == PostgreSqlDuplicateKeyError_UniqueConstraintViolation)
                        {
                            _logger.Value.WarnFormat(
                                "MsSqlOutbox: A duplicate Message with the MessageId {0} was inserted into the Outbox, ignoring and continuing",
                                message.Id);
                            return;
                        }
                        throw;
                    }
                }


            }
        }
    
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            var sql = string.Format("SELECT Id, MessageId, Topic, MessageType, Timestamp, HeaderBag, Body FROM {0} WHERE MessageId = @MessageId", _configuration.OutboxTableName);
            var parameters = new[]
            {
                CreateNpgsqlParameter("MessageId", messageId)
            };
          
            return ExecuteCommand(command => MapFunction(command.ExecuteReader()), sql, outBoxTimeout, parameters);

        }

        private NpgsqlCommand InitAddDbCommand(NpgsqlConnection connection, NpgsqlParameter[] parameters)
        {
            var command = connection.CreateCommand();
            var sql = string.Format("INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp::timestamptz, @HeaderBag, @Body)",
                    _configuration.OutboxTableName);
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
            return command;
        }

        private NpgsqlParameter[] InitAddDbParameters(Message message)
        {
            var bagjson = JsonConvert.SerializeObject(message.Header.Bag);
            var parameters = new NpgsqlParameter[]
            {
                CreateNpgsqlParameter("MessageId",message.Id),
                CreateNpgsqlParameter("MessageType", message.Header.MessageType.ToString()),
                CreateNpgsqlParameter("Topic", message.Header.Topic),
                new NpgsqlParameter("Timestamp",NpgsqlDbType.TimestampTz) {Value =  message.Header.TimeStamp},
                CreateNpgsqlParameter("HeaderBag", bagjson),
                CreateNpgsqlParameter("Body", message.Body.Value)
            };
            return parameters;
        }

        private NpgsqlParameter CreateNpgsqlParameter(string parametername, object value)
        {
            return new NpgsqlParameter(parametername, value);
        }


        private Message MapFunction(IDataReader reader)
        {
            if(reader.Read())
            {
               return MapAMessage(reader);
            }
            return new Message();
        }

        private Message MapAMessage(IDataReader dr)
        {
            var id = dr.GetGuid(dr.GetOrdinal("MessageId"));
            var messageType = (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
            var topic = dr.GetString(dr.GetOrdinal("Topic"));

            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = (dr.IsDBNull(ordinal) ? DateTime.MinValue : dr.GetDateTime(ordinal)).ToUniversalTime();

            var header = new MessageHeader(id, topic, messageType, timeStamp, 0, 0);

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
            
            var body = new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

            return new Message(header, body);
        }

        private T ExecuteCommand<T>(Func<NpgsqlCommand,T> execute, string sql, int outboxTimeout, NpgsqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                if (outboxTimeout != -1) command.CommandTimeout = outboxTimeout;

                connection.Open();
                return execute(command);
            }
        }

        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_configuration.ConnectionString);
        }

        public IList<Message> Get(int pageSize = 100, int pageNumber = 1)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                SetPagingCommandFor(command, _configuration, pageSize, pageNumber);

                connection.Open();

                var dbDataReader = command.ExecuteReader();

                var messages = new List<Message>();
                while (dbDataReader.Read())
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                return messages;
            }
        }

        private void SetPagingCommandFor(NpgsqlCommand command, PostgreSqlOutboxConfiguration configuration, int pageSize,
            int pageNumber)
        {
            var pagingSqlFormat = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
            var parameters = new[]
            {
                CreateNpgsqlParameter("PageNumber", pageNumber),
                CreateNpgsqlParameter("PageSize", pageSize)
            };

            var sql = string.Format(pagingSqlFormat, _configuration.OutboxTableName);

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }

    }
}
