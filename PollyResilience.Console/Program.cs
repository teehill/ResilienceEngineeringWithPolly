using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace PollyResilience.Console
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();

            /*var services = new ServiceCollection();

            var startup = new Startup();

            var serviceProvider = startup.ConfigureServices(services);

            await serviceProvider.GetService<ConsoleApp>().Run();*/
        }

        public static IHostBuilder CreateHostBuilder(string[] args) => 
            Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });

        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}