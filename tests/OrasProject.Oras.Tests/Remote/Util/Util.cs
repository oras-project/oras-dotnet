using Moq;
using Moq.Protected;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Tests.Remote.Util;

public class Util
{
    /// <summary>
    /// AreDescriptorsEqual compares two descriptors and returns true if they are equal.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool AreDescriptorsEqual(Descriptor a, Descriptor b)
    {
        return a.MediaType == b.MediaType && a.Digest == b.Digest && a.Size == b.Size;
    }
    
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

    public static HttpClient CustomClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
    {
        var moqHandler = new Mock<DelegatingHandler>();
        moqHandler.Protected().Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        ).Returns(func);
        return new HttpClient(moqHandler.Object);
    }
}
