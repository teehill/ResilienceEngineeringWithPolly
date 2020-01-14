using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PollyResilience.Service
{    public class RepoService
    {
        private readonly HttpClient _httpClient;

        public RepoService(HttpClient client)
        {
            _httpClient = client;
        }

        public async Task<IEnumerable<string>> GetRepos()
        {
            var response = await _httpClient.GetAsync("aspnet/repos");

            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync
                <IEnumerable<string>>(responseStream);
        }
    }
}