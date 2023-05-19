using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using static Oras.Content.Content;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Oras.Constants;
using Oras.Models;
using Oras.Remote;
using Xunit;

namespace Oras.Tests.RemoteTest
{
    public class RemoteTest
    {
        public HttpClient CustomClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> func)
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
        public async Task Repository_FetchAsync()
        {
            var blob = Encoding.UTF8.GetBytes("hello world");
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint) blob.Length
            };
            var index = Encoding.UTF8.GetBytes("""{"manifests":[]}""");
            var indexDesc = new Descriptor()
            {
                Digest = CalculateDigest(index),
                MediaType = OCIMediaTypes.ImageIndex,
                Size = index.Length
            };
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var resp = new HttpResponseMessage();
                if (req.Method != HttpMethod.Get)
                {
                    Debug.WriteLine("Expected GET request");
                    resp.StatusCode = HttpStatusCode.BadRequest;
                    return resp;
                }

                var path = req.RequestUri!.AbsolutePath;
                if (path == "v2/test/blobs/" + blobDesc.Digest)
                {
                    resp.Headers.Add("Content-Type", "application/octet-stream");
                    resp.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    resp.Content = new ByteArrayContent(blob);
                    return resp;
                }
                else if (path == "v2/test/manifests/" + indexDesc.Digest)
                {
                    if (!req.Headers.Accept.Contains(new MediaTypeWithQualityHeaderValue(OCIMediaTypes.ImageIndex)))
                    {
                        resp.StatusCode = HttpStatusCode.BadRequest;
                        Debug.WriteLine("manifest not convertable: " + req.Headers.Accept);
                        return resp;
                    }
                   
                    resp.Headers.Add("Content-Type", indexDesc.MediaType);
                    resp.Headers.Add("Docker-Content-Digest", indexDesc.Digest);
                    resp.Content = new ByteArrayContent(index);
                    return resp;
                   
                }
                else
                {
                    Debug.WriteLine("Unexpected path: " + path);
                    resp.StatusCode = HttpStatusCode.NotFound;
                    return resp;
                }

            };
            var repo = new Repository("http://localhost:5000/test");
            repo.Client = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var stream = await repo.FetchAsync(blobDesc,cancellationToken);
            var buf = new byte[stream.Length];
            await stream.ReadAsync(buf, 0, (int)stream.Length, cancellationToken);
            Assert.Equal(blob, buf);


        }
    }
}
