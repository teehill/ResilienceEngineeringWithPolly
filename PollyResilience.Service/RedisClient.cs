using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Wrap;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace PollyResilience.Service
{
    public class RedisClient : IRedisClient
    {
        protected readonly IConfigurationRoot _configuration;
        protected readonly string _connectionString;
        protected Lazy<ConnectionMultiplexer> _multiplexer;
        protected IDatabase _database;
        protected ISubscriber _subscriber;
        protected ILogger<RedisClient> _logger;
        protected readonly IAsyncPolicy _policy;
        protected readonly PolicyWrap<string> _retryCircuitStringFallback;

        public RedisClient(ILogger<RedisClient> logger, IConfigurationRoot configuration, IAsyncPolicy policy)
        {
            _logger = logger;
            _configuration = configuration;
            _policy = policy;
            _connectionString = _configuration["RedisConnectionString"];

            _multiplexer = CreateMultiplexer();

            _database = _multiplexer.Value.GetDatabase();

            _subscriber = _multiplexer.Value.GetSubscriber();
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
            _subscriber.UnsubscribeAll();

            await _subscriber.SubscribeAsync(channel, handler);
        }

        public async Task PublishAsync(string channel, string message)
        {
            await _subscriber.PublishAsync(channel, message);
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