using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Polly;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Latency;
using Polly.Contrib.Simmy.Outcomes;
using Polly.Extensions.Http;
using Polly.Wrap;
using StackExchange.Redis;
using PollyResilience.Service;

namespace RedisPublisher
{
    public class Startup
    {
        protected static string baseDir = Directory.GetCurrentDirectory();
        protected IConfigurationRoot _configuration { get; }
        protected IPublisherConfiguration _publisherConfiguration;
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

            services.AddSingleton(_configuration.GetSection("ResiliencyConfiguration").Get<ResiliencyConfiguration>());

            services.AddSingleton<IPublisherConfiguration, PublisherConfiguration>();

            services.AddSingleton<IRedisClient, RedisClient>();

            services.AddTransient<IPollyResilienceService, PollyResilienceService>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog(Path.Combine(baseDir, "nlog.config"));
            });

            services.AddTransient<ConsoleApp>();

            var serviceProvider = services.BuildServiceProvider();

            _publisherConfiguration = serviceProvider.GetService<IPublisherConfiguration>();
            _logger = serviceProvider.GetService<ILogger<ConsoleApp>>();

            var retryPolicy = Policy.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(
                    retryCount: _publisherConfiguration.ResiliencyConfiguration.RetryCount,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(_publisherConfiguration.ResiliencyConfiguration.RetryDelayMilliseconds),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Information, $"Publisher error ({exception.GetType().ToString()}) on retry {retryCount} for {timeSpan.ToString()}", exception);
                    }
                );

            var fault = new SocketException(errorCode: 10013);

            var chaosPolicy = MonkeyPolicy.InjectExceptionAsync(with =>
                with.Fault(fault)
                    .InjectionRate(_publisherConfiguration.ResiliencyConfiguration.FaultRate)
                    .Enabled()
                );

            var chaosLatencyPolicy = MonkeyPolicy.InjectLatencyAsync(with =>
                with.Latency(TimeSpan.FromMilliseconds(_publisherConfiguration.ResiliencyConfiguration.LatencyMilliseconds))
                    .InjectionRate(_publisherConfiguration.ResiliencyConfiguration.LatencyInjectionRate)
                    .Enabled()
                );

            var redisPolicy = retryPolicy.WrapAsync(chaosPolicy).WrapAsync(chaosLatencyPolicy);

            services.AddSingleton<IAsyncPolicy>(redisPolicy);

            return services.BuildServiceProvider();
        }
    }
}
