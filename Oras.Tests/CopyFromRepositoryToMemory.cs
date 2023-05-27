using Moq;
using Moq.Protected;
using Oras.Constants;
using Oras.Memory;
using Oras.Models;
using Oras.Remote;
using System.Net;
using Xunit;
using static Oras.Content.DigestUtility;

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

        /// <summary>
        /// This test tries copying artifacts from the remote target to the memory target
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_CopyFromRepositoryToMemory()
        {
            var exampleManifest = @"hello world"u8.ToArray();

            var exampleManifestDescriptor = new Descriptor
            {
                MediaType = OCIMediaTypes.Descriptor,
                Digest = CalculateSHA256DigestFromBytes(exampleManifest),
                Size = exampleManifest.Length
            };
            var exampleTag = "latest";
            var exampleUploadUUid = new Guid().ToString();
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                var p = req.RequestUri.AbsolutePath;
                var m = req.Method;
                if (p.Contains("/blobs/uploads/") && m == HttpMethod.Post)
                {
                    res.StatusCode = HttpStatusCode.Accepted;
                    res.Headers.Location = new Uri($"{p}/{exampleUploadUUid}");
                    res.Content.Headers.ContentType.MediaType = OCIMediaTypes.ImageManifest;
                    return res;
                }
                if (p.Contains("/blobs/uploads/" + exampleUploadUUid) && m == HttpMethod.Get)
                {
                    res.StatusCode = HttpStatusCode.Created;
                    return res;
                }

                if (p.Contains("/manifests/latest") && m == HttpMethod.Put)
                {
                    res.StatusCode = HttpStatusCode.Created;
                    return res;
                }
                if (p.Contains("/manifests/" + exampleManifestDescriptor.Digest) || p.Contains("/manifests/latest") && m == HttpMethod.Head)
                {
                    if (m == HttpMethod.Get)
                    {
                        res.Content = new ByteArrayContent(exampleManifest);
                        res.Content.Headers.Add("Content-Type", OCIMediaTypes.Descriptor);
                        res.Content.Headers.Add("Docker-Content-Digest", exampleManifestDescriptor.Digest);
                        res.Content.Headers.Add("Content-Length", exampleManifest.Length.ToString());
                        return res;
                    }
                    res.Content.Headers.Add("Content-Type", OCIMediaTypes.Descriptor);
                    res.Content.Headers.Add("Docker-Content-Digest", exampleManifestDescriptor.Digest);
                    res.Content.Headers.Add("Content-Length", exampleManifest.Length.ToString());
                    return res;
                }


                if (p.Contains("/blobs/") && (m == HttpMethod.Get || m == HttpMethod.Head))
                {
                    var arr = p.Split("/");
                    var digest = arr[arr.Length - 1];
                    Descriptor desc = null;
                    byte[] content = null;

                    if (digest == exampleManifestDescriptor.Digest)
                    {
                        desc = exampleManifestDescriptor;
                        content = exampleManifest;
                    }

                    res.Content = new ByteArrayContent(content);
                    res.Content.Headers.Add("Content-Type", desc.MediaType);
                    res.Content.Headers.Add("Docker-Content-Digest", digest);
                    res.Content.Headers.Add("Content-Length", content.Length.ToString());
                    return res;
                }

                if (p.Contains("/manifests/") && m == HttpMethod.Put)
                {
                    res.StatusCode = HttpStatusCode.Created;
                    return res;
                }

                return res;
            };

            var reg = new Registry("localhost:5000");

            var src = await reg.Repository("source", CustomClient(func), CancellationToken.None);
            var dst = new MemoryTarget();
            var tagName = "latest";
            var desc = await Copy.CopyAsync(src, tagName, dst, tagName, CancellationToken.None);
            Console.WriteLine(desc.Digest);
        }
    }
}
