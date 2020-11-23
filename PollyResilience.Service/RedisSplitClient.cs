
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Wrap;
using StackExchange.Redis;

namespace PollyResilience.Service
{
    public class RedisSplitClient : IRedisClient
    {
        protected readonly IConfigurationRoot _configuration;
        protected readonly string _readConnectionString;
        protected readonly string _writeConnectionString;
        protected Lazy<ConnectionMultiplexer> _readMultiplexer;
        protected Lazy<ConnectionMultiplexer> _writeMultiplexer;
        protected IDatabase _writeDatabase;
        protected IDatabase _readDatabase;
        protected ISubscriber _subscriber;
        protected ILogger<RedisClient> _logger;
        protected readonly IAsyncPolicy _policy;
        protected readonly PolicyWrap<string> _retryCircuitStringFallback;

        public RedisSplitClient(
            ILogger<RedisClient> logger,
            IConfigurationRoot configuration,
            IAsyncPolicy policy,
            string writeConfigKey = "RedisWriteConnectionString",
            string readConfigKey = "RedisReadConnectionString")
        {
            _logger = logger;
            _configuration = configuration;
            _policy = policy;
            _writeConnectionString = _configuration[writeConfigKey];
            _readConnectionString = _configuration[readConfigKey];

            _writeMultiplexer = CreateMultiplexer(_writeConnectionString);
            _readMultiplexer = CreateMultiplexer(_readConnectionString);

            _writeDatabase = _writeMultiplexer.Value.GetDatabase();
            _readDatabase = _readMultiplexer.Value.GetDatabase();

            _subscriber = _writeMultiplexer.Value.GetSubscriber();
        }

        public async Task<List<string>> GetKeys(string query = "*")
        {
            return await _policy.ExecuteAsync(async () =>
            {
                var keys = new List<string>();

                foreach (var endpoint in _readMultiplexer.Value.GetEndPoints())
                {
                    var hostAndPort = endpoint.ToString().Replace($"{endpoint.AddressFamily}/", string.Empty);
                    var server = _readMultiplexer.Value.GetServer(hostAndPort);

                    if (server.IsConnected)
                    {
                        foreach (var key in server.Keys(pattern: query, flags: CommandFlags.DemandReplica))
                        {
                            keys.Add(key);
                        }
                    }
                }

                return await Task.FromResult(keys);
            });
        }

        public async Task<bool> StoreAsync(string key, string value, TimeSpan expiresAt)
        {
            return await _policy.ExecuteAsync(async () =>
                await _writeDatabase.StringSetAsync(key, value, flags: CommandFlags.DemandMaster)
            );
        }

        public async Task<string> GetAsync(string key)
        {
            return await _policy.ExecuteAsync(async () =>
                await _readDatabase.StringGetAsync(key, CommandFlags.DemandReplica)
            );
        }

        public async Task<bool> RemoveAsync(string key)
        {
            return await _policy.ExecuteAsync(async () =>
                await _writeDatabase.KeyDeleteAsync(key)
            );
        }

        public async Task SubscribeAsync(string channel, Action<RedisChannel, RedisValue> handler)
        {
            await _policy.ExecuteAsync(async () =>
            {
                _subscriber.UnsubscribeAll();

                await _subscriber.SubscribeAsync(channel, handler);
            });
        }

        public async Task PublishAsync(string channel, string message)
        {
            await _policy.ExecuteAsync(async () =>
            {
                await _subscriber.PublishAsync(channel, message);
            });
        }

        protected Lazy<ConnectionMultiplexer> CreateMultiplexer(string connectionString)
        {
            var options = ConfigurationOptions.Parse(connectionString);

            options.AbortOnConnectFail = false;
            options.ReconnectRetryPolicy = new ExponentialRetry(5000);

            return new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options));
        }
    }
}