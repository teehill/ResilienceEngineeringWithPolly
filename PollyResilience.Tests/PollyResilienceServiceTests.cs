using FakeItEasy;
using PollyResilience.Service;
using System.Threading.Tasks;
using Xunit;

namespace PollyResilience.Tests
{
    public class PollyResilienceServiceTests
    {
        protected IRepoService _repoService;
        protected PollyResilienceService _resiliencyService;

        public PollyResilienceServiceTests()
        {
            _repoService = A.Fake<IRepoService>();
            _resiliencyService = new PollyResilienceService(_repoService);
        }


        [Fact]
        public async Task GetRepos_MustHaveHappened()
        {
            await _resiliencyService.ProcessRepositories();

            A.CallTo(() => _repoService.GetRepos()).MustHaveHappened();
        }
    }
}
