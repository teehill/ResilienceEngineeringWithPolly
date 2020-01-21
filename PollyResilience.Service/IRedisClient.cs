using System;

namespace PollyResilience.Service
{
    public interface IRedisClient
    {
        bool Store(string key, string value, TimeSpan expiresAt);

        string Get(string key);

        bool Remove(string key);

        void Subscribe(string channel);
        
        void Publish(string channel, string message);
    }
}