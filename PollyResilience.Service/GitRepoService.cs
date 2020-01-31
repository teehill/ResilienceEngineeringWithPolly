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
        Task<RepositoryReadme> GetRepoReadme(Repository repo);
    }

    public class GitRepoService : IRepoService
    {
        private readonly HttpClient _reposHttpClient;
        private readonly IHttpClientFactory _httpClientFactory;

        public GitRepoService(HttpClient client, IHttpClientFactory httpClientFactory)
        {
            _reposHttpClient = client;
            _httpClientFactory = httpClientFactory;

        }

        public async Task<IEnumerable<Repository>> GetRepos()
        {
            var streamTask = _reposHttpClient.GetStreamAsync("orgs/dotnet/repos");

            return await JsonSerializer.DeserializeAsync<IEnumerable<Repository>>(await streamTask);
        }

        public async Task<RepositoryReadme> GetRepoReadme(Repository repo)
        {
            var client = _httpClientFactory.CreateClient("GitHubRepoClient");
            var streamTask = client.GetStreamAsync($"{repo.Url}/readme");

            return await JsonSerializer.DeserializeAsync<RepositoryReadme>(await streamTask);
        }
    }
}