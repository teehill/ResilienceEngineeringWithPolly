using Microsoft.Extensions.Configuration;

namespace RedisSubscriber
{
    public class SubscriberConfiguration : ISubscriberConfiguration
    {
        readonly IConfigurationRoot _configurationRoot;
        public SubscriberConfiguration(IConfigurationRoot configurationRoot)
        {
            _configurationRoot = configurationRoot;
        }

        public string DependencyURL => _configurationRoot["DependencyURL"];
        public string NLogConfig => _configurationRoot["NLogConfig"];
    }

    public interface ISubscriberConfiguration
    {
        string DependencyURL { get; }
        string NLogConfig { get; }
    }
}