using Moq;
using Moq.Protected;
using Xunit;

namespace Oras.Tests
{
    public class CopyFromRepositoryToMemory
    {
        public static HttpClient CustomClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> func)
        {
            var moqHandler = new Mock<DelegatingHandler>();
            moqHandler.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            ).ReturnsAsync(func);
            return new HttpClient(moqHandler.Object);
        }

        [Fact]
        public async Task Test_CopyFromRepositoryToMemory()
        {

        }
    }
}
