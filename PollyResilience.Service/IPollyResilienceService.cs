using System.Collections.Generic;
using System.Threading.Tasks;
using PollyResilience.Service.Models;

namespace PollyResilience.Service
{
    public interface IPollyResilienceService
    {
        Task<IEnumerable<Repository>> ProcessRepositories();
    }
}
