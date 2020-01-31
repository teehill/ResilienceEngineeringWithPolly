namespace GitHubClient
{
    public class GitHubConfiguration
    {
        public string BaseAPIPath { get; set; }
        public int RetryCount { get; set; }
        public int RetryDelayMilliseconds { get; set; }
        public int CircuitBreakerThreshold { get; set; }
        public int CircuitBreakerDurationMilliseconds { get; set; }
    }
}
