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
using Polly.Contrib.Simmy.Outcomes;
using Polly.Contrib.Simmy.Latency;
using Polly.Extensions.Http;
using PollyResilience.Service;
using StackExchange.Redis;

namespace RedisPublisher
{
    public class Startup
    {
        protected static string baseDir = Directory.GetCurrentDirectory();
        protected IConfigurationRoot _configuration { get; }

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

            services.AddSingleton<IPublisherConfiguration, PublisherConfiguration>();

            services.AddSingleton<IRedisClient, RedisClient>();

            services.AddTransient<IPollyResilienceService, PollyResilienceService>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog(_configuration["NLogConfig"]);
            });

            services.AddTransient<ConsoleApp>();

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<ConsoleApp>>();

            var resiliencyConfiguration = _configuration.GetSection("ResiliencyConfiguration").Get<ResiliencyConfiguration>();

            var retryPolicy = Policy.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(
                    resiliencyConfiguration.RetryCount, 
                    retryAttempt => TimeSpan.FromMilliseconds(resiliencyConfiguration.RetryDelayMilliseconds),
                    (exception, timeSpan, retryCount, context) =>
                {
                    logger.Log(LogLevel.Information, $"Publisher error ({exception.GetType().ToString()}) on retry {retryCount} for {timeSpan.ToString()}", exception);
                });

            var fault = new SocketException(errorCode: 10013);

            var chaosPolicy = MonkeyPolicy.InjectExceptionAsync(with => 
                with.Fault(fault)
                    .InjectionRate(resiliencyConfiguration.FaultRate)
                    .Enabled()
                );

            var chaosLatencyPolicy = MonkeyPolicy.InjectLatencyAsync(with =>
                with.Latency(TimeSpan.FromMilliseconds(resiliencyConfiguration.LatencyMilliseconds))
                    .InjectionRate(resiliencyConfiguration.LatencyInjectionRate)
                    .Enabled()
                );

            var policy = retryPolicy.WrapAsync(chaosPolicy).WrapAsync(chaosLatencyPolicy);

            services.AddSingleton<IAsyncPolicy>(policy);

            return services.BuildServiceProvider();
        }
    }

    public class ResiliencyConfiguration
    {
        public int RetryCount { get; set; }
        public int RetryDelayMilliseconds {get; set;}
        public double FaultRate {get; set;}

        public int LatencyMilliseconds { get; set; }

        public double LatencyInjectionRate { get; set; }
    }
}
