using System;
using Microsoft.Extensions.DependencyInjection;

namespace PollyResilience.Console
{
    public class Program
    {
        static void Main(string[] args)
        {
            IServiceCollection services = new ServiceCollection();

            Startup startup = new Startup();
            startup.ConfigureServices(services);

            IServiceProvider serviceProvider = services.BuildServiceProvider();

            // entry to run app
            serviceProvider.GetService<ConsoleApp>().Run();
        }

        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}