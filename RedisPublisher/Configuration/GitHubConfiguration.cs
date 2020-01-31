namespace RedisPublisher
{
    public class GitHubConfiguration
    {
        public string BaseAPIPath { get; set; }
        public int RetryCount { get; set; }
        public int RetryDelayMilliseconds { get; set; }
    }
}
