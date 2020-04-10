using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace RedisREPL
{
    class Program
    {
        private static IServiceProvider _serviceProvider;

        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();

            var startup = new Startup();

            _serviceProvider = startup.ConfigureServices(services);

            await _serviceProvider.GetService<ConsoleApp>().Run();
        }
    }
}
