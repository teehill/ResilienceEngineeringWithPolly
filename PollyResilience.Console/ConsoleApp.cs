using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PollyResilience.Service;

namespace PollyResilience.Console
{
    public class ConsoleApp
    {
        protected readonly ILogger<ConsoleApp> _logger;
        protected readonly IPollyConfiguration _config;

        protected readonly IPollyResilienceService _pollyService;

        public ConsoleApp(IPollyConfiguration configurationRoot, ILogger<ConsoleApp> logger, IPollyResilienceService pollyService)
        {
            _logger = logger;
            _config = configurationRoot;
            _pollyService = pollyService;
        }

        public async Task Run()
        {
            var repositories = await _pollyService.ProcessRepositories();

            System.Console.WriteLine("Repos motherfuckers!");
            System.Console.WriteLine("----------------------------------------------");

            foreach (var repo in repositories) {
                System.Console.WriteLine($"{repo.Name} | {repo.Description}");
                System.Console.WriteLine($"{repo.GitHubHomeUrl} | {repo.Homepage}");
                System.Console.WriteLine($"{repo.Watchers} | {repo.LastPush}");
                System.Console.WriteLine("----------------------------------------------");
            }

            System.Console.ReadKey();
        }

        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}