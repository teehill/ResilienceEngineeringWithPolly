using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Wrap;
using StackExchange.Redis;

namespace PollyResilience.Service
{
    public class RedisClient : IRedisClient
    {
        protected readonly IConfigurationRoot _configuration;
        protected readonly string _connectionString;
        protected Lazy<ConnectionMultiplexer> _multiplexer;
        protected IDatabase _database;
        protected IServer _server;
        protected ISubscriber _subscriber;
        protected ILogger<RedisClient> _logger;
        protected readonly IAsyncPolicy _policy;
        protected readonly PolicyWrap<string> _retryCircuitStringFallback;

        public RedisClient(
            ILogger<RedisClient> logger,
            IConfigurationRoot configuration,
            IAsyncPolicy policy)
        {
            _logger = logger;
            _configuration = configuration;
            _policy = policy;
            _connectionString = _configuration["RedisConnectionString"];

            _multiplexer = CreateMultiplexer();
            _database = _multiplexer.Value.GetDatabase();

            _subscriber = _multiplexer.Value.GetSubscriber();
        }

        public async Task<List<string>> GetKeys(string query = "*")
        {
            return await _policy.ExecuteAsync(async () =>
            {
                var keys = new List<string>();

                foreach (var endpoint in _multiplexer.Value.GetEndPoints())
                {
                    var ipEndpoint = (DnsEndPoint)endpoint;
                    var hostAndPort = $"{ipEndpoint.Host}:{ipEndpoint.Port}";
                    var server = _multiplexer.Value.GetServer(hostAndPort);
                    //var server = _multiplexer.Value.GetServer(endpoint.ToString().Substring(12));
                    foreach (var key in  server.Keys(pattern: query))
                    {
                        keys.Add(key);
                    }
                }

                return await Task.FromResult(keys);
            });
        }

        public async Task<bool> StoreAsync(string key, string value, TimeSpan expiresAt)
        {
            return await _policy.ExecuteAsync(async () =>
                await _database.StringSetAsync(key, value)
            );
        }

        public async Task<string> GetAsync(string key)
        {
            return await _policy.ExecuteAsync(async () =>
                await _database.StringGetAsync(key)
            );
        }

        public async Task<bool> RemoveAsync(string key)
        {
            return await _policy.ExecuteAsync(async () =>
                await _database.KeyDeleteAsync(key)
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

        protected Lazy<ConnectionMultiplexer> CreateMultiplexer()
        {
            var options = ConfigurationOptions.Parse(_connectionString);

            options.AbortOnConnectFail = false;
            options.ReconnectRetryPolicy = new ExponentialRetry(5000);

            return new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options));
        }
    }
}