using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace PollyResilience.Service
{
    public interface IRedisClient
    {
        Task<bool> StoreAsync(string key, string value, TimeSpan expiresAt);

        Task<string> GetAsync(string key);

        Task<bool> RemoveAsync(string key);

        Task SubscribeAsync(string channel, Action<RedisChannel, RedisValue> handler);

        Task PublishAsync(string channel, string message);
    }
}