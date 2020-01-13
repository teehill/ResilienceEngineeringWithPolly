using Microsoft.Extensions.DependencyInjection;

namespace PollyResilience.Console
{
    public class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection();

            var startup = new Startup();

            var serviceProvider = startup.ConfigureServices(services);

            // entry to run app
            serviceProvider.GetService<ConsoleApp>().Run();
        }

        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}