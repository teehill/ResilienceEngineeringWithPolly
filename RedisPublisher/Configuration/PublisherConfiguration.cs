using Microsoft.Extensions.Configuration;

namespace RedisPublisher
{
    public class PublisherConfiguration : IPublisherConfiguration
    {
        IConfigurationRoot _configurationRoot;
        public readonly ResiliencyConfiguration _resiliencyConfiguration;
        public readonly GitHubConfiguration _gitHubConfiguration;

        public PublisherConfiguration(IConfigurationRoot configurationRoot, 
            ResiliencyConfiguration resiliencyConfiguration,
            GitHubConfiguration gitHubConfiguration)
        {
            _configurationRoot = configurationRoot;
            _resiliencyConfiguration = resiliencyConfiguration;
            _gitHubConfiguration = gitHubConfiguration;
        }

        ResiliencyConfiguration IPublisherConfiguration.ResiliencyConfiguration => _resiliencyConfiguration;
        GitHubConfiguration IPublisherConfiguration.GitHubConfiguration => _gitHubConfiguration;
    }

    public interface IPublisherConfiguration
    {
        ResiliencyConfiguration ResiliencyConfiguration { get; }
        GitHubConfiguration GitHubConfiguration { get; }
    }
}