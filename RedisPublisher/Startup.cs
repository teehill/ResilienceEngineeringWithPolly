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
            services.AddSingleton(_configuration.GetSection("GitHubConfiguration").Get<GitHubConfiguration>());

            services.AddSingleton<IPublisherConfiguration, PublisherConfiguration>();

            services.AddSingleton<IRedisClient, RedisClient>();

            services.AddTransient<IPollyResilienceService, PollyResilienceService>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog($"{baseDir}\\nlog.config");
            });

            services.AddTransient<ConsoleApp>();

            var serviceProvider = services.BuildServiceProvider();

            var pubConfig = serviceProvider.GetService<IPublisherConfiguration>();
            var logger = serviceProvider.GetService<ILogger<ConsoleApp>>();

            var retryPolicy = Policy.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(
                    retryCount: pubConfig.ResiliencyConfiguration.RetryCount,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(pubConfig.ResiliencyConfiguration.RetryDelayMilliseconds),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        logger.Log(LogLevel.Information, $"Publisher error ({exception.GetType().ToString()}) on retry {retryCount} for {timeSpan.ToString()}", exception);
                    }
                );

            var fault = new SocketException(errorCode: 10013);

            var chaosPolicy = MonkeyPolicy.InjectExceptionAsync(with =>
                with.Fault(fault)
                    .InjectionRate(pubConfig.ResiliencyConfiguration.FaultRate)
                    .Enabled()
                );

            var chaosLatencyPolicy = MonkeyPolicy.InjectLatencyAsync(with =>
                with.Latency(TimeSpan.FromMilliseconds(pubConfig.ResiliencyConfiguration.LatencyMilliseconds))
                    .InjectionRate(pubConfig.ResiliencyConfiguration.LatencyInjectionRate)
                    .Enabled()
                );

            var redisPolicy = retryPolicy.WrapAsync(chaosPolicy).WrapAsync(chaosLatencyPolicy);

            services.AddSingleton<IAsyncPolicy>(redisPolicy);

            var githubPolicy = SetupGithubPolicy(logger, pubConfig);

            services.AddHttpClient<IRepoService, GitRepoService>(client =>
            {
                client.BaseAddress = new Uri(pubConfig.GitHubConfiguration.BaseAPIPath);
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                client.DefaultRequestHeaders.Add("User-Agent", "HttpClientFactory-Sample");
            }).AddPolicyHandler(githubPolicy);

            return services.BuildServiceProvider();
        }

        private AsyncPolicyWrap<HttpResponseMessage> SetupGithubPolicy(ILogger<ConsoleApp> logger, IPublisherConfiguration pubConfig)
        {
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<SocketException>()
                .WaitAndRetryAsync(retryCount: pubConfig.GitHubConfiguration.RetryCount,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(pubConfig.GitHubConfiguration.RetryDelayMilliseconds),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        logger.Log(LogLevel.Error, $"Git repo http error on retry {retryCount} for {context.PolicyKey}", exception);
                    }
                );

            var circuitBreaker = HttpPolicyExtensions.HandleTransientHttpError()
                .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking: 1,
                    durationOfBreak: TimeSpan.FromSeconds(120),
                    onHalfOpen: () => { logger.Log(LogLevel.Information, "Git repo http client breaker: half open"); },
                    onBreak: (ex, ts) => { logger.Log(LogLevel.Information, $"Git repo http client circuit breaker: open for {ts.TotalSeconds} seconds"); },
                    onReset: () => { logger.Log(LogLevel.Information, $"Git repo http client circuit breaker: closed"); }
                );

            return retryPolicy.WrapAsync(circuitBreaker);
        }
    }
}
