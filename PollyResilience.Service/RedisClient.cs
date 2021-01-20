using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
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
            IAsyncPolicy policy,
            string configKey = "RedisConnectionString")
        {
            _logger = logger;
            _configuration = configuration;
            _policy = policy;
            _connectionString = _configuration[configKey];

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
                    var ipEndpoint = (IPEndPoint)endpoint;
                    var hostAndPort = $"{ipEndpoint.Address}:{ipEndpoint.Port}";
                    var server = _multiplexer.Value.GetServer(hostAndPort);

                    if (server.IsConnected)
                    {
                        foreach (var key in server.Keys(pattern: query, flags: CommandFlags.PreferReplica))
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
                await _database.StringSetAsync(key, value, flags: CommandFlags.DemandMaster)
            );
        }

        public async Task<string> GetAsync(string key)
        {
            return await _policy.ExecuteAsync(async () =>
                await _database.StringGetAsync(key, CommandFlags.PreferReplica)
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
            options.AllowAdmin = true;
            options.ReconnectRetryPolicy = new ExponentialRetry(5000);
            options.CertificateValidation += CheckServerCertificate;

            return new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options));
        }

        private static bool CheckServerCertificate(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        //reconnecting variables
        protected object _reconnectLock = new object();
        protected long _lastReconnectTicks = DateTimeOffset.MinValue.UtcTicks;
        protected DateTimeOffset _firstErrorAfterReconnect = DateTimeOffset.MinValue;
        protected DateTimeOffset _previousError = DateTimeOffset.MinValue;
        // In general, let StackExchange.Redis handle most reconnects, 
        // so limit the frequency of how often this will actually reconnect.
        protected static TimeSpan s_reconnectMinFrequency = TimeSpan.FromSeconds(60);
        // if errors continue for longer than the below threshold, then the 
        // multiplexer seems to not be reconnecting, so re-create the multiplexer
        protected TimeSpan _reconnectErrorThreshold = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Force a new ConnectionMultiplexer to be created.  
        /// NOTES: 
        ///     1. Users of the ConnectionMultiplexer MUST handle ObjectDisposedExceptions, which can now happen as a result of calling ForceReconnect()
        ///     2. Don't call ForceReconnect for Timeouts, just for RedisConnectionExceptions or SocketExceptions
        ///     3. Call this method every time you see a connection exception, the code will wait to reconnect:
        ///         a. for at least the "ReconnectErrorThreshold" time of repeated errors before actually reconnecting
        ///         b. not reconnect more frequently than configured in "ReconnectMinFrequency"
        /// </summary>    
        public void ForceReconnect()
        {
            var utcNow = DateTimeOffset.UtcNow;
            var previousTicks = Interlocked.Read(ref _lastReconnectTicks);
            var previousReconnect = new DateTimeOffset(previousTicks, TimeSpan.Zero);
            var elapsedSinceLastReconnect = utcNow - previousReconnect;

            if (elapsedSinceLastReconnect > s_reconnectMinFrequency)
            {
                lock (_reconnectLock)
                {
                    utcNow = DateTimeOffset.UtcNow;
                    elapsedSinceLastReconnect = utcNow - previousReconnect;

                    if (_firstErrorAfterReconnect == DateTimeOffset.MinValue)
                    {
                        _firstErrorAfterReconnect = utcNow;
                        _previousError = utcNow;
                        return;
                    }

                    if (elapsedSinceLastReconnect < s_reconnectMinFrequency)
                        return; // Some other thread made it through the check and the lock, so nothing to do.

                    var elapsedSinceFirstError = utcNow - _firstErrorAfterReconnect;
                    var elapsedSinceMostRecentError = utcNow - _previousError;

                    var shouldReconnect =
                        elapsedSinceFirstError >= _reconnectErrorThreshold   // make sure we gave the multiplexer enough time to reconnect on its own if it can
                        && elapsedSinceMostRecentError <= _reconnectErrorThreshold; //make sure we aren't working on stale data (e.g. if there was a gap in errors, don't reconnect yet).

                    _previousError = utcNow;

                    if (shouldReconnect)
                    {
                        _logger?.Log(LogLevel.Warning, $"Redis force reconnect at {utcNow.ToString()}, firstError at {_firstErrorAfterReconnect.ToString()}, previousError at {_previousError.ToString()}, lastConnect at {_lastReconnectTicks.ToString()}");

                        _firstErrorAfterReconnect = DateTimeOffset.MinValue;
                        _previousError = DateTimeOffset.MinValue;

                        var oldMultiplexer = _multiplexer;
                        CloseMultiplexer(oldMultiplexer);
                        _multiplexer = CreateMultiplexer();
                        Interlocked.Exchange(ref _lastReconnectTicks, utcNow.UtcTicks);
                    }
                    else
                    {
                        _logger?.Log(LogLevel.Warning, string.Format("Redis force reconnect delay due to current min frequency: {0}s, lastConnect at {1:hh\\:mm\\:ss}",
                            s_reconnectMinFrequency.TotalSeconds, previousReconnect));
                    }
                }
            }
        }

        protected void CloseMultiplexer(Lazy<ConnectionMultiplexer> oldMultiplexer)
        {
            if (oldMultiplexer?.Value != null)
            {
                try
                {
                    oldMultiplexer.Value.Close();
                }
                catch (Exception exception)
                {
                    _logger?.Log(LogLevel.Error, $"Redis error encountered while closing multiplexer", exception);
                }
            }
        }
    }
}