using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using PollyResilience.Service.Models;

namespace PollyResilience.Service
{
    public interface IRepoService
    {
        Task<IEnumerable<Repository>> GetRepos();
    }

    public class GitRepoService : IRepoService
    {
        private readonly HttpClient _httpClient;

        public GitRepoService(HttpClient client)
        {
            _httpClient = client;
        }

        public async Task<IEnumerable<Repository>> GetRepos()
        {
            var streamTask = _httpClient.GetStreamAsync("orgs/dotnet/repos");

            return await JsonSerializer.DeserializeAsync<IEnumerable<Repository>>(await streamTask);

        }
    }
}