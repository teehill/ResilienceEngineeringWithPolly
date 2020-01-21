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
        protected readonly IRedisClient _redisClient;

        public ConsoleApp(IPollyConfiguration configurationRoot,
            ILogger<ConsoleApp> logger,
            IPollyResilienceService pollyService,
            IRedisClient redisClient)
        {
            _logger = logger;
            _config = configurationRoot;
            _pollyService = pollyService;
            _redisClient = redisClient;
        }

        public async Task Run()
        {
            _redisClient.Subscribe("repos");

            var repositories = await _pollyService.ProcessRepositories();

            _logger.LogInformation("Repositories");
            _logger.LogInformation("----------------------------------------------");

            foreach (var repo in repositories) {
                _logger.LogInformation($"{repo.Name} | {repo.Description}");
                _logger.LogInformation($"{repo.GitHubHomeUrl} | {repo.Homepage}");
                _logger.LogInformation($"{repo.Watchers} | {repo.LastPush}");
                _logger.LogInformation("----------------------------------------------");

                _redisClient.Publish("repos", repo.GitHubHomeUrl.ToString());
            }



            System.Console.ReadKey();
        }

        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}