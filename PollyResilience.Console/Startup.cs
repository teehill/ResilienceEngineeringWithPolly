using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        protected IConfiguration _configuration { get; }

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
            /*var builder = new ConfigurationBuilder();
            builder.SetBasePath(baseDir);
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();*/
        }


        public ServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IConfiguration>(_configuration);

            services.AddSingleton<IPollyConfiguration, PollyConfiguration>(); 

            services.AddTransient<IPollyResilienceService, PollyResilienceService>();

            services.AddLogging(loggingBuilder => {
                loggingBuilder.AddNLog($"{baseDir}/nlog.config");
            });

            services.AddTransient<ConsoleApp>();

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>() // thrown by Polly's TimeoutPolicy if the inner execution times out
                .RetryAsync(3);

            services.AddHttpClient<RepoService>(c =>
            {
                c.BaseAddress = new Uri("https://api.github.com/");
                c.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                c.DefaultRequestHeaders.Add("User-Agent", "HttpClientFactory-Sample");
            });
            
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
