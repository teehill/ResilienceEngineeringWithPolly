using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using PollyResilience.Service;

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

            services.AddSingleton<ISubscriberConfiguration, SubscriberConfiguration>();

            services.AddSingleton<IRedisClient, RedisClient>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog(Path.Combine(baseDir, "nlog.config"));
            });

            services.AddTransient<ConsoleApp>();

            var logger = services.BuildServiceProvider().GetService<ILogger<ConsoleApp>>();

            return services.BuildServiceProvider();
        }
    }
}
