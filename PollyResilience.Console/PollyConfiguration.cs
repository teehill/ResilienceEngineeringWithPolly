using Microsoft.Extensions.Configuration;

namespace PollyResilience.Console
{
    public class PollyConfiguration : IPollyConfiguration
    {
        IConfigurationRoot _configurationRoot;
        public PollyConfiguration(IConfigurationRoot configurationRoot)
        {
            _configurationRoot = configurationRoot;
        }

        public string DependencyURL => _configurationRoot["DependencyURL"];
        public string NLogConfig => _configurationRoot["NLogConfig"];
    }

    public interface IPollyConfiguration
    {
        string DependencyURL { get; }
    }
}