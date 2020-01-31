using System;
using System.Text.Json.Serialization;

namespace PollyResilience.Service.Models
{
    public partial class RepositoryReadme
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("encoding")]
        public string Encoding { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; }

        [JsonPropertyName("url")]
        public Uri Url { get; set; }

        [JsonPropertyName("git_url")]
        public Uri GitUrl { get; set; }

        [JsonPropertyName("html_url")]
        public Uri HtmlUrl { get; set; }

        [JsonPropertyName("download_url")]
        public Uri DownloadUrl { get; set; }

        [JsonPropertyName("_links")]
        public Links Links { get; set; }
    }

    public partial class Links
    {
        [JsonPropertyName("git")]
        public Uri Git { get; set; }

        [JsonPropertyName("self")]
        public Uri Self { get; set; }

        [JsonPropertyName("html")]
        public Uri Html { get; set; }
    }
}
