using System.Collections.Generic;
using System.Threading.Tasks;
using PollyResilience.Service.Models;

namespace PollyResilience.Service
{
    public class PollyResilienceService : IPollyResilienceService
    {
        private readonly IRepoService _repoService;

        public PollyResilienceService(IRepoService repoService) =>
            _repoService = repoService;

        public async Task<IEnumerable<Repository>> ProcessRepositories()
        {
            return await _repoService.GetRepos();
        }
    }
}
