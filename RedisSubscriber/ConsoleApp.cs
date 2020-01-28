using Microsoft.Extensions.Logging;
using PollyResilience.Service;
using System;
using System.Threading.Tasks;

namespace RedisSubscriber
{
    public class ConsoleApp
    {
        protected readonly ILogger<ConsoleApp> _logger;
        protected readonly ISubscriberConfiguration _config;
        protected readonly IRedisClient _redisClient;

        public ConsoleApp(ISubscriberConfiguration configurationRoot,
            ILogger<ConsoleApp> logger,
            IRedisClient redisClient)
        {
            _logger = logger;
            _config = configurationRoot;
            _redisClient = redisClient;
        }

        public async Task Run()
        {
            await _redisClient.SubscribeAsync("messages", async (message, value) =>
            {
                Console.Clear();

                Console.Write(value.ToString());
                //await Task.Delay(500);
            });

            Console.ReadLine();
        }

        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}