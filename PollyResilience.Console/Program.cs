using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace PollyResilience.Console
{
    public class Program
    {
        private static IServiceProvider _serviceProvider;

        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();

            var startup = new Startup();

            _serviceProvider = startup.ConfigureServices(services);

            await _serviceProvider.GetService<ConsoleApp>().Run();
        }



        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}