namespace RedisPublisher
{
    public class ResiliencyConfiguration
    {
        public int RetryCount { get; set; }
        public int RetryDelayMilliseconds { get; set; }
        public double FaultRate { get; set; }
        public int LatencyMilliseconds { get; set; }
        public double LatencyInjectionRate { get; set; }
        public double MonkeyInjectionRate { get; set; }
        public int MonkeyDelay { get; set; }
    }
}
