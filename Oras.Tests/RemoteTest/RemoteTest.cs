using Moq;
using Moq.Protected;
using Oras.Constants;
using Oras.Models;
using Oras.Remote;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;
using static Oras.Content.Content;

namespace Oras.Tests.RemoteTest
{
    public class RemoteTest
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
        /// Repository_FetchAsync tests the FetchAsync method of the Repository.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Repository_FetchAsync()
        {
            var blob = Encoding.UTF8.GetBytes("hello world");
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint)blob.Length
            };
            var index = """{"manifests":[]}"""u8.ToArray();
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
                if (path == "/v2/test/blobs/" + blobDesc.Digest)
                {
                    resp.Content = new ByteArrayContent(blob);
                    resp.Content.Headers.Add("Content-Type", "application/octet-stream");
                    resp.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    return resp;
                }
                if (path == "/v2/test/manifests/" + indexDesc.Digest)
                {
                    if (!req.Headers.Accept.Contains(new MediaTypeWithQualityHeaderValue(OCIMediaTypes.ImageIndex)))
                    {
                        resp.StatusCode = HttpStatusCode.BadRequest;
                        Debug.WriteLine("manifest not convertable: " + req.Headers.Accept);
                        return resp;
                    }
                    resp.Content = new ByteArrayContent(index);
                    resp.Content.Headers.Add("Content-Type", indexDesc.MediaType);
                    resp.Content.Headers.Add("Docker-Content-Digest", indexDesc.Digest);
                    return resp;

                }
                resp.StatusCode = HttpStatusCode.NotFound;
                return resp;
            };
            var repo = new Repository("localhost:5000/test");
            repo.Client = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var stream = await repo.FetchAsync(blobDesc, cancellationToken);
            var buf = new byte[stream.Length];
            await stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(blob, buf);
            stream = await repo.FetchAsync(indexDesc, cancellationToken);
            buf = new byte[stream.Length];
            await stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(index, buf);

        }

        /// <summary>
        /// Repository_PushAsync tests the PushAsync method of the Repository
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Repository_PushAsync()
        {
            var blob = @"""hello world"""u8.ToArray();
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint)blob.Length
            };
            var index = @"""{""manifests"":[]}"""u8.ToArray();
            var indexDesc = new Descriptor()
            {
                Digest = CalculateDigest(index),
                MediaType = OCIMediaTypes.ImageIndex,
                Size = index.Length
            };
            var uuid = Guid.NewGuid().ToString();
            var gotBlob = new byte[blobDesc.Size];
            var gotIndex = new byte[indexDesc.Size];
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var resp = new HttpResponseMessage();
                resp.RequestMessage = req;
                if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == "/v2/test/blobs/uploads/")
                {
                    resp.Headers.Location = new Uri("http://localhost:5000/v2/test/blobs/uploads/" + uuid);
                    resp.StatusCode = HttpStatusCode.Accepted;
                    return resp;
                }

                if (req.Method == HttpMethod.Put &&
                    req.RequestUri!.AbsolutePath == "/v2/test/blobs/uploads/" + uuid)
                {
                    if (req.Headers.TryGetValues("Content-Type", out var values) && !values.Contains("application/octet-stream"))
                    {
                        resp.StatusCode = HttpStatusCode.BadRequest;
                        return resp;

                    }
                    if (!req.RequestUri.Query.Contains("digest=" + blobDesc.Digest))
                    {
                        resp.StatusCode = HttpStatusCode.BadRequest;
                        return resp;
                    }

                    var stream = req.Content!.ReadAsStream(cancellationToken);
                    stream.Read(gotBlob);
                    resp.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    resp.StatusCode = HttpStatusCode.Created;
                    return resp;

                }
                if (req.Method == HttpMethod.Put &&
                    req.RequestUri!.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)
                {
                    if (req.Headers.TryGetValues("Content-Type", out var values) &&
                        !values.Contains(OCIMediaTypes.ImageIndex))
                    {
                        resp.StatusCode = HttpStatusCode.BadRequest;
                        return resp;
                    }
                    var stream = req.Content!.ReadAsStream(cancellationToken);
                    stream.Read(gotIndex);
                    resp.Content.Headers.Add("Docker-Content-Digest", indexDesc.Digest);
                    resp.StatusCode = HttpStatusCode.Created;
                    return resp;
                }
                resp.StatusCode = HttpStatusCode.Forbidden;
                return resp;

            };

            var repo = new Repository("localhost:5000/test");
            repo.Client = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            await repo.PushAsync(blobDesc, new MemoryStream(blob), cancellationToken);
            Assert.Equal(blob, gotBlob);
            await repo.PushAsync(indexDesc, new MemoryStream(index), cancellationToken);
            Assert.Equal(index, gotIndex);
        }

        public async Task Repository_ExistsAsync()
        {
            var blob = @"""hello world"""u8.ToArray();
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint)blob.Length
            };
            var index = @"""{""manifests"":[]}"""u8.ToArray();
            var indexDesc = new Descriptor()
            {
                Digest = CalculateDigest(index),
                MediaType = OCIMediaTypes.ImageIndex,
                Size = index.Length
            };

        }
    }
}

