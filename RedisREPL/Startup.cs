using System;
using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Polly;
using PollyResilience.Service;
using StackExchange.Redis;

namespace RedisREPL
{
    public class Startup
    {
        protected static string baseDir = Directory.GetCurrentDirectory();
        protected IConfigurationRoot _configuration { get; }

        protected ILogger<ConsoleApp> _logger;

        public Startup()
        {
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(baseDir);
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            _configuration = builder.Build();
        }


        public ServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_configuration);

            services.AddSingleton<IRedisClient, RedisSplitClient>();

            services.AddTransient<IPollyResilienceService, PollyResilienceService>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog(Path.Combine(baseDir, "nlog.config"));
                loggingBuilder.SetMinimumLevel(LogLevel.Trace);
            });

            services.AddTransient<ConsoleApp>();

            var serviceProvider = services.BuildServiceProvider();

            _logger = serviceProvider.GetService<ILogger<ConsoleApp>>();

            var retryPolicy = Policy.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(200),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Warning, $"REPL error ({exception.GetType().ToString()}) on retry {retryCount} for {timeSpan.ToString()}", exception);
                    }
                );

            services.AddSingleton<IAsyncPolicy>(retryPolicy);

            return services.BuildServiceProvider();
        }
    }
}
