using Xunit;

namespace PollyResilienceConsole.Tests
{
    public class ProgramTests
    {
        [Theory]
        [InlineData(2,4,6)]
        [InlineData(1,2,3)]
        public void Should_Return_Provided_Int(int x, int y, int z)
        {
            var result = RedisSubscriber.ConsoleApp.Add(x, y);

            Assert.Equal(z, result);
        }
    }
}
