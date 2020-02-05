using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Behavior;
using Polly.Wrap;
using StackExchange.Redis;
using PollyResilience.Service;

namespace RedisPublisher
{
    public class ConsoleApp
    {
        protected readonly IRedisClient _redisClient;
        protected readonly IPublisherConfiguration _configuration;
        protected readonly ILogger<ConsoleApp> _logger;

        public ConsoleApp(IRedisClient redisClient,
            IPublisherConfiguration configuration,
            ILogger<ConsoleApp> logger)
        {
            _redisClient = redisClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task Run()
        {
            var policy = SetupPolicy();

            while (true)
            {
                foreach (var message in messages)
                {
                    await policy.ExecuteAsync(async () =>
                    {
                        await _redisClient.PublishAsync("messages", message);
                        return string.Empty;
                    });

                    await Task.Delay(600);
                }
            }
        }

        private AsyncPolicyWrap<string> SetupPolicy()
        {
            var redisFallbackPolicyString = Policy<string>.Handle<RedisConnectionException>()
                .Or<SocketException>()
                .FallbackAsync(
                    fallbackValue: null,
                    onFallbackAsync: async b =>
                    {
                        await Task.CompletedTask;
                        _logger.Log(LogLevel.Error, $"Fallback caught a '{b.Exception.GetType().ToString()}': '{b.Exception.Message}'");
                        return;
                    });

            var chaosPolicy = MonkeyPolicy.InjectBehaviourAsync<string>(with =>
                with.Behaviour(async () =>
                {
                    await _redisClient.PublishAsync("messages", monkeyMessage);
                    await Task.Delay(_configuration.ResiliencyConfiguration.MonkeyDelay);
                    _logger.Log(LogLevel.Information, $"Injected monkey ook ook");
                    return;
                })
                    .InjectionRate(_configuration.ResiliencyConfiguration.MonkeyInjectionRate)
                    .Enabled()
            );

            return redisFallbackPolicyString.WrapAsync(chaosPolicy);
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