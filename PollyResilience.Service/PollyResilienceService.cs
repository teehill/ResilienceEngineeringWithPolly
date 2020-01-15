using PollyResilience.Service.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PollyResilience.Service
{
    public class PollyResilienceService : IPollyResilienceService
    {
        private readonly IRepoService _repoService;

        public PollyResilienceService(IRepoService repoService) =>
            _repoService = repoService;

        public void HelloWorld()
        {

        }

        public async Task<IEnumerable<Repository>> ProcessRepositories()
        {
            return await _repoService.GetRepos();
        }
    }
}
