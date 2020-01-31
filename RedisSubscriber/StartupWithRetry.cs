using System;
using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Polly;
using StackExchange.Redis;
using PollyResilience.Service;

namespace RedisSubscriber
{
    public class StartupWithRetry
    {
        protected static string baseDir = Directory.GetCurrentDirectory();

        protected IConfigurationRoot _configuration { get; }

        public StartupWithRetry()
        {
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(baseDir);
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            _configuration = builder.Build();
        }


        public ServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_configuration);

            services.AddSingleton<ISubscriberConfiguration, SubscriberConfiguration>();

            services.AddSingleton<IRedisClient, RedisClient>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog(Path.Combine(baseDir, "nlog.config"));
            });

            services.AddTransient<ConsoleApp>();

            var logger = services.BuildServiceProvider().GetService<ILogger<ConsoleApp>>();

            var retryPolicy = Policy.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(1), (exception, timeSpan, retryCount, context) =>
                {
                    logger.Log(LogLevel.Error, $"Redis error on retry {retryCount} for {context.PolicyKey}", exception);
                });

            services.AddSingleton<IAsyncPolicy>(retryPolicy);

            return services.BuildServiceProvider();
        }
    }
}
