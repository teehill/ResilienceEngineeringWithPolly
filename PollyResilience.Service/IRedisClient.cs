using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace PollyResilience.Service
{
    public interface IRedisClient
    {
        string GetServerName();

        Task<List<string>> GetKeys(string query);

        Task<bool> StoreAsync(string key, string value, TimeSpan expiresAt);

        Task<bool> StoreAsync(string key, object value, TimeSpan expiresAt, bool binary = true);

        Task<string> GetAsync(string key);

        Task<T> GetAsync<T>(string key, bool binary = true);

        Task<bool> RemoveAsync(string key);

        Task SubscribeAsync(string channel, Action<RedisChannel, RedisValue> handler);

        Task PublishAsync(string channel, string message);

        Task<TimeSpan> Ping(RedisServerType serverType = RedisServerType.Read);

        IEnumerable<EndPoint> GetEndpoints();

        Task<RedisResult> IssueCommand(EndPoint serverEndpoint, string command);

        Task<ClientInfo[]> GetClientList(EndPoint serverEndpoint);
    }
}