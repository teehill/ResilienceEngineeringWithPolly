using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog.Extensions.Logging;
using PollyResilience.Service;

namespace PollyResilience.Console
{
    public class Startup
    {
        protected static string baseDir = Directory.GetCurrentDirectory();

        IConfigurationRoot Configuration { get; }

        public Startup()
        {
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(baseDir);
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();
        }


        public ServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IConfigurationRoot>(Configuration);

            services.AddSingleton<IPollyConfiguration, PollyConfiguration>(); 

            services.AddTransient<IPollyResilienceService, PollyResilienceService>();

            services.AddLogging(loggingBuilder => {
                loggingBuilder.AddNLog($"{baseDir}/nlog.config");
            });

            services.AddTransient<ConsoleApp>();

            return services.BuildServiceProvider();
        }
    }
}
