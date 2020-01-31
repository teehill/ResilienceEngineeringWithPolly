using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using PollyResilience.Service;

namespace GitHubClient
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

            services.AddSingleton(_configuration.GetSection("GitHubConfiguration").Get<GitHubConfiguration>());

            services.AddTransient<IPollyResilienceService, PollyResilienceService>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog($"{baseDir}\\nlog.config");
            });

            services.AddTransient<ConsoleApp>();

            var serviceProvider = services.BuildServiceProvider();

            var _gitHubConfiguration = serviceProvider.GetService<GitHubConfiguration>();
            _logger = serviceProvider.GetService<ILogger<ConsoleApp>>();


            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<SocketException>()
                .WaitAndRetryAsync(retryCount: _gitHubConfiguration.RetryCount,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(_gitHubConfiguration.RetryDelayMilliseconds),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, $"Git repo http error on retry {retryCount} for {context.PolicyKey}", exception);
                    }
                );

            var circuitBreaker = HttpPolicyExtensions.HandleTransientHttpError()
                .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking: _gitHubConfiguration.CircuitBreakerThreshold,
                    durationOfBreak: TimeSpan.FromMilliseconds(_gitHubConfiguration.CircuitBreakerDurationMilliseconds),
                    onHalfOpen: () => { _logger.Log(LogLevel.Information, "Git repo http client breaker: half open"); },
                    onBreak: (ex, ts) => { _logger.Log(LogLevel.Information, $"Git repo http client circuit breaker: open for {ts.TotalSeconds} seconds"); },
                    onReset: () => { _logger.Log(LogLevel.Information, $"Git repo http client circuit breaker: closed"); }
                );

            var githubPolicy = retryPolicy.WrapAsync(circuitBreaker);

            services.AddHttpClient<IRepoService, GitRepoService>(client =>
            {
                client.BaseAddress = new Uri(_gitHubConfiguration.BaseAPIPath);
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            }).AddPolicyHandler(githubPolicy);

            services.AddHttpClient("GitHubRepoClient", client => 
            {
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            }).AddTransientHttpErrorPolicy(p => p.RetryAsync(_gitHubConfiguration.RetryDelayMilliseconds))
                .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(
                    _gitHubConfiguration.CircuitBreakerThreshold,
                    TimeSpan.FromMilliseconds(_gitHubConfiguration.CircuitBreakerDurationMilliseconds)));

            return services.BuildServiceProvider();
        }
    }
}
