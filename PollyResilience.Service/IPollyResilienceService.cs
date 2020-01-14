using System.Collections.Generic;
using System.Threading.Tasks;
using PollyResilience.Service.Models;

namespace PollyResilience.Service
{
    public interface IPollyResilienceService
    {
        void HelloWorld();
        Task<IEnumerable<Repository>> ProcessRepositories();
    }
}
