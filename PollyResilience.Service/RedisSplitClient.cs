
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Wrap;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

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

        protected readonly string _fireForgetKey = "FireForgetMode";
        protected readonly string _readModeKey = "ReadMode";
        protected readonly string _writeModeKey = "WriteMode";
        protected readonly string _readConfigKey = "RedisReadConnectionString";
        protected readonly string _writeConfigKey = "RedisWriteConnectionString";

        protected CommandFlags _readFlags;
        protected CommandFlags _writeFlags;

        private readonly System.IO.StringWriter logwriter = new System.IO.StringWriter();

        public RedisSplitClient(
            ILogger<RedisClient> logger,
            IConfigurationRoot configuration,
            IAsyncPolicy policy)
        {
            _logger = logger;
            _configuration = configuration;
            _policy = policy;
            _writeConnectionString = _configuration[_writeConfigKey];
            _readConnectionString = _configuration[_readConfigKey];


            bool fireAndForget = true;
            bool.TryParse(_configuration[_fireForgetKey], out fireAndForget);

            var readMode = _configuration[_readModeKey];
            var writeMode = _configuration[_writeModeKey];

            if (!string.IsNullOrEmpty(readMode))
            {
                _readFlags = (CommandFlags)Enum.Parse(typeof(CommandFlags), readMode, true);
            }

            if (!string.IsNullOrEmpty(writeMode))
            {
                var writeFlag = (CommandFlags)Enum.Parse(typeof(CommandFlags), writeMode, true);

                _writeFlags = (fireAndForget) ? writeFlag | CommandFlags.FireAndForget : writeFlag;
            }

            _writeMultiplexer = CreateMultiplexer(_writeConnectionString);
            _readMultiplexer = CreateMultiplexer(_readConnectionString);

            _writeDatabase = _writeMultiplexer.Value.GetDatabase();
            _readDatabase = _readMultiplexer.Value.GetDatabase();


            var connectionLogs = logwriter.ToString();
            _logger.LogDebug(connectionLogs);
            logwriter.Flush();
        }

        public async Task<List<string>> GetKeys(string query = "*")
        {
            return await _policy.ExecuteAsync(async () =>
            {
                var keys = new List<string>();

                //can't issue SCAN against replica (so using write multiplexer)
                foreach (var endpoint in _writeMultiplexer.Value.GetEndPoints())
                {
                    var hostAndPort = endpoint.ToString().Replace($"{endpoint.AddressFamily}/", string.Empty);

                    var server = _writeMultiplexer.Value.GetServer(hostAndPort);

                    if (server.IsConnected)
                    {
                        _logger.LogDebug($"Querying: {hostAndPort}");

                        foreach (var key in server.Keys(pattern: query, flags: _readFlags))
                        {
                            keys.Add(key);
                        }
                    }
                }

                _logger.LogDebug(logwriter.ToString());
                logwriter.Flush();

                return await Task.FromResult(keys);
            });
        }

        public async Task<bool> StoreAsync(string key, string value, TimeSpan expiresAt)
        {
            _logger.LogDebug($"Store {_writeMultiplexer.Value.GetStatus()}");

            return await _policy.ExecuteAsync(async () =>
                await _writeDatabase.StringSetAsync(key, value, flags: _writeFlags)
            );
        }

        public async Task<string> GetAsync(string key)
        {
            _logger.LogDebug($"Get {_readMultiplexer.Value.GetStatus()}");

            return await _policy.ExecuteAsync(async () =>
                await _readDatabase.StringGetAsync(key, _readFlags)
            );
        }

        public async Task<bool> RemoveAsync(string key)
        {
            _logger.LogDebug($"Remove {_writeMultiplexer.Value.GetStatus()}");

            return await _policy.ExecuteAsync(async () =>
                await _writeDatabase.KeyDeleteAsync(key, flags: _writeFlags)
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
            options.AllowAdmin = true;
            options.CertificateValidation += CheckServerCertificate;

            return new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options, logwriter));
        }

        private static bool CheckServerCertificate(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}