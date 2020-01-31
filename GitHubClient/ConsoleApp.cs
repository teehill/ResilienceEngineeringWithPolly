using Microsoft.Extensions.Logging;
using Polly;
using PollyResilience.Service;
using PollyResilience.Service.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GitHubClient
{
    public class ConsoleApp
    {
        protected readonly IPollyResilienceService _pollyResilienceService;
        protected readonly ILogger<ConsoleApp> _logger;

        public ConsoleApp(IPollyResilienceService pollyResilienceService,
            ILogger<ConsoleApp> logger)
        {
            _pollyResilienceService = pollyResilienceService;
            _logger = logger;
        }

        public async Task Run()
        {
            var policy = Policy<IEnumerable<Repository>>.Handle<Exception>()
                .FallbackAsync(
                    fallbackValue: null,
                    onFallbackAsync: async b =>
                    {
                        await Task.FromResult(true);
                        _logger.Log(LogLevel.Error, $"Fallback caught a '{b.Exception.GetType().ToString()}': '{b.Exception.Message}'");
                        return;
                    });

            var repos = await policy.ExecuteAsync(async () => {
                return await _pollyResilienceService.ProcessRepositories();
            });

            foreach (var repo in repos ?? Enumerable.Empty<Repository>())
            {
                var readme = await _pollyResilienceService.GetRepoReadme(repo);
                Console.WriteLine(readme.HtmlUrl);
            }
        }
    }
}