using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
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
        protected ISubscriber _subscriber; 
        protected ILogger<RedisClient> _logger;
        protected readonly PolicyWrap<bool> _retryCircuitBoolFallback;
        protected readonly PolicyWrap<string> _retryCircuitStringFallback;

        public RedisClient(ILogger<RedisClient> logger, IConfigurationRoot configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionString = _configuration["RedisConnectionString"];

            _multiplexer = CreateMultiplexer();

            _database = _multiplexer.Value.GetDatabase();

            _subscriber = _multiplexer.Value.GetSubscriber();

            var redisBreaker = Policy.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .CircuitBreaker(
                    exceptionsAllowedBeforeBreaking: 1,
                    durationOfBreak: TimeSpan.FromSeconds(120),
                    onHalfOpen: () => { _logger?.Log(LogLevel.Information, $"Redis caching circuit breaker: half open"); },
                    onBreak: (exception, timespan) => { _logger?.Log(LogLevel.Information, $"Redis caching circuit breaker: open for {timespan.TotalSeconds} seconds"); },
                    onReset: () => { _logger?.Log(LogLevel.Information, $"Redis caching circuit breaker: closed"); }
            );

            var redisErrorRetryPolicy = Policy.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(1), (exception, timeSpan, retryCount, context) =>
                {
                    _logger.Log(LogLevel.Error, $"Redis error on retry {retryCount} for {context.PolicyKey}", exception);
                }
            );

            var retryCircuitPolicy = Policy.Wrap(redisBreaker, redisErrorRetryPolicy);

            var redisFallbackPolicyBool = Policy<bool>.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .Or<BrokenCircuitException>()
                .Fallback(false);

            _retryCircuitBoolFallback = redisFallbackPolicyBool.Wrap(retryCircuitPolicy);

            var redisFallbackPolicyString = Policy<string>.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .Or<BrokenCircuitException>()
                .Fallback(fallbackValue: "X");

            _retryCircuitStringFallback = redisFallbackPolicyString.Wrap(retryCircuitPolicy);
        }

        public bool Store(string key, string value, TimeSpan expiresAt)
        {
            return _retryCircuitBoolFallback.Execute(() => 
                _database.StringSet(key, value)
            );
        }
        
        public string Get(string key)
        {
            return _retryCircuitStringFallback.Execute(() =>
                _database.StringGet(key)
            );
        }

        public bool Remove(string key)
        {
            return _retryCircuitBoolFallback.Execute(() =>
                _database.KeyDelete(key)
            );
        }

        public void Subscribe(string channel)
        {
            _subscriber.Subscribe(channel).OnMessage(channelMessage => {
                Console.WriteLine(channelMessage.Message);
            });
        }

        public void Publish(string channel, string message)
        {
            _subscriber.Publish(channel, message);
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