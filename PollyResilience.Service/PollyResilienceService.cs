using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using PollyResilience.Service.Models;

namespace PollyResilience.Service
{
    public class PollyResilienceService : IPollyResilienceService    {
        private static readonly HttpClient client = new HttpClient();

        public PollyResilienceService()
        {

        }

        public void HelloWorld()
        {
            
        }

        public async Task<IEnumerable<Repository>> ProcessRepositories()
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

            var streamTask = client.GetStreamAsync("https://api.github.com/orgs/dotnet/repos");
            var repositories = await JsonSerializer.DeserializeAsync<IEnumerable<Repository>>(await streamTask);

            return repositories;
        }
    }
}
