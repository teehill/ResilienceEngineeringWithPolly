using System;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Contrib.Simmy;
using PollyResilience.Service;
using Polly.Contrib.Simmy.Outcomes;

namespace RedisSubscriber
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

            services.AddSingleton<IRedisClient, RedisClient>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog(_configuration["NLogConfig"]);
            });

            services.AddTransient<ConsoleApp>();

            var logger = services.BuildServiceProvider().GetService<ILogger<ConsoleApp>>();

            return services.BuildServiceProvider();
        }
    }
}
