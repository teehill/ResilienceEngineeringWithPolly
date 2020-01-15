using System;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using PollyResilience.Service;

namespace PollyResilience.Console
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

            services.AddSingleton<IPollyConfiguration, PollyConfiguration>();

            services.AddTransient<IPollyResilienceService, PollyResilienceService>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog($"{baseDir}/nlog.config");
            });

            services.AddTransient<ConsoleApp>();

            var logger = services.BuildServiceProvider().GetService<ILogger<ConsoleApp>>();

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(1), (exception, timeSpan, retryCount, context) =>
                {
                    logger.Log(LogLevel.Error, $"Redis error on retry {retryCount} for {context.PolicyKey}", exception);
                });

            var circuitBreaker = HttpPolicyExtensions.HandleTransientHttpError()
                .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking: 1,
                    durationOfBreak: TimeSpan.FromSeconds(120),
                    onHalfOpen: () => { logger.Log(LogLevel.Information, "Git repo http client breaker: half open"); },
                    onBreak: (ex, ts) => { logger.Log(LogLevel.Information, $"Git repo http client circuit breaker: open for {ts.TotalSeconds} seconds"); },
                    onReset: () => { logger.Log(LogLevel.Information, $"Git repo http client circuit breaker: closed"); }
                );

            services.AddHttpClient<IRepoService,RepoService>(client =>
            {
                client.BaseAddress = new Uri("https://api.github.com/");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                client.DefaultRequestHeaders.Add("User-Agent", "HttpClientFactory-Sample");
            }).AddPolicyHandler(retryPolicy).AddPolicyHandler(circuitBreaker);


            var timeout = Policy.TimeoutAsync<HttpResponseMessage>(
                TimeSpan.FromSeconds(10));
            var longTimeout = Policy.TimeoutAsync<HttpResponseMessage>(
                TimeSpan.FromSeconds(30));

            var registry = services.AddPolicyRegistry();

            registry.Add("regular", timeout);
            registry.Add("long", longTimeout);

            services.AddHttpClient("multiplepolicies")
                .AddTransientHttpErrorPolicy(p => p.RetryAsync(3))
                .AddTransientHttpErrorPolicy(
                    p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

            return services.BuildServiceProvider();
        }
    }
}
