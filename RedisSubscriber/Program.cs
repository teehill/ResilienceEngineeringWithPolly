using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RedisSubscriber
{
    public class Program
    {
        private static IServiceProvider _serviceProvider;

        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();

            var startup = new StartupWithRetry();

            _serviceProvider = startup.ConfigureServices(services);

            await _serviceProvider.GetService<ConsoleApp>().Run();
        }
    }
}