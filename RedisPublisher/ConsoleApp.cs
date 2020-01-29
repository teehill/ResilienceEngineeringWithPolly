using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Wrap;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Behavior;
using StackExchange.Redis;
using PollyResilience.Service;

namespace RedisPublisher
{
    public class ConsoleApp
    {
        protected readonly ILogger<ConsoleApp> _logger;
        protected readonly IPublisherConfiguration _config;
        protected readonly IRedisClient _redisClient;

        public ConsoleApp(IPublisherConfiguration configuration,
            ILogger<ConsoleApp> logger,
            IRedisClient redisClient)
        {
            _logger = logger;
            _config = configuration;
            _redisClient = redisClient;
        }

        public async Task Run()
        {
            var redisFallbackPolicyString = Policy<string>.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .FallbackAsync(
                    fallbackValue: null,
                    onFallbackAsync: async b => {
                        await Task.FromResult(true);
                        _logger.Log(LogLevel.Error, $"Fallback caught a '{b.Exception.GetType().ToString()}': '{b.Exception.Message}'");
                        return; 
                    });

            var chaosPolicy = MonkeyPolicy.InjectBehaviourAsync<string>(with =>
                with.Behaviour(InjectMonkey)
                    .InjectionRate(0.1)
                    .Enabled()
            );

            var policy = redisFallbackPolicyString.WrapAsync(chaosPolicy);

            var i = 0;

            while (true)
            {
                await policy.ExecuteAsync(async () => {
                    await _redisClient.PublishAsync("messages", messages[i]);
                    return string.Empty;
                });

                await Task.Delay(600);

                if (i == messages.Length - 1)
                    i = 0;
                else
                    i++;
            }
        }

        public async Task InjectMonkey()
        {
            await _redisClient.PublishAsync("messages", monkeyMessage);
            await Task.Delay(2000);
            _logger.Log(LogLevel.Information, $"Injected monkey ook ook");
            return;
        }

        protected string[] messages => new string[] {
@"
▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒
▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒
▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒
▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒
▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒
▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒
▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒
▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒
▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒",
@"
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
@"
▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
@"
████████████████████████████████████████████████
████████████████████████████████████████████████
████████████████████████████████████████████████
████████████████████████████████████████████████
████████████████████████████████████████████████
████████████████████████████████████████████████
████████████████████████████████████████████████
████████████████████████████████████████████████
████████████████████████████████████████████████",
@"                                               
 /$$$$$$$   /$$$$$$  /$$       /$$   /$$     /$$
| $$__  $$ /$$__  $$| $$      | $$  |  $$   /$$/
| $$  \ $$| $$  \ $$| $$      | $$   \  $$ /$$/ 
| $$$$$$$/| $$  | $$| $$      | $$    \  $$$$/  
| $$____/ | $$  | $$| $$      | $$     \  $$/   
| $$      | $$  | $$| $$      | $$      | $$    
| $$      |  $$$$$$/| $$$$$$$$| $$$$$$$$| $$    
|__/       \______/ |________/|________/|__/    ",
@"                                               
$$$$$$$\   $$$$$$\  $$\       $$\   $$\     $$\ 
$$  __$$\ $$  __$$\ $$ |      $$ |  \$$\   $$  |
$$ |  $$ |$$ /  $$ |$$ |      $$ |   \$$\ $$  / 
$$$$$$$  |$$ |  $$ |$$ |      $$ |    \$$$$  /  
$$  ____/ $$ |  $$ |$$ |      $$ |     \$$  /   
$$ |      $$ |  $$ |$$ |      $$ |      $$ |    
$$ |       $$$$$$  |$$$$$$$$\ $$$$$$$$\ $$ |    
\__|       \______/ \________|\________|\__|    ",
@"
 _______    ______   __        __    __      __ 
/       \  /      \ /  |      /  |  /  \    /  |
$$$$$$$  |/$$$$$$  |$$ |      $$ |  $$  \  /$$/ 
$$ |__$$ |$$ |  $$ |$$ |      $$ |   $$  \/$$/  
$$    $$/ $$ |  $$ |$$ |      $$ |    $$  $$/   
$$$$$$$/  $$ |  $$ |$$ |      $$ |     $$$$/    
$$ |      $$ \__$$ |$$ |_____ $$ |_____ $$ |    
$$ |      $$    $$/ $$       |$$       |$$ |    
$$/        $$$$$$/  $$$$$$$$/ $$$$$$$$/ $$/     ",
@"
 _______    ______   __        __    __      __ 
|       \  /      \ |  \      |  \  |  \    /  \
| $$$$$$$\|  $$$$$$\| $$      | $$   \$$\  /  $$
| $$__/ $$| $$  | $$| $$      | $$    \$$\/  $$ 
| $$    $$| $$  | $$| $$      | $$     \$$  $$  
| $$$$$$$ | $$  | $$| $$      | $$      \$$$$   
| $$      | $$__/ $$| $$_____ | $$_____ | $$    
| $$       \$$    $$| $$     \| $$     \| $$    
 \$$        \$$$$$$  \$$$$$$$$ \$$$$$$$$ \$$    "
};

        protected readonly string monkeyMessage = @"
            .-`-.            .-`-.             
          _/_-.-_\_        _/.-.-.\_           
         / __} {__ \      /|( o o )|\          
        / //  *  \\ \    | //  *  \\ |         
       / / \'---'/ \ \  / / \'---'/ \ \        
       \ \_/`'''`\_/ /  \ \_/`'''`\_/ /        
        \           /    \           /         
                                               ";
    }
}