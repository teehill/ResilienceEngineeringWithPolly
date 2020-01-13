using System;
using Microsoft.Extensions.Logging;

namespace PollyResilience.Console
{
    public class ConsoleApp
    {
        private readonly ILogger<ConsoleApp> _logger;
        private readonly IPollyConfiguration _config;

        public ConsoleApp(IPollyConfiguration configurationRoot, ILogger<ConsoleApp> logger)
        {
            _logger = logger;
            _config = configurationRoot;
        }

        public void Run()
        {
            var DependencyURL = _config.DependencyURL;

            _logger.LogCritical(DependencyURL);

            System.Console.ReadKey();
        }

        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}