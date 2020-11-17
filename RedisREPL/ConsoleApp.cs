using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using PollyResilience.Service;
using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace RedisREPL
{
    public class ConsoleApp
    {
        protected readonly IRedisClient _redisClient;
        protected readonly IRedisClient _redisClientReplica;
        protected readonly ILogger<ConsoleApp> _logger;

        public ConsoleApp(IRedisClient redisClient,
            IConfigurationRoot configuration,
            ILogger<ConsoleApp> logger)
        {
            _redisClient = redisClient;

            var retryPolicy = Policy.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(
                    retryCount: 0,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(200),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Information, $"REPL error ({exception.GetType().ToString()}) on retry {retryCount} for {timeSpan.ToString()}", exception);
                    }
                );

            _redisClientReplica = new RedisClient(null, configuration, retryPolicy, "RedisConnectionStringReplica");
            _logger = logger;
        }

        public async Task Run()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Choose an option:");
                Console.WriteLine("1) Query by key");
                Console.WriteLine("2) Get all keys / values");
                Console.WriteLine("3) Add new key / value");
                Console.WriteLine("4) Test Replication");
                Console.WriteLine("(Anything else) Exit");
                Console.Write("\r\nSelect an option >");

                switch (Console.ReadKey().KeyChar)
                {
                    case '1':
                        await GetByKey();
                        break;
                    case '2':
                        await GetAllKeysAndValues();
                        break;
                    case '3':
                        await AddNewItem();
                        break;
                    case '4':
                        await TestReplication();
                        break;
                    default:
                        return;
                }


            }
        }

        public async Task GetByKey()
        {
            Console.WriteLine();
            Console.WriteLine("Choose an output option:");
            Console.WriteLine("1) Console out");
            Console.WriteLine("2) Dump to file");
            Console.WriteLine("3) Both");
            Console.Write("\r\nSelect an option >");

            var displayType = Console.ReadKey().KeyChar;
            string fileName = null;
            StreamWriter writer = null;

            if (displayType == '2' || displayType == '3')
            {
                Console.Write("\r\nFilename >");
                fileName = Console.ReadLine();

                fileName = $"{Environment.CurrentDirectory}\\{fileName}";
                writer = new StreamWriter(fileName);
            }

            Console.Write("\r\nEnter key >");
            var key = Console.ReadLine();

            var timer = new Stopwatch();
            timer.Start();
                
            var value = await _redisClient.GetAsync(key);

            timer.Stop();

            var outputLine = $">>>'{key}': '{value}' (in {timer.ElapsedMilliseconds}ms)";

            if (displayType == '2' || displayType == '3')
            {
                writer.WriteLine(outputLine);
            }

            if (displayType == '1')
            {
                Console.WriteLine(outputLine);
            }

            if (writer != null)
            {
                writer.Flush();
                writer.Dispose();
                writer.Close();
            }

            Console.WriteLine("---Press any key---");
            Console.ReadKey();
        }

        public async Task GetAllKeysAndValues()
        {
            Console.WriteLine();            
            Console.WriteLine("Choose an output option:");
            Console.WriteLine("1) Console out");
            Console.WriteLine("2) Dump to file");
            Console.WriteLine("3) Both");
            Console.Write("\r\nSelect an option >");

            var displayType = Console.ReadKey().KeyChar;
            string fileName = null;
            StreamWriter writer = null;

            if (displayType == '2' || displayType == '3')
            {
                Console.Write("\r\nFilename >");
                fileName = Console.ReadLine();

                fileName = $"{Environment.CurrentDirectory}\\{fileName}";
                writer = new StreamWriter(fileName);
            }

            Console.Write("\r\nEnter key query >");
            var query = Console.ReadLine();

            var timer = new Stopwatch();
            timer.Start();

            var keys = await _redisClient.GetKeys(query);

            timer.Stop();
            Console.WriteLine($"Query '{query}' took {timer.ElapsedMilliseconds}ms with {keys.Count} results");

            foreach (var key in keys)
            {
                timer.Reset();
                timer.Start();

                var value = await _redisClient.GetAsync(key);

                timer.Stop();

                var outputLine = $">>>'{key}': '{value}' (in {timer.ElapsedMilliseconds}ms)";

                if (displayType == '2' || displayType == '3')
                {
                    writer.WriteLine(outputLine);
                }

                if (displayType == '1')
                {
                    Console.WriteLine(outputLine);
                }
            }

            if (writer != null)
            {
                writer.Flush();
                writer.Dispose();
                writer.Close();
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

        public async Task TestReplication()
        {
            Console.WriteLine();

            Console.Write("Iterations >");

            var iterations = int.Parse(Console.ReadLine());

            Console.Write("Key name >");

            var keyName = Console.ReadLine();

            Console.Write("Value >");

            var value = Console.ReadLine();

            for (int i = 0; i < iterations; i++)
            {
                var iterationKey = $"{keyName}_{i}";

                await _redisClient.StoreAsync(iterationKey, value, TimeSpan.FromDays(1));

                var timer = new Stopwatch();
                timer.Start();

                while (true)
                {
                    var replicatedValue = await _redisClientReplica.GetAsync(iterationKey);

                    if (replicatedValue == value)
                    {
                        timer.Stop();

                        Console.WriteLine($"iteration {i}: propagated in {timer.ElapsedMilliseconds}ms");

                        break;
                    }
                    else
                    {
                        Console.WriteLine($"iteration {i}: not yet");
                    }
                }
            }

            Console.WriteLine("---Press any key---");
            Console.ReadKey();
        }
    }
}