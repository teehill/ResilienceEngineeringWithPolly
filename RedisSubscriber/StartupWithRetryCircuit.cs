using System;
using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Polly;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;
using PollyResilience.Service;
using StackExchange.Redis;

namespace RedisSubscriber
{
    public class StartupWithRetryCircuit
    {
        protected static string baseDir = Directory.GetCurrentDirectory();

        protected IConfigurationRoot _configuration { get; }

        public StartupWithRetryCircuit()
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

            services.AddTransient<IPollyResilienceService, PollyResilienceService>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog($"{baseDir}\\nlog.config");
            });

            services.AddTransient<ConsoleApp>();

            var logger = services.BuildServiceProvider().GetService<ILogger<ConsoleApp>>();

            var retryPolicy = Policy.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(1), (exception, timeSpan, retryCount, context) =>
                {
                    logger.Log(LogLevel.Error, $"Redis error on retry {retryCount} for {context.PolicyKey}", exception);
                });

            var circuitBreakerPolicy = Policy.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 1,
                    durationOfBreak: TimeSpan.FromSeconds(120),
                    onHalfOpen: () => { logger.Log(LogLevel.Information, $"Redis caching circuit breaker: half open"); },
                    onBreak: (exception, timespan) => { logger.Log(LogLevel.Information, $"Redis caching circuit breaker: open for {timespan.TotalSeconds} seconds"); },
                    onReset: () => { logger.Log(LogLevel.Information, $"Redis caching circuit breaker: closed"); }
            );

            var fault = new SocketException(errorCode: 10013);
            var chaosPolicy = MonkeyPolicy.InjectExceptionAsync(with =>
                with.Fault(fault)
                    .InjectionRate(.3)
                    .Enabled()
                );

            var pol = retryPolicy.WrapAsync(circuitBreakerPolicy)
                .WrapAsync(chaosPolicy);

            services.AddSingleton<IAsyncPolicy>(pol);

            return services.BuildServiceProvider();
        }
    }
}
