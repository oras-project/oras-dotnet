using Moq;
using Moq.Protected;
using Oras.Constants;
using Oras.Exceptions;
using Oras.Models;
using Oras.Remote;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Oras.Remote;
using System.Text.RegularExpressions;
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
                resp.RequestMessage = req;
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

        /// <summary>
        /// Repository_ExistsAsync tests the ExistsAsync method of the Repository
        /// </summary>
        /// <returns></returns>
        [Fact]
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
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Head)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }
                if (req.RequestUri!.AbsolutePath == "/v2/test/blobs/" + blobDesc.Digest)
                {
                    res.Content.Headers.Add("Content-Type", "application/octet-stream");
                    res.Content.Headers.Add("Content-Length", blobDesc.Size.ToString());
                    res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    return res;
                }
                if (req.RequestUri!.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)
                {
                    if (req.Headers.TryGetValues("Accept", out var values) && !values.Contains(OCIMediaTypes.ImageIndex))
                    {
                        return new HttpResponseMessage(HttpStatusCode.NotAcceptable);
                    }
                    res.Content.Headers.Add("Content-Type", indexDesc.MediaType);
                    res.Content.Headers.Add("Content-Length", indexDesc.Size.ToString());
                    res.Content.Headers.Add("Docker-Content-Digest", indexDesc.Digest);
                    return res;
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.Client = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var exists = await repo.ExistsAsync(blobDesc, cancellationToken);
            Assert.True(exists);
            exists = await repo.ExistsAsync(indexDesc, cancellationToken);
            Assert.True(exists);
        }

        /// <summary>
        /// Repository_DeleteAsync tests the DeleteAsync method of the Repository
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Repository_DeleteAsync()
        {
            var blob = @"""hello world"""u8.ToArray();
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint)blob.Length
            };
            var blobDeleted = false;
            var index = @"""{""manifests"":[]}"""u8.ToArray();
            var indexDesc = new Descriptor()
            {
                Digest = CalculateDigest(index),
                MediaType = OCIMediaTypes.ImageIndex,
                Size = index.Length
            };
            var indexDeleted = false;
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Delete)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }
                if (req.RequestUri!.AbsolutePath == "/v2/test/blobs/" + blobDesc.Digest)
                {
                    blobDeleted = true;
                    res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    res.StatusCode = HttpStatusCode.Accepted;
                    return res;
                }
                if (req.RequestUri!.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)
                {
                    indexDeleted = true;
                    // no "Docker-Content-Digest" header for manifest deletion
                    res.StatusCode = HttpStatusCode.Accepted;
                    return res;
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.Client = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            await repo.DeleteAsync(blobDesc, cancellationToken);
            Assert.True(blobDeleted);
            await repo.DeleteAsync(indexDesc, cancellationToken);
            Assert.True(indexDeleted);
        }

        /// <summary>
        /// Repository_ResolveAsync tests the ResolveAsync method of the Repository
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Repository_ResolveAsync()
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
            var reference = "foobar";

            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Head)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }
                if (req.RequestUri!.AbsolutePath == "/v2/test/manifests/" + blobDesc.Digest)
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
                if (req.RequestUri!.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest
                    || req.RequestUri!.AbsolutePath == "/v2/test/manifests/" + reference)

                {
                    if (req.Headers.TryGetValues("Accept", out var values) && !values.Contains(OCIMediaTypes.ImageIndex))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                    res.Content.Headers.Add("Content-Type", indexDesc.MediaType);
                    res.Content.Headers.Add("Content-Length", indexDesc.Size.ToString());
                    res.Content.Headers.Add("Docker-Content-Digest", indexDesc.Digest);
                    return res;
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.Client = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            await Assert.ThrowsAsync<NotFoundException>(async () => await repo.ResolveAsync(blobDesc.Digest, cancellationToken));
            // await repo.ResolveAsync(blobDesc.Digest, cancellationToken);
            var got = await repo.ResolveAsync(indexDesc.Digest, cancellationToken);
            Assert.Equal(indexDesc, got);
            got = await repo.ResolveAsync(reference, cancellationToken);
            Assert.Equal(indexDesc, got);
            var tagDigestRef = "whatever" + "@" + indexDesc.Digest;
            got = await repo.ResolveAsync(tagDigestRef, cancellationToken);
            Assert.Equal(indexDesc, got);
            var fqdnRef = "localhost:5000/test" + ":" + tagDigestRef;
            got = await repo.ResolveAsync(fqdnRef, cancellationToken);
            Assert.Equal(indexDesc, got);
        }

        /// <summary>
        /// Repository_ResolveAsync tests the ResolveAsync method of the Repository
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Repository_TagAsync()
        {
            var blob = """hello"""u8.ToArray();
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
            var gotIndex = new byte[indexDesc.Size];
            var reference = "foobar";

            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method == HttpMethod.Get &&
                    req.RequestUri.AbsolutePath == "/v2/test/manifests/" + blobDesc.Digest)
                {
                    return new HttpResponseMessage(HttpStatusCode.Found);
                }
                if (req.Method == HttpMethod.Get && req.RequestUri.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)
                {
                    if (req.Headers.TryGetValues("Accept", out var values) && !values.Contains(OCIMediaTypes.ImageIndex))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }

                    res.Content = new ByteArrayContent(index);
                    res.Content.Headers.Add("Content-Type", indexDesc.MediaType);
                    res.Content.Headers.Add("Docker-Content-Digest", indexDesc.Digest);
                    return res;
                }
                if (req.Method == HttpMethod.Put && req.RequestUri.AbsolutePath == "/v2/test/manifests/" + reference
                    || req.RequestUri.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)

                {
                    if (req.Headers.TryGetValues("Content-Type", out var values) && !values.Contains(OCIMediaTypes.ImageIndex))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                    gotIndex = req.Content.ReadAsByteArrayAsync().Result;
                    res.Content.Headers.Add("Docker-Content-Digest", indexDesc.Digest);
                    res.StatusCode = HttpStatusCode.Created;
                    return res;
                }
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            };
            var repo = new Repository("localhost:5000/test");
            repo.Client = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
           await Assert.ThrowsAnyAsync<Exception>(
async () => await repo.TagAsync(blobDesc, reference, cancellationToken));
            await repo.TagAsync(indexDesc, reference, cancellationToken);
            Assert.Equal(index, gotIndex);
            await repo.TagAsync(indexDesc, indexDesc.Digest, cancellationToken);
            Assert.Equal(index, gotIndex);
        }

        /// <summary>
        /// Repository_PushReferenceAsync tests the PushReferenceAsync method of the Repository
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Repository_PushReferenceAsync()
        {
            var index = @"""{""manifests"":[]}"""u8.ToArray();
            var indexDesc = new Descriptor()
            {
                Digest = CalculateDigest(index),
                MediaType = OCIMediaTypes.ImageIndex,
                Size = index.Length
            };
            var gotIndex = new byte[indexDesc.Size];
            var reference = "foobar";

            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method == HttpMethod.Put && req.RequestUri.AbsolutePath == "/v2/test/manifests/" + reference)
                {
                    if (req.Headers.TryGetValues("Content-Type", out var values) && !values.Contains(OCIMediaTypes.ImageIndex))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                    gotIndex = req.Content.ReadAsByteArrayAsync().Result;
                    res.Content.Headers.Add("Docker-Content-Digest", indexDesc.Digest);
                    res.StatusCode = HttpStatusCode.Created;
                    return res;
                }
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            };
            var repo = new Repository("localhost:5000/test");
            repo.Client = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var streamContent = new MemoryStream(index);
            await repo.PushReferenceAsync(indexDesc, streamContent, reference, cancellationToken);
            Assert.Equal(index, gotIndex);
        }

        /// <summary>
        /// Repository_FetchReferenceAsync tests the FetchReferenceAsync method of the Repository
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Repository_FetchReferenceAsyc()
        {
            var blob = """hello"""u8.ToArray();
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
            var reference = "foobar";

            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Get)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }

                if (req.RequestUri.AbsolutePath == "/v2/test/manifests/" + blobDesc.Digest)
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (req.RequestUri.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest
                    || req.RequestUri.AbsolutePath == "/v2/test/manifests/" + reference)
                {
                    if (req.Headers.TryGetValues("Accept", out var values) &&
                        !values.Contains(OCIMediaTypes.ImageIndex))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }

                    res.Content = new ByteArrayContent(index);
                    res.Content.Headers.Add("Content-Type", indexDesc.MediaType);
                    res.Content.Headers.Add("Docker-Content-Digest", indexDesc.Digest);
                    return res;
                }

                return new HttpResponseMessage(HttpStatusCode.Found);
            };
            var repo = new Repository("localhost:5000/test");
            repo.Client = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();

            // test with blob digest
           await Assert.ThrowsAsync<NotFoundException>(
                async () => await repo.FetchReferenceAsync(blobDesc.Digest, cancellationToken));

            // test with manifest digest
            var data = await repo.FetchReferenceAsync(indexDesc.Digest, cancellationToken);
            Assert.Equal(indexDesc, data.Descriptor);

            var buf = new byte[data.Stream.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(index, buf);

            // test with manifest tag
            data = await repo.FetchReferenceAsync(reference, cancellationToken);
            Assert.Equal(indexDesc, data.Descriptor);

            buf = new byte[data.Stream.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(index, buf);

            // test with manifest tag@digest
            var tagDigestRef = "whatever" + "@" + indexDesc.Digest;
            data = await repo.FetchReferenceAsync(tagDigestRef, cancellationToken);
            Assert.Equal(indexDesc, data.Descriptor);

            buf = new byte[data.Stream.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(index, buf);

            // test with manifest FQDN
            var fqdnRef = "localhost:5000/test" + ":" + tagDigestRef;
            data = await repo.FetchReferenceAsync(fqdnRef, cancellationToken);
            Assert.Equal(indexDesc, data.Descriptor);

            buf = new byte[data.Stream.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(index, buf);
        }

        /// <summary>
        /// Repository_TagsAsync tests the TagsAsync method of the Repository
        /// to check if the tags are returned correctly
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Fact]
        public async Task Repository_TagsAsync()
        {
            var tagSet = new List<List<string>>()
            {
                new(){"the", "quick", "brown", "fox"},
                new(){"jumps", "over", "the", "lazy"},
                new(){"dog"}
            };
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Get ||
                    req.RequestUri.AbsolutePath != "/v2/test/tags/list"
                    )
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                var q = req.RequestUri.Query;
                try
                {
                    var n = int.Parse(Regex.Match(q, @"(?<=n=)\d+").Value);
                    if (n != 4) throw new Exception();
                }
                catch (Exception e)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                var tags = new List<string>();
                var serverUrl = "http://localhost:5000";
                var matched = Regex.Match(q, @"(?<=test=)\w+").Value;
                switch (matched)
                {
                    case "foo":
                        tags = tagSet[1];
                        res.Headers.Add("Link", $"<{serverUrl}/v2/test/tags/list?n=4&test=bar>; rel=\"next\"");
                        break;
                    case "bar":
                        tags = tagSet[2];
                        break;
                    default:
                        tags = tagSet[0];
                        res.Headers.Add("Link", $"</v2/test/tags/list?n=4&test=foo>; rel=\"next\"");
                        break;
                }
                var tagObj = new ResponseTypes.Tags()
                {
                    tags = tags.ToArray()
                };
                res.Content = new StringContent(JsonSerializer.Serialize(tagObj));
                return res;

            };

            var repo = new Repository("localhost:5000/test");
            repo.Client = CustomClient(func);
            repo.PlainHTTP = true;
            repo.TagListPageSize = 4;

            var cancellationToken = new CancellationToken();

            var index = 0;
            await repo.TagsAsync("", async (string[] got) =>
            {
                if (index > 2)
                {
                    throw new Exception($"Error out of range: {index}");
                }

                var tags = tagSet[index];
                index++;
                Assert.Equal(got, tags);
            }, cancellationToken);
        }
    }
}




