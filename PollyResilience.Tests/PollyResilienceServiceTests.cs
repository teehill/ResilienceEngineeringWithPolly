using FakeItEasy;
using Xunit;
using PollyResilience.Service;

namespace PollyResilienceConsole.Tests
{
    public class PollyResilienceServiceTests
    {
        protected IPollyResilienceService _pollyResilienceService;

        public PollyResilienceServiceTests()
        {
            _pollyResilienceService = A.Fake<IPollyResilienceService>();
        }


        [Fact]
        public void Should_Run()
        {
            _pollyResilienceService.HelloWorld();

            A.CallTo(() => _pollyResilienceService.HelloWorld()).MustHaveHappened();
        }
    }
}
