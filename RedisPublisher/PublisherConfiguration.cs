using Microsoft.Extensions.Configuration;

namespace RedisPublisher
{
    public class PublisherConfiguration : IPublisherConfiguration
    {
        IConfigurationRoot _configurationRoot;
        public PublisherConfiguration(IConfigurationRoot configurationRoot)
        {
            _configurationRoot = configurationRoot;
        }
        public string NLogConfig => _configurationRoot["NLogConfig"];
    }

    public interface IPublisherConfiguration
    {
        string NLogConfig { get; }
    }
}