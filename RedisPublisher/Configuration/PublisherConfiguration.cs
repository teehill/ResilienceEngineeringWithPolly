using Microsoft.Extensions.Configuration;

namespace RedisPublisher
{
    public class PublisherConfiguration : IPublisherConfiguration
    {
        IConfigurationRoot _configurationRoot;
        public readonly ResiliencyConfiguration _resiliencyConfiguration;

        public PublisherConfiguration(IConfigurationRoot configurationRoot, 
            ResiliencyConfiguration resiliencyConfiguration)
        {
            _configurationRoot = configurationRoot;
            _resiliencyConfiguration = resiliencyConfiguration;
        }

        ResiliencyConfiguration IPublisherConfiguration.ResiliencyConfiguration => _resiliencyConfiguration;
    }

    public interface IPublisherConfiguration
    {
        ResiliencyConfiguration ResiliencyConfiguration { get; }
    }
}