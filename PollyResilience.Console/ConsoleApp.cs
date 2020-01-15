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

        public ConsoleApp(IPollyConfiguration configurationRoot,
            ILogger<ConsoleApp> logger,
            IPollyResilienceService pollyService)
        {
            _logger = logger;
            _config = configurationRoot;
            _pollyService = pollyService;
        }

        public async Task Run()
        {
            var repositories = await _pollyService.ProcessRepositories();

            _logger.LogInformation("Repos");
            _logger.LogInformation("----------------------------------------------");

            foreach (var repo in repositories) {
                _logger.LogInformation($"{repo.Name} | {repo.Description}");
                _logger.LogInformation($"{repo.GitHubHomeUrl} | {repo.Homepage}");
                _logger.LogInformation($"{repo.Watchers} | {repo.LastPush}");
                _logger.LogInformation("----------------------------------------------");
            }

            System.Console.ReadKey();
        }

        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}