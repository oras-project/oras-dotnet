using Moq;
using Moq.Protected;
using Oras.Constants;
using Oras.Exceptions;
using Oras.Models;
using Oras.Remote;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Xunit;
using static Oras.Content.Content;

namespace Oras.Tests.RemoteTest
{
    public class RepositoryTest
    {
        public struct TestIOStruct
        {
            public bool isTag;
            public bool errExpectedOnHEAD;
            public string serverCalculatedDigest;
            public string clientSuppliedReference;
            public bool errExpectedOnGET;
        }

        private byte[] theAmazingBanClan = "Ban Gu, Ban Chao, Ban Zhao"u8.ToArray();
        private const string theAmazingBanDigest = "b526a4f2be963a2f9b0990c001255669eab8a254ab1a6e3f84f1820212ac7078";

        private const string dockerContentDigestHeader = "Docker-Content-Digest";
        // The following truth table aims to cover the expected GET/HEAD request outcome
        // for all possible permutations of the client/server "containing a digest", for
        // both Manifests and Blobs.  Where the results between the two differ, the index
        // of the first column has an exclamation mark.
        //
        // The client is said to "contain a digest" if the user-supplied reference string
        // is of the form that contains a digest rather than a tag.  The server, on the
        // other hand, is said to "contain a digest" if the server responded with the
        // special header `Docker-Content-Digest`.
        //
        // In this table, anything denoted with an asterisk indicates that the true
        // response should actually be the opposite of what's expected; for example,
        // `*PASS` means we will get a `PASS`, even though the true answer would be its
        // diametric opposite--a `FAIL`. This may seem odd, and deserves an explanation.
        // This function has blind-spots, and while it can expend power to gain sight,
        // i.e., perform the expensive validation, we chose not to.  The reason is two-
        // fold: a) we "know" that even if we say "!PASS", it will eventually fail later
        // when checks are performed, and with that assumption, we have the luxury for
        // the second point, which is b) performance.
        //
        //	 _______________________________________________________________________________________________________________
        //	| ID | CLIENT          | SERVER           | Manifest.GET          | Blob.GET  | Manifest.HEAD       | Blob.HEAD |
        //	|----+-----------------+------------------+-----------------------+-----------+---------------------+-----------+
        //	| 1  | tag             | missing          | CALCULATE,PASS        | n/a       | FAIL                | n/a       |
        //	| 2  | tag             | presentCorrect   | TRUST,PASS            | n/a       | TRUST,PASS          | n/a       |
        //	| 3  | tag             | presentIncorrect | TRUST,*PASS           | n/a       | TRUST,*PASS         | n/a       |
        //	| 4  | correctDigest   | missing          | TRUST,PASS            | PASS      | TRUST,PASS          | PASS      |
        //	| 5  | correctDigest   | presentCorrect   | TRUST,COMPARE,PASS    | PASS      | TRUST,COMPARE,PASS  | PASS      |
        //	| 6  | correctDigest   | presentIncorrect | TRUST,COMPARE,FAIL    | FAIL      | TRUST,COMPARE,FAIL  | FAIL      |
        //	 ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// GetTestIOStructMapForGetDescriptorClass returns a map of test cases for different
        /// GET/HEAD request outcome for all possible permutations of the client/server "containing a digest", for
        /// both Manifests and Blobs. 
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, TestIOStruct> GetTestIOStructMapForGetDescriptorClass()
        {
            string correctDigest = $"sha256:{theAmazingBanDigest}";
            string incorrectDigest = $"sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

            return new Dictionary<string, TestIOStruct>
            {
                ["1. Client:Tag & Server:DigestMissing"] = new TestIOStruct
                {
                    isTag = true,
                    errExpectedOnHEAD = true
                },
                ["2. Client:Tag & Server:DigestValid"] = new TestIOStruct
                {
                    isTag = true,
                    serverCalculatedDigest = correctDigest
                },
                ["3. Client:Tag & Server:DigestWrongButSyntacticallyValid"] = new TestIOStruct
                {
                    isTag = true,
                    serverCalculatedDigest = incorrectDigest
                },
                ["4. Client:DigestValid & Server:DigestMissing"] = new TestIOStruct
                {
                    clientSuppliedReference = correctDigest
                },
                ["5. Client:DigestValid & Server:DigestValid"] = new TestIOStruct
                {
                    clientSuppliedReference = correctDigest,
                    serverCalculatedDigest = correctDigest
                },
                ["6. Client:DigestValid & Server:DigestWrongButSyntacticallyValid"] = new TestIOStruct
                {
                    clientSuppliedReference = correctDigest,
                    serverCalculatedDigest = incorrectDigest,
                    errExpectedOnHEAD = true,
                    errExpectedOnGET = true
                }
            };
        }

