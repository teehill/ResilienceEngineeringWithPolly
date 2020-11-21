using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace PollyResilience.Service
{
    public interface IRedisClient
    {
        Task<List<string>> GetKeys(string query);

        Task<bool> StoreAsync(string key, string value, TimeSpan expiresAt);

        Task<string> GetAsync(string key);

        Task<bool> RemoveAsync(string key);

        Task SubscribeAsync(string channel, Action<RedisChannel, RedisValue> handler);

        Task PublishAsync(string channel, string message);

        void ForceReconnect();
    }
}