﻿using Microsoft.Extensions.Configuration;
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
            IServiceProvider serviceProvider,
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

            var redisLogger = (ILogger<RedisClient>)serviceProvider.GetService(typeof(ILogger<RedisClient>));

            _redisClientReplica = new RedisClient(redisLogger, configuration, retryPolicy, "RedisReadConnectionString");
            _logger = logger;

            //initial connection viewing
            Console.WriteLine("---Press any key---");
            Console.ReadKey();
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
                Console.WriteLine("5) Get Extended Tests");
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
                    case '5':
                        await GetExtendedTest();
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

            Console.WriteLine("---Press any key---");
            Console.ReadKey();
        }

        public async Task TestReplication()
        {
            Console.WriteLine();

            Console.Write("Iterations (0 forever) >");

            var iterations = int.Parse(Console.ReadLine());

            Console.Write("Delay between tests (ms) >");

            int delay = int.Parse(Console.ReadLine());

            Console.Write("Key name >");

            var keyName = Console.ReadLine();

            Console.Write("Store Value >");

            var value = Console.ReadLine();

            var count = 0;

            while (true)
            {
                var iterationKey = $"{keyName}_{count}";

                await _redisClient.StoreAsync(iterationKey, value, TimeSpan.FromDays(1));

                var timer = new Stopwatch();
                timer.Start();

                while (true)
                {
                    var replicatedValue = await _redisClientReplica.GetAsync(iterationKey);

                    if (replicatedValue == value)
                    {
                        timer.Stop();

                        var logMessage = $"iteration {count}: propagation in {decimal.Divide(timer.ElapsedTicks, TimeSpan.TicksPerMillisecond)} ms {timer.ElapsedTicks} ticks";
                        Console.WriteLine(logMessage);
                        _logger.LogInformation(logMessage);
                        break;
                    }
                }

                if (iterations > 0 && count >= iterations - 1)
                {
                    Console.WriteLine("All done");
                    break;
                }

                System.Threading.Thread.Sleep(delay);

                count++;
            }

            Console.WriteLine("---Press any key---");
            Console.ReadKey();
        }

        public async Task GetExtendedTest()
        {
            Console.WriteLine();

            Console.Write("Iterations (0 forever) >");

            var iterations = int.Parse(Console.ReadLine());

            Console.Write("Delay between tests (ms) >");

            int delay = int.Parse(Console.ReadLine());

            Console.Write("Key to query >");

            var key = Console.ReadLine();

            var count = 0;

            while (true)
            {
                var timer = new Stopwatch();
                timer.Start();

                var result = await _redisClient.GetAsync(key);

                timer.Stop();

                string logMessage = $"{count}|{key}:{result}: get completed in {decimal.Divide(timer.ElapsedTicks, TimeSpan.TicksPerMillisecond)} ms {timer.ElapsedTicks} ticks";
                Console.WriteLine(logMessage);
                _logger.LogInformation(logMessage);

                if (iterations > 0 && count >= iterations - 1)
                {
                    Console.WriteLine("All done");
                    break;
                }

                System.Threading.Thread.Sleep(delay);

                count++;
            }

            Console.WriteLine("---Press any key---");
            Console.ReadKey();
        }
    }
}