        /// <summary>
        /// AreDescriptorsEqual compares two descriptors and returns true if they are equal.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public bool AreDescriptorsEqual(Descriptor a, Descriptor b)
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
            repo.HttpClient = CustomClient(func);
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
            var blob = @"hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint)blob.Length
            };
            var index = @"{""manifests"":[]}"u8.ToArray();
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
                    if (req.Headers.TryGetValues("Content-Type", out var values) &&
                        !values.Contains("application/octet-stream"))
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
            repo.HttpClient = CustomClient(func);
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
            var blob = @"hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint)blob.Length
            };
            var index = @"{""manifests"":[]}"u8.ToArray();
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
                    if (req.Headers.TryGetValues("Accept", out var values) &&
                        !values.Contains(OCIMediaTypes.ImageIndex))
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
            repo.HttpClient = CustomClient(func);
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
            var blob = @"hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint)blob.Length
            };
            var blobDeleted = false;
            var index = @"{""manifests"":[]}"u8.ToArray();
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
            repo.HttpClient = CustomClient(func);
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
            var blob = @"hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint)blob.Length
            };
            var index = @"{""manifests"":[]}"u8.ToArray();
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
                    if (req.Headers.TryGetValues("Accept", out var values) &&
                        !values.Contains(OCIMediaTypes.ImageIndex))
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
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            await Assert.ThrowsAsync<NotFoundException>(async () =>
                await repo.ResolveAsync(blobDesc.Digest, cancellationToken));
            // await repo.ResolveAsync(blobDesc.Digest, cancellationToken);
            var got = await repo.ResolveAsync(indexDesc.Digest, cancellationToken);
            Assert.True(AreDescriptorsEqual(indexDesc, got));

            got = await repo.ResolveAsync(reference, cancellationToken);
            Assert.True(AreDescriptorsEqual(indexDesc, got));
            var tagDigestRef = "whatever" + "@" + indexDesc.Digest;
            got = await repo.ResolveAsync(tagDigestRef, cancellationToken);
            Assert.True(AreDescriptorsEqual(indexDesc, got));
            var fqdnRef = "localhost:5000/test" + ":" + tagDigestRef;
            got = await repo.ResolveAsync(fqdnRef, cancellationToken);
            Assert.True(AreDescriptorsEqual(indexDesc, got));
        }

        /// <summary>
        /// Repository_ResolveAsync tests the ResolveAsync method of the Repository
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Repository_TagAsync()
        {
            var blob = "hello"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint)blob.Length
            };
            var index = @"{""manifests"":[]}"u8.ToArray();
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

                if (req.Method == HttpMethod.Get &&
                    req.RequestUri.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)
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

                if (req.Method == HttpMethod.Put && req.RequestUri.AbsolutePath == "/v2/test/manifests/" + reference
                    || req.RequestUri.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)

                {
                    if (req.Headers.TryGetValues("Content-Type", out var values) &&
                        !values.Contains(OCIMediaTypes.ImageIndex))
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
            repo.HttpClient = CustomClient(func);
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
            var index = @"{""manifests"":[]}"u8.ToArray();
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
                    if (req.Headers.TryGetValues("Content-Type", out var values) &&
                        !values.Contains(OCIMediaTypes.ImageIndex))
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
            repo.HttpClient = CustomClient(func);
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
            var blob = "hello"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                Digest = CalculateDigest(blob),
                MediaType = "test",
                Size = (uint)blob.Length
            };
            var index = @"{""manifests"":[]}"u8.ToArray();
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
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();

            // test with blob digest
            await Assert.ThrowsAsync<NotFoundException>(
                async () => await repo.FetchReferenceAsync(blobDesc.Digest, cancellationToken));

            // test with manifest digest
            var data = await repo.FetchReferenceAsync(indexDesc.Digest, cancellationToken);
            Assert.True(AreDescriptorsEqual(indexDesc, data.Descriptor));
            var buf = new byte[data.Stream.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(index, buf);

            // test with manifest tag
            data = await repo.FetchReferenceAsync(reference, cancellationToken);
            Assert.True(AreDescriptorsEqual(indexDesc, data.Descriptor));
            buf = new byte[data.Stream.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(index, buf);

            // test with manifest tag@digest
            var tagDigestRef = "whatever" + "@" + indexDesc.Digest;
            data = await repo.FetchReferenceAsync(tagDigestRef, cancellationToken);
            Assert.True(AreDescriptorsEqual(indexDesc, data.Descriptor));
            buf = new byte[data.Stream.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(index, buf);

            // test with manifest FQDN
            var fqdnRef = "localhost:5000/test" + ":" + tagDigestRef;
            data = await repo.FetchReferenceAsync(fqdnRef, cancellationToken);
            Assert.True(AreDescriptorsEqual(indexDesc, data.Descriptor));

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
                new() {"the", "quick", "brown", "fox"},
                new() {"jumps", "over", "the", "lazy"},
                new() {"dog"}
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

                var listOfTags =  new ResponseTypes.TagList
                {
                    Tags = tags.ToArray()
                };
                res.Content = new StringContent(JsonSerializer.Serialize(listOfTags));
                return res;

            };

            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
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

        /// <summary>
        /// BlobStore_FetchAsync tests the FetchAsync method of the BlobStore
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task BlobStore_FetchAsync()
        {
            var blob = "hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(blob),
                Size = blob.Length
            };
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Get)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }

                if (req.RequestUri.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
                {
                    res.Content = new ByteArrayContent(blob);
                    res.Content.Headers.Add("Content-Type", "application/octet-stream");
                    res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    return res;
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };

            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new BlobStore(repo);
            var stream = await store.FetchAsync(blobDesc, cancellationToken);
            var buf = new byte[stream.Length];
            await stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(blob, buf);

        }

        /// <summary>
        /// BlobStore_FetchAsync_CanSeek tests the FetchAsync method of the BlobStore for a stream that can seek
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task BlobStore_FetchAsync_CanSeek()
        {
            var blob = "hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(blob),
                Size = blob.Length
            };
            var seekable = false;
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Get)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }

                if (req.RequestUri.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
                {
                    if (seekable)
                    {
                        res.Headers.AcceptRanges.Add("bytes");
                    }

                    IEnumerable<string> rangeHeader;
                    if (req.Headers.TryGetValues("Range", out rangeHeader))
                    {
                    }


                    if (!seekable || rangeHeader == null || rangeHeader.FirstOrDefault() == "")
                    {
                        res.StatusCode = HttpStatusCode.OK;
                        res.Content = new ByteArrayContent(blob);
                        res.Content.Headers.Add("Content-Type", "application/octet-stream");
                        res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                        return res;
                    }


                    long start = 0, end = 0;
                    try
                    {
                        start = req.Headers.Range.Ranges.First().From.Value;
                        end = req.Headers.Range.Ranges.First().To.Value;
                    }
                    catch (Exception e)
                    {
                        return new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
                    }

                    if (start < 0 || start > end || start >= blobDesc.Size)
                    {
                        return new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
                    }

                    end++;
                    if (end > blobDesc.Size)
                    {
                        end = blobDesc.Size;
                    }

                    res.StatusCode = HttpStatusCode.PartialContent;
                    res.Content = new ByteArrayContent(blob[(int)start..(int)end]);
                    res.Content.Headers.Add("Content-Type", "application/octet-stream");
                    res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    return res;
                }

                res.Content.Headers.Add("Content-Type", "application/octet-stream");
                res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                res.StatusCode = HttpStatusCode.NotFound;
                return res;
            };

            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new BlobStore(repo);
            var stream = await store.FetchAsync(blobDesc, cancellationToken);
            var buf = new byte[stream.Length];
            await stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(blob, buf);

            seekable = true;
            stream = await store.FetchAsync(blobDesc, cancellationToken);
            buf = new byte[stream.Length];
            await stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(blob, buf);

            buf = new byte[stream.Length - 3];
            stream.Seek(3, SeekOrigin.Begin);
            await stream.ReadAsync(buf, cancellationToken);
            var seg = blob[3..];
            Assert.Equal(seg, buf);
        }

        /// <summary>
        /// BlobStore_FetchAsync_ZeroSizedBlob tests the FetchAsync method of the BlobStore for a zero sized blob
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task BlobStore_FetchAsync_ZeroSizedBlob()
        {
            var blob = ""u8.ToArray();
            var blobDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(blob),
                Size = blob.Length
            };
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Get)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }

                if (req.RequestUri.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
                {
                    if (req.Headers.TryGetValues("Range", out var rangeHeader))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }

                    res.Content.Headers.Add("Content-Type", "application/octet-stream");
                    res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    return res;
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };

            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new BlobStore(repo);
            var stream = await store.FetchAsync(blobDesc, cancellationToken);
            var buf = new byte[stream.Length];
            await stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(blob, buf);
        }

        /// <summary>
        /// BlobStore_PushAsync tests the PushAsync method of the BlobStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task BlobStore_PushAsync()
        {
            var blob = "hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(blob),
                Size = blob.Length
            };
            var gotBlob = new byte[blob.Length];
            var uuid = Guid.NewGuid().ToString();
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method == HttpMethod.Post && req.RequestUri.AbsolutePath == $"/v2/test/blobs/uploads/")
                {
                    res.StatusCode = HttpStatusCode.Accepted;
                    res.Headers.Add("Location", "/v2/test/blobs/uploads/" + uuid);
                    return res;
                }

                if (req.Method == HttpMethod.Put && req.RequestUri.AbsolutePath == "/v2/test/blobs/uploads/" + uuid)
                {
                    if (req.Headers.TryGetValues("Content-Type", out var contentType) &&
                        contentType.FirstOrDefault() != "application/octet-stream")
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }

                    if (HttpUtility.ParseQueryString(req.RequestUri.Query)["digest"] != blobDesc.Digest)
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }

                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    // read content into buffer
                    var stream = req.Content!.ReadAsStream(cancellationToken);
                    stream.Read(gotBlob);
                    res.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    res.StatusCode = HttpStatusCode.Created;
                    return res;
                }

                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new BlobStore(repo);
            await store.PushAsync(blobDesc, new MemoryStream(blob), cancellationToken);
            Assert.Equal(blob, gotBlob);
        }

        /// <summary>
        /// BlobStore_ExistsAsync tests the ExistsAsync method of the BlobStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task BlobStore_ExistsAsync()
        {
            var blob = "hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(blob),
                Size = blob.Length
            };
            var content = "foobar"u8.ToArray();
            var contentDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(content),
                Size = content.Length
            };
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Head)
                {
                    res.StatusCode = HttpStatusCode.MethodNotAllowed;
                    return res;
                }

                if (req.RequestUri.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
                {
                    res.Content.Headers.Add("Content-Type", "application/octet-stream");
                    res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    res.Content.Headers.Add("Content-Length", blobDesc.Size.ToString());
                    return res;
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new BlobStore(repo);
            var exists = await store.ExistsAsync(blobDesc, cancellationToken);
            Assert.True(exists);
            exists = await store.ExistsAsync(contentDesc, cancellationToken);
            Assert.False(exists);
        }

        /// <summary>
        /// BlobStore_DeleteAsync tests the DeleteAsync method of the BlobStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task BlobStore_DeleteAsync()
        {
            var blob = "hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(blob),
                Size = blob.Length
            };
            var blobDeleted = false;
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Delete)
                {
                    res.StatusCode = HttpStatusCode.MethodNotAllowed;
                    return res;
                }

                if (req.RequestUri.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
                {
                    blobDeleted = true;
                    res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    res.StatusCode = HttpStatusCode.Accepted;
                    return res;
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new BlobStore(repo);
            await store.DeleteAsync(blobDesc, cancellationToken);
            Assert.True(blobDeleted);

            var content = "foobar"u8.ToArray();
            var contentDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(content),
                Size = content.Length
            };
            Assert.ThrowsAsync<NotFoundException>(async () => await store.DeleteAsync(contentDesc, cancellationToken));
        }

        /// <summary>
        /// BlobStore_ResolveAsync tests the ResolveAsync method of the BlobStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task BlobStore_ResolveAsync()
        {
            var blob = "hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(blob),
                Size = blob.Length
            };
            var reference = "foobar";
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Head)
                {
                    res.StatusCode = HttpStatusCode.MethodNotAllowed;
                    return res;
                }

                if (req.RequestUri.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
                {
                    res.Content.Headers.Add("Content-Type", "application/octet-stream");
                    res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    res.Content.Headers.Add("Content-Length", blobDesc.Size.ToString());
                    return res;
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new BlobStore(repo);
            var got = await store.ResolveAsync(blobDesc.Digest, cancellationToken);
            Assert.Equal(blobDesc.Digest, got.Digest);
            Assert.Equal(blobDesc.Size, got.Size);

            var fqdnRef = $"localhost:5000/test@{blobDesc.Digest}";
            got = await store.ResolveAsync(fqdnRef, cancellationToken);
            Assert.Equal(blobDesc.Digest, got.Digest);

            var content = "foobar"u8.ToArray();
            var contentDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(content),
                Size = content.Length
            };
            await Assert.ThrowsAsync<NotFoundException>(async () =>
                await store.ResolveAsync(contentDesc.Digest, cancellationToken));
        }

        /// <summary>
        /// BlobStore_FetchReferenceAsync tests the FetchReferenceAsync method of BlobStore
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task BlobStore_FetchReferenceAsync()
        {
            var blob = "hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(blob),
                Size = blob.Length
            };
            var reference = "foobar";
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Get)
                {
                    res.StatusCode = HttpStatusCode.MethodNotAllowed;
                    return res;
                }

                if (req.RequestUri.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
                {
                    res.Content = new ByteArrayContent(blob);
                    res.Content.Headers.Add("Content-Type", "application/octet-stream");
                    res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    return res;
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };

            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new BlobStore(repo);

            // test with digest
            var gotDesc = await store.FetchReferenceAsync(blobDesc.Digest, cancellationToken);
            Assert.Equal(blobDesc.Digest, gotDesc.Descriptor.Digest);
            Assert.Equal(blobDesc.Size, gotDesc.Descriptor.Size);

            var buf = new byte[gotDesc.Descriptor.Size];
            await gotDesc.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(blob, buf);

            // test with FQDN reference
            var fqdnRef = $"localhost:5000/test@{blobDesc.Digest}";
            gotDesc = await store.FetchReferenceAsync(fqdnRef, cancellationToken);
            Assert.Equal(blobDesc.Digest, gotDesc.Descriptor.Digest);
            Assert.Equal(blobDesc.Size, gotDesc.Descriptor.Size);

            var content = "foobar"u8.ToArray();
            var contentDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(content),
                Size = content.Length
            };
            // test with other digest
            await Assert.ThrowsAsync<NotFoundException>(async () =>
                await store.FetchReferenceAsync(contentDesc.Digest, cancellationToken));
        }

        /// <summary>
        /// BlobStore_FetchAsyncReferenceAsync_Seek tests the FetchAsync method of BlobStore with seek.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task BlobStore_FetchReferenceAsync_Seek()
        {
            var blob = "hello world"u8.ToArray();
            var blobDesc = new Descriptor()
            {
                MediaType = "test",
                Digest = CalculateDigest(blob),
                Size = blob.Length
            };
            var seekable = false;
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Get)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }

                if (req.RequestUri.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
                {
                    if (seekable)
                    {
                        res.Headers.AcceptRanges.Add("bytes");
                    }

                    IEnumerable<string> rangeHeader;
                    if (req.Headers.TryGetValues("Range", out rangeHeader))
                    {
                    }


                    if (!seekable || rangeHeader == null || rangeHeader.FirstOrDefault() == "")
                    {
                        res.StatusCode = HttpStatusCode.OK;
                        res.Content = new ByteArrayContent(blob);
                        res.Content.Headers.Add("Content-Type", "application/octet-stream");
                        res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                        return res;
                    }


                    long start = 0;
                    try
                    {
                        start = req.Headers.Range.Ranges.First().From.Value;
                    }
                    catch (Exception e)
                    {
                        return new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
                    }

                    if (start < 0 || start >= blobDesc.Size)
                    {
                        return new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
                    }

                    res.StatusCode = HttpStatusCode.PartialContent;
                    res.Content = new ByteArrayContent(blob[(int)start..]);
                    res.Content.Headers.Add("Content-Type", "application/octet-stream");
                    res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                    return res;
                }

                res.Content.Headers.Add("Content-Type", "application/octet-stream");
                res.Content.Headers.Add("Docker-Content-Digest", blobDesc.Digest);
                res.StatusCode = HttpStatusCode.NotFound;
                return res;
            };

            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();

            var store = new BlobStore(repo);

            // test non-seekable content

            var data = await store.FetchReferenceAsync(blobDesc.Digest, cancellationToken);

            Assert.Equal(data.Descriptor.Digest, blobDesc.Digest);
            Assert.Equal(data.Descriptor.Size, blobDesc.Size);

            var buf = new byte[data.Descriptor.Size];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(blob, buf);

            // test seekable content
            seekable = true;
            data = await store.FetchReferenceAsync(blobDesc.Digest, cancellationToken);
            Assert.Equal(data.Descriptor.Digest, blobDesc.Digest);
            Assert.Equal(data.Descriptor.Size, blobDesc.Size);

            data.Stream.Seek(3, SeekOrigin.Begin);
            buf = new byte[data.Descriptor.Size - 3];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(blob[3..], buf);
        }


        /// <summary>
        /// GenerateBlobDescriptor_WithVariusDockerContentDigestHeaders tests the GenerateBlobDescriptor method of BlobStore with various Docker-Content-Digest headers.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Fact]
        public async Task GenerateBlobDescriptor_WithVariousDockerContentDigestHeaders()
        {
            var reference = new RemoteReference()
            {
                Registry = "eastern.haan.com",
                Reference = "<calculate>",
                Repository = "from25to220ce"
            };
            var tests = GetTestIOStructMapForGetDescriptorClass();
            foreach ((string testName, TestIOStruct dcdIOStruct) in tests)
            {
                if (dcdIOStruct.isTag)
                {
                    continue;
                }
                HttpMethod[] methods = new HttpMethod[] { HttpMethod.Get, HttpMethod.Head };
                foreach ((int i, HttpMethod method) in methods.Select((value, i) => (i, value)))
                {
                    reference.Reference = dcdIOStruct.clientSuppliedReference;
                    var resp = new HttpResponseMessage();
                    if (method == HttpMethod.Get)
                    {
                        resp.Content = new ByteArrayContent(theAmazingBanClan);
                        resp.Content.Headers.Add("Content-Type", new string[] { "application/vnd.docker.distribution.manifest.v2+json" });
                        resp.Content.Headers.Add(dockerContentDigestHeader, new string[] { dcdIOStruct.serverCalculatedDigest });
                    }
                    if (!resp.Content.Headers.TryGetValues(dockerContentDigestHeader, out IEnumerable<string> values))
                    {
                        resp.Content.Headers.Add("Content-Type", new string[] { "application/vnd.docker.distribution.manifest.v2+json" });
                        resp.Content.Headers.Add(dockerContentDigestHeader, new string[] { dcdIOStruct.serverCalculatedDigest });
                        resp.RequestMessage = new HttpRequestMessage()
                        {
                            Method = method
                        };

                    }
                    else
                    {
                        resp.RequestMessage = new HttpRequestMessage()
                        {
                            Method = method
                        };
                    }

                    var d = string.Empty;
                    try
                    {
                        d = reference.Digest();
                    }
                    catch (Exception e)
                    {
                        throw new Exception(
                            $"[Blob.{method}] {testName}; got digest from a tag reference unexpectedly");
                    }

                    var errExpected = new bool[] { dcdIOStruct.errExpectedOnGET, dcdIOStruct.errExpectedOnHEAD }[i];
                    if (d.Length == 0)
                    {
                        // To avoid an otherwise impossible scenario in the tested code
                        // path, we set d so that verifyContentDigest does not break.
                        d = dcdIOStruct.serverCalculatedDigest;
                    }

                    var err = false;
                    try
                    {
                        Repository.GenerateBlobDescriptor(resp, d);

                    }
                    catch (Exception e)
                    {
                        err = true;
                        if (!errExpected)
                        {
                            throw new Exception(
                                $"[Blob.{method}] {testName}; expected no error for request, but got err; {e.Message}");
                        }

                    }

                    if (errExpected && !err)
                    {
                        throw new Exception($"[Blob.{method}] {testName}; expected error for request, but got none");
                    }
                }
            }
        }


        /// <summary>
        /// ManifestStore_FetchAsync tests the FetchAsync method of ManifestStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ManifestStore_FetchAsync()
        {
            var manifest = """{"layers":[]}"""u8.ToArray();
            var manifestDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageManifest,
                Digest = CalculateDigest(manifest),
                Size = manifest.Length
            };

            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Get)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }
                if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
                {
                    if (req.Headers.TryGetValues("Accept", out IEnumerable<string> values) && !values.Contains(OCIMediaTypes.ImageManifest))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                    res.Content = new ByteArrayContent(manifest);
                    res.Content.Headers.Add("Content-Type", new string[] { OCIMediaTypes.ImageManifest });
                    res.Content.Headers.Add("Docker-Content-Digest", new string[] { manifestDesc.Digest });
                    return res;
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new ManifestStore(repo);
            var data = await store.FetchAsync(manifestDesc, cancellationToken);
            var buf = new byte[data.Length];
            await data.ReadAsync(buf, cancellationToken);
            Assert.Equal(manifest, buf);

            var content = """{"manifests":[]}"""u8.ToArray();
            var contentDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageIndex,
                Digest = CalculateDigest(content),
                Size = content.Length
            };
            Assert.ThrowsAsync<NotFoundException>(async () => await store.FetchAsync(contentDesc, cancellationToken));
        }

        /// <summary>
        /// ManifestStore_PushAsync tests the PushAsync method of ManifestStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ManifestStore_PushAsync()
        {
            var manifest = """{"layers":[]}"""u8.ToArray();
            var manifestDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageManifest,
                Digest = CalculateDigest(manifest),
                Size = manifest.Length
            };
            byte[] gotManifest = null;

            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method == HttpMethod.Put && req.RequestUri.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
                {
                    if (req.Headers.TryGetValues("Content-Type", out IEnumerable<string> values) && !values.Contains(OCIMediaTypes.ImageManifest))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }

                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    req.Content.ReadAsByteArrayAsync().Result.CopyTo(buf, 0);
                    gotManifest = buf;
                    res.Content.Headers.Add("Docker-Content-Digest", new string[] { manifestDesc.Digest });
                    res.StatusCode = HttpStatusCode.Created;
                    return res;
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);
                }
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new ManifestStore(repo);
            await store.PushAsync(manifestDesc, new MemoryStream(manifest), cancellationToken);
            Assert.Equal(manifest, gotManifest);
        }

        /// <summary>
        /// ManifestStore_ExistAsync tests the ExistAsync method of ManifestStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ManifestStore_ExistAsync()
        {
            var manifest = """{"layers":[]}"""u8.ToArray();
            var manifestDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageManifest,
                Digest = CalculateDigest(manifest),
                Size = manifest.Length
            };
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Head)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }
                if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
                {
                    if (req.Headers.TryGetValues("Accept", out IEnumerable<string> values) && !values.Contains(OCIMediaTypes.ImageManifest))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                    res.Content.Headers.Add("Docker-Content-Digest", new string[] { manifestDesc.Digest });
                    res.Content.Headers.Add("Content-Type", new string[] { OCIMediaTypes.ImageManifest });
                    res.Content.Headers.Add("Content-Length", new string[] { manifest.Length.ToString() });
                    return res;
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new ManifestStore(repo);
            var exist = await store.ExistsAsync(manifestDesc, cancellationToken);
            Assert.True(exist);

            var content = """{"manifests":[]}"""u8.ToArray();
            var contentDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageIndex,
                Digest = CalculateDigest(content),
                Size = content.Length
            };
            exist = await store.ExistsAsync(contentDesc, cancellationToken);
            Assert.False(exist);
        }

        /// <summary>
        /// ManifestStore_DeleteAsync tests the DeleteAsync method of ManifestStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ManifestStore_DeleteAsync()
        {
            var manifest = """{"layers":[]}"""u8.ToArray();
            var manifestDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageManifest,
                Digest = CalculateDigest(manifest),
                Size = manifest.Length
            };
            var manifestDeleted = false;
            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method != HttpMethod.Delete && req.Method != HttpMethod.Get)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }
                if (req.Method == HttpMethod.Delete && req.RequestUri.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
                {
                    manifestDeleted = true;
                    res.StatusCode = HttpStatusCode.Accepted;
                    return res;
                }
                if (req.Method == HttpMethod.Get && req.RequestUri.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
                {
                    if (req.Headers.TryGetValues("Accept", out IEnumerable<string> values) && !values.Contains(OCIMediaTypes.ImageManifest))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                    res.Content = new ByteArrayContent(manifest);
                    res.Content.Headers.Add("Docker-Content-Digest", new string[] { manifestDesc.Digest });
                    res.Content.Headers.Add("Content-Type", new string[] { OCIMediaTypes.ImageManifest });
                    return res;
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new ManifestStore(repo);
            await store.DeleteAsync(manifestDesc, cancellationToken);
            Assert.True(manifestDeleted);

            var content = """{"manifests":[]}"""u8.ToArray();
            var contentDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageIndex,
                Digest = CalculateDigest(content),
                Size = content.Length
            };
            Assert.ThrowsAsync<NotFoundException>(async () => await store.DeleteAsync(contentDesc, cancellationToken));
        }

        /// <summary>
        /// ManifestStore_ResolveAsync tests the ResolveAsync method of ManifestStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ManifestStore_ResolveAsync()
        {
            var manifest = """{"layers":[]}"""u8.ToArray();
            var manifestDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageManifest,
                Digest = CalculateDigest(manifest),
                Size = manifest.Length
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
                if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}" || req.RequestUri.AbsolutePath == $"/v2/test/manifests/{reference}")
                {
                    if (req.Headers.TryGetValues("Accept", out IEnumerable<string> values) && !values.Contains(OCIMediaTypes.ImageManifest))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                    res.Content.Headers.Add("Docker-Content-Digest", new string[] { manifestDesc.Digest });
                    res.Content.Headers.Add("Content-Type", new string[] { OCIMediaTypes.ImageManifest });
                    res.Content.Headers.Add("Content-Length", new string[] { manifest.Length.ToString() });
                    return res;
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new ManifestStore(repo);
            var got = await store.ResolveAsync(manifestDesc.Digest, cancellationToken);
            Assert.True(AreDescriptorsEqual(manifestDesc, got));
            got = await store.ResolveAsync(reference, cancellationToken);
            Assert.True(AreDescriptorsEqual(manifestDesc, got));

            var tagDigestRef = "whatever" + "@" + manifestDesc.Digest;
            got = await store.ResolveAsync(tagDigestRef, cancellationToken);
            Assert.True(AreDescriptorsEqual(manifestDesc, got));

            var fqdnRef = "localhost:5000/test" + ":" + tagDigestRef;
            got = await store.ResolveAsync(fqdnRef, cancellationToken);
            Assert.True(AreDescriptorsEqual(manifestDesc, got));

            var content = """{"manifests":[]}"""u8.ToArray();
            var contentDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageIndex,
                Digest = CalculateDigest(content),
                Size = content.Length
            };
            Assert.ThrowsAsync<NotFoundException>(async () => await store.ResolveAsync(contentDesc.Digest, cancellationToken));

        }

        /// <summary>
        /// ManifestStore_FetchReferenceAsync tests the FetchReferenceAsync method of ManifestStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ManifestStore_FetchReferenceAsync()
        {
            var manifest = """{"layers":[]}"""u8.ToArray();
            var manifestDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageManifest,
                Digest = CalculateDigest(manifest),
                Size = manifest.Length
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
                if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}" || req.RequestUri.AbsolutePath == $"/v2/test/manifests/{reference}")
                {
                    if (req.Headers.TryGetValues("Accept", out IEnumerable<string> values) && !values.Contains(OCIMediaTypes.ImageManifest))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                    res.Content = new ByteArrayContent(manifest);
                    res.Content.Headers.Add("Docker-Content-Digest", new string[] { manifestDesc.Digest });
                    res.Content.Headers.Add("Content-Type", new string[] { OCIMediaTypes.ImageManifest });
                    return res;
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new ManifestStore(repo);

            // test with tag
            var data = await store.FetchReferenceAsync(reference, cancellationToken);
            Assert.True(AreDescriptorsEqual(manifestDesc, data.Descriptor));
            var buf = new byte[manifest.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(manifest, buf);

            // test with other tag
            var randomRef = "whatever";
            await Assert.ThrowsAsync<NotFoundException>(async () => await store.FetchReferenceAsync(randomRef, cancellationToken));

            // test with digest
            data = await store.FetchReferenceAsync(manifestDesc.Digest, cancellationToken);
            Assert.True(AreDescriptorsEqual(manifestDesc, data.Descriptor));

            buf = new byte[manifest.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(manifest, buf);

            // test with tag@digest
            var tagDigestRef = randomRef + "@" + manifestDesc.Digest;
            data = await store.FetchReferenceAsync(tagDigestRef, cancellationToken);
            Assert.True(AreDescriptorsEqual(manifestDesc, data.Descriptor));
            buf = new byte[manifest.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(manifest, buf);

            // test with FQDN
            var fqdnRef = "localhost:5000/test" + ":" + tagDigestRef;
            data = await store.FetchReferenceAsync(fqdnRef, cancellationToken);
            Assert.True(AreDescriptorsEqual(manifestDesc, data.Descriptor));
            buf = new byte[manifest.Length];
            await data.Stream.ReadAsync(buf, cancellationToken);
            Assert.Equal(manifest, buf);
        }

        /// <summary>
        /// ManifestStore_TagAsync tests the TagAsync method of ManifestStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ManifestStore_TagAsync()
        {
            var blob = "hello world"u8.ToArray();
            var blobDesc = new Descriptor
            {
                MediaType = "test",
                Digest = CalculateDigest(blob),
                Size = blob.Length
            };
            var index = """{"manifests":[]}"""u8.ToArray();
            var indexDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageIndex,
                Digest = CalculateDigest(index),
                Size = index.Length
            };
            var gotIndex = new byte[index.Length];
            var reference = "foobar";

            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;
                if (req.Method == HttpMethod.Get && req.RequestUri.AbsolutePath == $"/v2/test/manifests/{blobDesc.Digest}")
                {
                    res.StatusCode = HttpStatusCode.NotFound;
                    return res;
                }
                if (req.Method == HttpMethod.Get && req.RequestUri.AbsolutePath == $"/v2/test/manifests/{indexDesc.Digest}")
                {
                    if (req.Headers.TryGetValues("Accept", out IEnumerable<string> values) && !values.Contains(indexDesc.MediaType))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                    res.Content = new ByteArrayContent(index);
                    res.Content.Headers.Add("Docker-Content-Digest", new string[] { indexDesc.Digest });
                    res.Content.Headers.Add("Content-Type", new string[] { indexDesc.MediaType });
                    return res;
                }
                if (req.Method == HttpMethod.Put && req.RequestUri.AbsolutePath == $"/v2/test/manifests/{reference}" || req.RequestUri.AbsolutePath == $"/v2/test/manifests/{indexDesc.Digest}")
                {
                    if (req.Headers.TryGetValues("Content-Type", out IEnumerable<string> values) && !values.Contains(indexDesc.MediaType))
                    {
                        res.StatusCode = HttpStatusCode.BadRequest;
                        return res;
                    }
                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    req.Content.ReadAsByteArrayAsync().Result.CopyTo(buf, 0);
                    gotIndex = buf;
                    res.Content.Headers.Add("Docker-Content-Digest", new string[] { indexDesc.Digest });
                    res.StatusCode = HttpStatusCode.Created;
                    return res;
                }

                res.StatusCode = HttpStatusCode.Forbidden;
                return res;
            };

            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new ManifestStore(repo);

            Assert.ThrowsAnyAsync<Exception>(async () => await store.TagAsync(blobDesc, reference, cancellationToken));

            await store.TagAsync(indexDesc, reference, cancellationToken);
            Assert.Equal(index, gotIndex);

            gotIndex = null;
            await store.TagAsync(indexDesc, indexDesc.Digest, cancellationToken);
            Assert.Equal(index, gotIndex);
        }

        /// <summary>
        /// ManifestStore_PushReferenceAsync tests the PushReferenceAsync of ManifestStore.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ManifestStore_PushReferenceAsync()
        {
            var index = """{"manifests":[]}"""u8.ToArray();
            var indexDesc = new Descriptor
            {
                MediaType = OCIMediaTypes.ImageIndex,
                Digest = CalculateDigest(index),
                Size = index.Length
            };
            var gotIndex = new byte[index.Length];
            var reference = "foobar";

            var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
            {
                var res = new HttpResponseMessage();
                res.RequestMessage = req;

                if (req.Method == HttpMethod.Put && req.RequestUri.AbsolutePath == $"/v2/test/manifests/{reference}")
                {
                    if (req.Headers.TryGetValues("Content-Type", out IEnumerable<string> values) && !values.Contains(indexDesc.MediaType))
                    {
                        res.StatusCode = HttpStatusCode.BadRequest;
                        return res;
                    }
                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    req.Content.ReadAsByteArrayAsync().Result.CopyTo(buf, 0);
                    gotIndex = buf;
                    res.Content.Headers.Add("Docker-Content-Digest", new string[] { indexDesc.Digest });
                    res.StatusCode = HttpStatusCode.Created;
                    return res;
                }
                res.StatusCode = HttpStatusCode.Forbidden;
                return res;
            };
            var repo = new Repository("localhost:5000/test");
            repo.HttpClient = CustomClient(func);
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            var store = new ManifestStore(repo);
            await store.PushReferenceAsync(indexDesc, new MemoryStream(index), reference, cancellationToken);
            Assert.Equal(index, gotIndex);
        }


        public async Task ManifestStore_generateDescriptorWithVariousDockerContentDigestHeaders()
        {
            var reference = new RemoteReference()
            {
                Registry = "eastern.haan.com",
                Reference = "<calculate>",
                Repository = "from25to220ce"
            };
            var tests = GetTestIOStructMapForGetDescriptorClass();
            foreach ((string testName, TestIOStruct dcdIOStruct) in tests)
            {
                var repo = new Repository(reference.Repository + "/" + reference.Repository);
                HttpMethod[] methods = new HttpMethod[] { HttpMethod.Get, HttpMethod.Head };
                var s = new ManifestStore(repo);
                foreach ((int i, HttpMethod method) in methods.Select((value, i) => (i, value)))
                {
                    reference.Reference = dcdIOStruct.clientSuppliedReference;
                    var resp = new HttpResponseMessage();
                    if (method == HttpMethod.Get)
                    {
                        resp.Content = new ByteArrayContent(theAmazingBanClan);
                        resp.Content.Headers.Add("Content-Type", new string[] { "application/vnd.docker.distribution.manifest.v2+json" });
                        resp.Content.Headers.Add(dockerContentDigestHeader, new string[] { dcdIOStruct.serverCalculatedDigest });
                    }
                    else
                    {
                        resp.Content.Headers.Add("Content-Type", new string[] { "application/vnd.docker.distribution.manifest.v2+json" });
                        resp.Content.Headers.Add(dockerContentDigestHeader, new string[] { dcdIOStruct.serverCalculatedDigest });
                    }
                    resp.RequestMessage = new HttpRequestMessage()
                    {
                        Method = method
                    };

                    var errExpected = new bool[] { dcdIOStruct.errExpectedOnGET, dcdIOStruct.errExpectedOnHEAD }[i];

                    var err = false;
                    try
                    {
                        s.GenerateDescriptor(resp, reference, method);
                    }
                    catch (Exception e)
                    {
                        err = true;
                        if (!errExpected)
                        {
                            throw new Exception(
                                $"[Manifest.{method}] {testName}; expected no error for request, but got err; {e.Message}");
                        }

                    }
                    if (errExpected && !err)
                    {
                        throw new Exception($"[Manifest.{method}] {testName}; expected error for request, but got none");
                    }
                }
            }

        }
    }
}
