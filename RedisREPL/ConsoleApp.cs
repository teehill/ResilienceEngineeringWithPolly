using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Behavior;
using Polly.Wrap;
using StackExchange.Redis;
using PollyResilience.Service;
using System;

namespace RedisREPL
{
    public class ConsoleApp
    {
        protected readonly IRedisClient _redisClient;
        protected readonly ILogger<ConsoleApp> _logger;

        public ConsoleApp(IRedisClient redisClient,
            ILogger<ConsoleApp> logger)
        {
            _redisClient = redisClient;
            _logger = logger;
        }

        public async Task Run()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Choose an option:");
                Console.WriteLine("1) Query by key ");
                Console.WriteLine("2) Add new key / valuee");
                Console.WriteLine("(Anything else) Exit");
                Console.Write("\r\nSelect an option >");

                switch (Console.ReadKey().KeyChar)
                {
                    case '1':
                        await GetAllKeysAndValues();
                        break;
                    case '2':
                        await AddNewItem();
                        break;
                    default:
                        Console.WriteLine("Nope");
                        return;
                }

                
            }
        }

        public async Task GetAllKeysAndValues()
        {
            Console.WriteLine();
            Console.Write("Enter key query >");

            var query = Console.ReadLine();

            var keys = await _redisClient.GetKeys(query);

            foreach (var key in keys)
            {
                var value = await _redisClient.GetAsync(key);
                Console.WriteLine($"'{key}': '{value}'");
            }

            Console.WriteLine("---Press any key---");
            Console.ReadKey();
        }

        public async Task AddNewItem()
        {
            Console.WriteLine();
            Console.Write("Key name >");

            var keyName = Console.ReadLine();

            Console.Write("Value >");

            var value = Console.ReadLine();

            await _redisClient.StoreAsync(keyName, value, TimeSpan.FromDays(1));
        }
    }
}