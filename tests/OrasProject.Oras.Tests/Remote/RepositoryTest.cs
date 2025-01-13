// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Xunit;
using static OrasProject.Oras.Content.Digest;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OrasProject.Oras.Tests.Remote;

public class RepositoryTest
{
    public struct TestIOStruct
    {
        public bool IsTag;
        public bool ErrExpectedOnHEAD;
        public string ServerCalculatedDigest;
        public string ClientSuppliedReference;
        public bool ErrExpectedOnGET;
    }

    private byte[] _theAmazingBanClan = "Ban Gu, Ban Chao, Ban Zhao"u8.ToArray();
    private const string _theAmazingBanDigest = "b526a4f2be963a2f9b0990c001255669eab8a254ab1a6e3f84f1820212ac7078";

    private const string _dockerContentDigestHeader = "Docker-Content-Digest";

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
        string correctDigest = $"sha256:{_theAmazingBanDigest}";
        string incorrectDigest = $"sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

        return new Dictionary<string, TestIOStruct>
        {
            ["1. Client:Tag & Server:DigestMissing"] = new TestIOStruct
            {
                IsTag = true,
                ErrExpectedOnHEAD = true
            },
            ["2. Client:Tag & Server:DigestValid"] = new TestIOStruct
            {
                IsTag = true,
                ServerCalculatedDigest = correctDigest
            },
            ["3. Client:Tag & Server:DigestWrongButSyntacticallyValid"] = new TestIOStruct
            {
                IsTag = true,
                ServerCalculatedDigest = incorrectDigest
            },
            ["4. Client:DigestValid & Server:DigestMissing"] = new TestIOStruct
            {
                ClientSuppliedReference = correctDigest
            },
            ["5. Client:DigestValid & Server:DigestValid"] = new TestIOStruct
            {
                ClientSuppliedReference = correctDigest,
                ServerCalculatedDigest = correctDigest
            },
            ["6. Client:DigestValid & Server:DigestWrongButSyntacticallyValid"] = new TestIOStruct
            {
                ClientSuppliedReference = correctDigest,
                ServerCalculatedDigest = incorrectDigest,
                ErrExpectedOnHEAD = true,
                ErrExpectedOnGET = true
            }
        };
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
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        var index = """{"manifests":[]}"""u8.ToArray();
        var indexDesc = new Descriptor()
        {
            Digest = ComputeSHA256(index),
            MediaType = MediaType.ImageIndex,
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
                resp.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                return resp;
            }

            if (path == "/v2/test/manifests/" + indexDesc.Digest)
            {
                if (!req.Headers.Accept.Contains(new MediaTypeWithQualityHeaderValue(MediaType.ImageIndex)))
                {
                    resp.StatusCode = HttpStatusCode.BadRequest;
                    Debug.WriteLine("manifest not convertable: " + req.Headers.Accept);
                    return resp;
                }

                resp.Content = new ByteArrayContent(index);
                resp.Content.Headers.Add("Content-Type", indexDesc.MediaType);
                resp.Headers.Add(_dockerContentDigestHeader, indexDesc.Digest);
                return resp;
            }

            resp.StatusCode = HttpStatusCode.NotFound;
            return resp;
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        var index = @"{""manifests"":[]}"u8.ToArray();
        var indexDesc = new Descriptor()
        {
            Digest = ComputeSHA256(index),
            MediaType = MediaType.ImageIndex,
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

                var queries = HttpUtility.ParseQueryString(req.RequestUri.Query);
                if (queries["digest"] != blobDesc.Digest)
                {
                    resp.StatusCode = HttpStatusCode.BadRequest;
                    return resp;
                }

                var stream = req.Content!.ReadAsStream(cancellationToken);
                stream.Read(gotBlob);
                resp.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                resp.StatusCode = HttpStatusCode.Created;
                return resp;

            }

            if (req.Method == HttpMethod.Put &&
                req.RequestUri!.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)
            {
                if (req.Headers.TryGetValues("Content-Type", out var values) &&
                    !values.Contains(MediaType.ImageIndex))
                {
                    resp.StatusCode = HttpStatusCode.BadRequest;
                    return resp;
                }

                var stream = req.Content!.ReadAsStream(cancellationToken);
                stream.Read(gotIndex);
                resp.Headers.Add(_dockerContentDigestHeader, indexDesc.Digest);
                resp.StatusCode = HttpStatusCode.Created;
                return resp;
            }

            resp.StatusCode = HttpStatusCode.Forbidden;
            return resp;

        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        var index = @"{""manifests"":[]}"u8.ToArray();
        var indexDesc = new Descriptor()
        {
            Digest = ComputeSHA256(index),
            MediaType = MediaType.ImageIndex,
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
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                return res;
            }

            if (req.RequestUri!.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)
            {
                if (req.Headers.TryGetValues("Accept", out var values) &&
                    !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotAcceptable);
                }

                res.Content.Headers.Add("Content-Type", indexDesc.MediaType);
                res.Content.Headers.Add("Content-Length", indexDesc.Size.ToString());
                res.Headers.Add(_dockerContentDigestHeader, indexDesc.Digest);
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        var blobDeleted = false;
        var index = @"{""manifests"":[]}"u8.ToArray();
        var indexDesc = new Descriptor()
        {
            Digest = ComputeSHA256(index),
            MediaType = MediaType.ImageIndex,
            Size = index.Length
        };
        var indexDeleted = false;
        var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method != HttpMethod.Delete && req.Method != HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            if (req.Method == HttpMethod.Delete && req.RequestUri!.AbsolutePath == "/v2/test/blobs/" + blobDesc.Digest)
            {
                blobDeleted = true;
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                res.StatusCode = HttpStatusCode.Accepted;
                return res;
            }

            if (req.Method == HttpMethod.Delete && req.RequestUri!.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)
            {
                indexDeleted = true;
                // no dockerContentDigestHeader header for manifest deletion
                res.StatusCode = HttpStatusCode.Accepted;
                return res;
            }
            
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{indexDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                res.Content = new ByteArrayContent(index);
                res.Headers.Add(_dockerContentDigestHeader, new string[] { indexDesc.Digest });
                res.Content.Headers.Add("Content-Type", new string[] { MediaType.ImageIndex });
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        var index = @"{""manifests"":[]}"u8.ToArray();
        var indexDesc = new Descriptor()
        {
            Digest = ComputeSHA256(index),
            MediaType = MediaType.ImageIndex,
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
                    !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                res.Content.Headers.Add("Content-Type", indexDesc.MediaType);
                res.Content.Headers.Add("Content-Length", indexDesc.Size.ToString());
                res.Headers.Add(_dockerContentDigestHeader, indexDesc.Digest);
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        var index = @"{""manifests"":[]}"u8.ToArray();
        var indexDesc = new Descriptor()
        {
            Digest = ComputeSHA256(index),
            MediaType = MediaType.ImageIndex,
            Size = index.Length
        };
        byte[]? gotIndex = null;
        var reference = "foobar";

        var func = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method == HttpMethod.Get &&
                req.RequestUri?.AbsolutePath == "/v2/test/manifests/" + blobDesc.Digest)
            {
                return new HttpResponseMessage(HttpStatusCode.Found);
            }

            if (req.Method == HttpMethod.Get &&
                req.RequestUri?.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)
            {
                if (req.Headers.TryGetValues("Accept", out var values) &&
                    !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                res.Content = new ByteArrayContent(index);
                res.Content.Headers.Add("Content-Type", indexDesc.MediaType);
                res.Headers.Add(_dockerContentDigestHeader, indexDesc.Digest);
                return res;
            }

            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == "/v2/test/manifests/" + reference
                || req.RequestUri?.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest)

            {
                if (req.Headers.TryGetValues("Content-Type", out var values) &&
                    !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                if (req.Content != null)
                {
                    gotIndex = await req.Content.ReadAsByteArrayAsync(cancellationToken);
                }
                res.Headers.Add(_dockerContentDigestHeader, indexDesc.Digest);
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(index),
            MediaType = MediaType.ImageIndex,
            Size = index.Length
        };
        byte[]? gotIndex = null;
        var reference = "foobar";

        var func = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == "/v2/test/manifests/" + reference)
            {
                if (req.Headers.TryGetValues("Content-Type", out var values) &&
                    !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                if (req.Content != null)
                {
                    gotIndex = await req.Content.ReadAsByteArrayAsync();
                }
                res.Headers.Add(_dockerContentDigestHeader, indexDesc.Digest);
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var streamContent = new MemoryStream(index);
        await repo.PushAsync(indexDesc, streamContent, reference, cancellationToken);
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
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        var index = @"{""manifests"":[]}"u8.ToArray();
        var indexDesc = new Descriptor()
        {
            Digest = ComputeSHA256(index),
            MediaType = MediaType.ImageIndex,
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

            if (req.RequestUri?.AbsolutePath == "/v2/test/manifests/" + blobDesc.Digest)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (req.RequestUri?.AbsolutePath == "/v2/test/manifests/" + indexDesc.Digest
                || req.RequestUri?.AbsolutePath == "/v2/test/manifests/" + reference)
            {
                if (req.Headers.TryGetValues("Accept", out var values) &&
                    !values.Contains(MediaType.ImageIndex))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                res.Content = new ByteArrayContent(index);
                res.Content.Headers.Add("Content-Type", indexDesc.MediaType);
                res.Headers.Add(_dockerContentDigestHeader, indexDesc.Digest);
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.Found);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();

        // test with blob digest
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await repo.FetchAsync(blobDesc.Digest, cancellationToken));

        // test with manifest digest
        var data = await repo.FetchAsync(indexDesc.Digest, cancellationToken);
        Assert.True(AreDescriptorsEqual(indexDesc, data.Descriptor));
        var buf = new byte[data.Stream.Length];
        await data.Stream.ReadAsync(buf, cancellationToken);
        Assert.Equal(index, buf);

        // test with manifest tag
        data = await repo.FetchAsync(reference, cancellationToken);
        Assert.True(AreDescriptorsEqual(indexDesc, data.Descriptor));
        buf = new byte[data.Stream.Length];
        await data.Stream.ReadAsync(buf, cancellationToken);
        Assert.Equal(index, buf);

        // test with manifest tag@digest
        var tagDigestRef = "whatever" + "@" + indexDesc.Digest;
        data = await repo.FetchAsync(tagDigestRef, cancellationToken);
        Assert.True(AreDescriptorsEqual(indexDesc, data.Descriptor));
        buf = new byte[data.Stream.Length];
        await data.Stream.ReadAsync(buf, cancellationToken);
        Assert.Equal(index, buf);

        // test with manifest FQDN
        var fqdnRef = "localhost:5000/test" + ":" + tagDigestRef;
        data = await repo.FetchAsync(fqdnRef, cancellationToken);
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
                req.RequestUri?.AbsolutePath != "/v2/test/tags/list"
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
            catch
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

            var listOfTags = new Repository.TagList
            {
                Tags = tags.ToArray()
            };
            res.Content = new StringContent(JsonSerializer.Serialize(listOfTags));
            return res;

        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
            TagListPageSize = 4,
        });

        var cancellationToken = new CancellationToken();

        var wantTags = new List<string>();
        foreach (var set in tagSet)
        {
            wantTags.AddRange(set);
        }
        var gotTags = new List<string>();
        await foreach (var tag in repo.ListTagsAsync().WithCancellation(cancellationToken))
        {
            gotTags.Add(tag);
        }
        Assert.Equal(wantTags, gotTags);
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
            Digest = ComputeSHA256(blob),
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

            if (req.RequestUri?.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
            {
                res.Content = new ByteArrayContent(blob);
                res.Content.Headers.Add("Content-Type", "application/octet-stream");
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(blob),
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

            if (req.RequestUri?.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
            {
                if (seekable)
                {
                    res.Headers.AcceptRanges.Add("bytes");
                }

                if (req.Headers.TryGetValues("Range", out IEnumerable<string>? rangeHeader))
                {
                }


                if (!seekable || rangeHeader == null || rangeHeader.FirstOrDefault() == "")
                {
                    res.StatusCode = HttpStatusCode.OK;
                    res.Content = new ByteArrayContent(blob);
                    res.Content.Headers.Add("Content-Type", "application/octet-stream");
                    res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                    return res;
                }


                long start = -1, end = -1;
                var hv = req.Headers?.Range?.Ranges?.FirstOrDefault();
                if (hv != null && hv.From.HasValue && hv.To.HasValue)
                {
                    start = hv.From.Value;
                    end = hv.To.Value;
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
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                return res;
            }

            res.Content.Headers.Add("Content-Type", "application/octet-stream");
            res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
            res.StatusCode = HttpStatusCode.NotFound;
            return res;
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(blob),
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

            if (req.RequestUri?.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Range", out var rangeHeader))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                res.Content.Headers.Add("Content-Type", "application/octet-stream");
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(blob),
            Size = blob.Length
        };
        var gotBlob = new byte[blob.Length];
        var uuid = Guid.NewGuid().ToString();
        var existingQueryParameter = "existingParam=value";

        var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method == HttpMethod.Post && req.RequestUri?.AbsolutePath == $"/v2/test/blobs/uploads/")
            {
                res.StatusCode = HttpStatusCode.Accepted;
                res.Headers.Add("Location", $"/v2/test/blobs/uploads/{uuid}?{existingQueryParameter}");
                return res;
            }

            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == "/v2/test/blobs/uploads/" + uuid)
            {
                // Assert that the existing query parameter is present
                var queryParameters = HttpUtility.ParseQueryString(req.RequestUri.Query);
                Assert.Equal("value", queryParameters["existingParam"]);

                if (req.Headers.TryGetValues("Content-Type", out var contentType) &&
                    contentType.FirstOrDefault() != "application/octet-stream")
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                if (HttpUtility.ParseQueryString(req.RequestUri.Query)["digest"] != blobDesc.Digest)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                // read content into buffer
                var stream = req.Content!.ReadAsStream(cancellationToken);
                stream.Read(gotBlob);
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(blob),
            Size = blob.Length
        };
        var content = "foobar"u8.ToArray();
        var contentDesc = new Descriptor()
        {
            MediaType = "test",
            Digest = ComputeSHA256(content),
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

            if (req.RequestUri?.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
            {
                res.Content.Headers.Add("Content-Type", "application/octet-stream");
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                res.Content.Headers.Add("Content-Length", blobDesc.Size.ToString());
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(blob),
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

            if (req.RequestUri?.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
            {
                blobDeleted = true;
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                res.StatusCode = HttpStatusCode.Accepted;
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new BlobStore(repo);
        await store.DeleteAsync(blobDesc, cancellationToken);
        Assert.True(blobDeleted);

        var content = "foobar"u8.ToArray();
        var contentDesc = new Descriptor()
        {
            MediaType = "test",
            Digest = ComputeSHA256(content),
            Size = content.Length
        };
        await Assert.ThrowsAsync<NotFoundException>(async () => await store.DeleteAsync(contentDesc, cancellationToken));
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
            Digest = ComputeSHA256(blob),
            Size = blob.Length
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

            if (req.RequestUri?.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
            {
                res.Content.Headers.Add("Content-Type", "application/octet-stream");
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                res.Content.Headers.Add("Content-Length", blobDesc.Size.ToString());
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            Digest = ComputeSHA256(content),
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
            Digest = ComputeSHA256(blob),
            Size = blob.Length
        };
        var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method != HttpMethod.Get)
            {
                res.StatusCode = HttpStatusCode.MethodNotAllowed;
                return res;
            }

            if (req.RequestUri?.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
            {
                res.Content = new ByteArrayContent(blob);
                res.Content.Headers.Add("Content-Type", "application/octet-stream");
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new BlobStore(repo);

        // test with digest
        var gotDesc = await store.FetchAsync(blobDesc.Digest, cancellationToken);
        Assert.Equal(blobDesc.Digest, gotDesc.Descriptor.Digest);
        Assert.Equal(blobDesc.Size, gotDesc.Descriptor.Size);

        var buf = new byte[gotDesc.Descriptor.Size];
        await gotDesc.Stream.ReadAsync(buf, cancellationToken);
        Assert.Equal(blob, buf);

        // test with FQDN reference
        var fqdnRef = $"localhost:5000/test@{blobDesc.Digest}";
        gotDesc = await store.FetchAsync(fqdnRef, cancellationToken);
        Assert.Equal(blobDesc.Digest, gotDesc.Descriptor.Digest);
        Assert.Equal(blobDesc.Size, gotDesc.Descriptor.Size);

        var content = "foobar"u8.ToArray();
        var contentDesc = new Descriptor()
        {
            MediaType = "test",
            Digest = ComputeSHA256(content),
            Size = content.Length
        };
        // test with other digest
        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await store.FetchAsync(contentDesc.Digest, cancellationToken));
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
            Digest = ComputeSHA256(blob),
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

            if (req.RequestUri?.AbsolutePath == $"/v2/test/blobs/{blobDesc.Digest}")
            {
                if (seekable)
                {
                    res.Headers.AcceptRanges.Add("bytes");
                }

                if (req.Headers.TryGetValues("Range", out IEnumerable<string>? rangeHeader))
                {
                }


                if (!seekable || rangeHeader == null || rangeHeader.FirstOrDefault() == "")
                {
                    res.StatusCode = HttpStatusCode.OK;
                    res.Content = new ByteArrayContent(blob);
                    res.Content.Headers.Add("Content-Type", "application/octet-stream");
                    res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                    return res;
                }


                var hv = req.Headers?.Range?.Ranges?.FirstOrDefault();
                var start = hv != null && hv.To.HasValue ? hv.To.Value : -1;
                if (start < 0 || start >= blobDesc.Size)
                {
                    return new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
                }

                res.StatusCode = HttpStatusCode.PartialContent;
                res.Content = new ByteArrayContent(blob[(int)start..]);
                res.Content.Headers.Add("Content-Type", "application/octet-stream");
                res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                return res;
            }

            res.Content.Headers.Add("Content-Type", "application/octet-stream");
            res.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
            res.StatusCode = HttpStatusCode.NotFound;
            return res;
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();

        var store = new BlobStore(repo);

        // test non-seekable content

        var data = await store.FetchAsync(blobDesc.Digest, cancellationToken);

        Assert.Equal(data.Descriptor.Digest, blobDesc.Digest);
        Assert.Equal(data.Descriptor.Size, blobDesc.Size);

        var buf = new byte[data.Descriptor.Size];
        await data.Stream.ReadAsync(buf, cancellationToken);
        Assert.Equal(blob, buf);

        // test seekable content
        seekable = true;
        data = await store.FetchAsync(blobDesc.Digest, cancellationToken);
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
    public void GenerateBlobDescriptor_WithVariousDockerContentDigestHeaders()
    {
        var reference = new Reference("eastern.haan.com", "from25to220ce");
        var tests = GetTestIOStructMapForGetDescriptorClass();
        foreach ((string testName, TestIOStruct dcdIOStruct) in tests)
        {
            if (dcdIOStruct.IsTag)
            {
                continue;
            }
            HttpMethod[] methods = new HttpMethod[] { HttpMethod.Get, HttpMethod.Head };
            foreach ((int i, HttpMethod method) in methods.Select((value, i) => (i, value)))
            {
                reference.ContentReference = dcdIOStruct.ClientSuppliedReference;
                var resp = new HttpResponseMessage();
                if (method == HttpMethod.Get)
                {
                    resp.Content = new ByteArrayContent(_theAmazingBanClan);
                    resp.Content.Headers.Add("Content-Type", new string[] { "application/vnd.docker.distribution.manifest.v2+json" });
                    resp.Headers.Add(_dockerContentDigestHeader, new string[] { dcdIOStruct.ServerCalculatedDigest });
                }
                if (!resp.Headers.TryGetValues(_dockerContentDigestHeader, out IEnumerable<string>? values))
                {
                    resp.Content.Headers.Add("Content-Type", new string[] { "application/vnd.docker.distribution.manifest.v2+json" });
                    resp.Headers.Add(_dockerContentDigestHeader, new string[] { dcdIOStruct.ServerCalculatedDigest });
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
                    d = reference.Digest;
                }
                catch
                {
                    throw new Exception(
                        $"[Blob.{method}] {testName}; got digest from a tag reference unexpectedly");
                }

                var errExpected = new bool[] { dcdIOStruct.ErrExpectedOnGET, dcdIOStruct.ErrExpectedOnHEAD }[i];
                if (d.Length == 0)
                {
                    // To avoid an otherwise impossible scenario in the tested code
                    // path, we set d so that verifyContentDigest does not break.
                    d = dcdIOStruct.ServerCalculatedDigest;
                }

                var err = false;
                try
                {
                    resp.GenerateBlobDescriptor(d);
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
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(manifest),
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
            if (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                res.Content = new ByteArrayContent(manifest);
                res.Content.Headers.Add("Content-Type", new string[] { MediaType.ImageManifest });
                res.Headers.Add(_dockerContentDigestHeader, new string[] { manifestDesc.Digest });
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        var data = await store.FetchAsync(manifestDesc, cancellationToken);
        var buf = new byte[data.Length];
        await data.ReadAsync(buf, cancellationToken);
        Assert.Equal(manifest, buf);

        var content = """{"manifests":[]}"""u8.ToArray();
        var contentDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSHA256(content),
            Size = content.Length
        };
        await Assert.ThrowsAsync<NotFoundException>(async () => await store.FetchAsync(contentDesc, cancellationToken));
    }

    [Fact]
    public async Task ManifestStore_FetchAsync_ManifestUnknown()
    {
        var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            res.RequestMessage = req;
            if (req.Method != HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }
            if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
            res.Content = new StringContent("""{"errors":[{"code":"UNAUTHORIZED","message":"authentication required","detail":[{"Type":"repository","Class":"","Name":"repo","Action":"pull"}]}]}""");
            return res;
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        try
        {
            var data = await store.FetchAsync("hello", cancellationToken);
            Assert.Fail();
        }
        catch (ResponseException e)
        {
            Assert.Equal("UNAUTHORIZED", e.Errors?[0].Code);
        }
    }

    /// <summary>
    /// ManifestStore_PushAsync tests the PushAsync method of ManifestStore.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ManifestStore_PushAsync()
    {
        var (_, manifestBytes) = RandomManifest();
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(manifestBytes),
            Size = manifestBytes.Length
        };
        byte[]? gotManifest = null;

        var func = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Content-Type", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync()).CopyTo(buf, 0);
                    gotManifest = buf;
                }
                res.Headers.Add(_dockerContentDigestHeader, new string[] { manifestDesc.Digest });
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        await store.PushAsync(manifestDesc, new MemoryStream(manifestBytes), cancellationToken);
        Assert.Equal(manifestBytes, gotManifest);
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
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(manifest),
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
            if (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                res.Headers.Add(_dockerContentDigestHeader, new string[] { manifestDesc.Digest });
                res.Content.Headers.Add("Content-Type", new string[] { MediaType.ImageManifest });
                res.Content.Headers.Add("Content-Length", new string[] { manifest.Length.ToString() });
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        var exist = await store.ExistsAsync(manifestDesc, cancellationToken);
        Assert.True(exist);

        var content = """{"manifests":[]}"""u8.ToArray();
        var contentDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSHA256(content),
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
        var (_, manifestBytes) = RandomManifest();
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(manifestBytes),
            Size = manifestBytes.Length
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
            if (req.Method == HttpMethod.Delete && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
            {
                manifestDeleted = true;
                res.StatusCode = HttpStatusCode.Accepted;
                return res;
            }
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                res.Content = new ByteArrayContent(manifestBytes);
                res.Headers.Add(_dockerContentDigestHeader, new string[] { manifestDesc.Digest });
                res.Content.Headers.Add("Content-Type", new string[] { MediaType.ImageManifest });
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        await store.DeleteAsync(manifestDesc, cancellationToken);
        Assert.True(manifestDeleted);

        var content = """{"manifests":[]}"""u8.ToArray();
        var contentDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSHA256(content),
            Size = content.Length
        };
        await Assert.ThrowsAsync<NotFoundException>(async () => await store.DeleteAsync(contentDesc, cancellationToken));
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
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(manifest),
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
            if (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}" || req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{reference}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                res.Headers.Add(_dockerContentDigestHeader, new string[] { manifestDesc.Digest });
                res.Content.Headers.Add("Content-Type", new string[] { MediaType.ImageManifest });
                res.Content.Headers.Add("Content-Length", new string[] { manifest.Length.ToString() });
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
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
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSHA256(content),
            Size = content.Length
        };

        await Assert.ThrowsAsync<NotFoundException>(async () => await store.ResolveAsync(contentDesc.Digest, cancellationToken));

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
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSHA256(manifest),
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
            if (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}" || req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{reference}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                res.Content = new ByteArrayContent(manifest);
                res.Headers.Add(_dockerContentDigestHeader, new string[] { manifestDesc.Digest });
                res.Content.Headers.Add("Content-Type", new string[] { MediaType.ImageManifest });
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);

        // test with tag
        var data = await store.FetchAsync(reference, cancellationToken);
        Assert.True(AreDescriptorsEqual(manifestDesc, data.Descriptor));
        var buf = new byte[manifest.Length];
        await data.Stream.ReadAsync(buf, cancellationToken);
        Assert.Equal(manifest, buf);

        // test with other tag
        var randomRef = "whatever";
        await Assert.ThrowsAsync<NotFoundException>(async () => await store.FetchAsync(randomRef, cancellationToken));

        // test with digest
        data = await store.FetchAsync(manifestDesc.Digest, cancellationToken);
        Assert.True(AreDescriptorsEqual(manifestDesc, data.Descriptor));

        buf = new byte[manifest.Length];
        await data.Stream.ReadAsync(buf, cancellationToken);
        Assert.Equal(manifest, buf);

        // test with tag@digest
        var tagDigestRef = randomRef + "@" + manifestDesc.Digest;
        data = await store.FetchAsync(tagDigestRef, cancellationToken);
        Assert.True(AreDescriptorsEqual(manifestDesc, data.Descriptor));
        buf = new byte[manifest.Length];
        await data.Stream.ReadAsync(buf, cancellationToken);
        Assert.Equal(manifest, buf);

        // test with FQDN
        var fqdnRef = "localhost:5000/test" + ":" + tagDigestRef;
        data = await store.FetchAsync(fqdnRef, cancellationToken);
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
            Digest = ComputeSHA256(blob),
            Size = blob.Length
        };
        var index = """{"manifests":[]}"""u8.ToArray();
        var indexDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSHA256(index),
            Size = index.Length
        };
        var gotIndex = new byte[index.Length];
        var reference = "foobar";

        var func = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{blobDesc.Digest}")
            {
                res.StatusCode = HttpStatusCode.NotFound;
                return res;
            }
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{indexDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(indexDesc.MediaType))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                res.Content = new ByteArrayContent(index);
                res.Headers.Add(_dockerContentDigestHeader, new string[] { indexDesc.Digest });
                res.Content.Headers.Add("Content-Type", new string[] { indexDesc.MediaType });
                return res;
            }
            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{reference}" || req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{indexDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Content-Type", out IEnumerable<string>? values) && !values.Contains(indexDesc.MediaType))
                {
                    res.StatusCode = HttpStatusCode.BadRequest;
                    return res;
                }
                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync()).CopyTo(buf, 0);
                    gotIndex = buf;
                }

                res.Headers.Add(_dockerContentDigestHeader, new string[] { indexDesc.Digest });
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }

            res.StatusCode = HttpStatusCode.Forbidden;
            return res;
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);

        await Assert.ThrowsAnyAsync<Exception>(async () => await store.TagAsync(blobDesc, reference, cancellationToken));

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
        var index = RandomIndex();
        var indexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(index));
        var indexDesc = new Descriptor
        {
            MediaType = MediaType.ImageIndex,
            Digest = ComputeSHA256(indexBytes),
            Size = indexBytes.Length
        };
        var gotIndex = new byte[indexBytes.Length];
        var reference = "foobar";

        var func = async (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;

            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{reference}")
            {
                if (req.Headers.TryGetValues("Content-Type", out IEnumerable<string>? values) && !values.Contains(indexDesc.MediaType))
                {
                    res.StatusCode = HttpStatusCode.BadRequest;
                    return res;
                }

                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync()).CopyTo(buf, 0);
                    gotIndex = buf;
                }

                res.Headers.Add(_dockerContentDigestHeader, new string[] { indexDesc.Digest });
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }
            res.StatusCode = HttpStatusCode.Forbidden;
            return res;
        };
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        var store = new ManifestStore(repo);
        await store.PushAsync(indexDesc, new MemoryStream(indexBytes), reference, cancellationToken);
        Assert.Equal(indexBytes, gotIndex);
    }

    /// <summary>
    /// This test tries copying artifacts from the remote target to the memory target
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CopyFromRepositoryToMemory()
    {
        var exampleManifest = @"hello world"u8.ToArray();

        var exampleManifestDescriptor = new Descriptor
        {
            MediaType = MediaType.Descriptor,
            Digest = ComputeSHA256(exampleManifest),
            Size = exampleManifest.Length
        };
        var exampleUploadUUid = new Guid().ToString();
        var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            var path = req.RequestUri != null ? req.RequestUri.AbsolutePath : string.Empty;
            var method = req.Method;
            if (path.Contains("/blobs/uploads/") && method == HttpMethod.Post)
            {
                res.StatusCode = HttpStatusCode.Accepted;
                res.Headers.Location = new Uri($"{path}/{exampleUploadUUid}");
                res.Headers.Add("Content-Type", MediaType.ImageManifest);
                return res;
            }
            if (path.Contains("/blobs/uploads/" + exampleUploadUUid) && method == HttpMethod.Get)
            {
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }

            if (path.Contains("/manifests/latest") && method == HttpMethod.Put)
            {
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }

            if (path.Contains("/manifests/" + exampleManifestDescriptor.Digest) || path.Contains("/manifests/latest") && method == HttpMethod.Head)
            {
                if (method == HttpMethod.Get)
                {
                    res.Content = new ByteArrayContent(exampleManifest);
                    res.Content.Headers.Add("Content-Type", MediaType.Descriptor);
                    res.Headers.Add(_dockerContentDigestHeader, exampleManifestDescriptor.Digest);
                    res.Content.Headers.Add("Content-Length", exampleManifest.Length.ToString());
                    return res;
                }
                res.Content.Headers.Add("Content-Type", MediaType.Descriptor);
                res.Headers.Add(_dockerContentDigestHeader, exampleManifestDescriptor.Digest);
                res.Content.Headers.Add("Content-Length", exampleManifest.Length.ToString());
                return res;
            }


            if (path.Contains("/blobs/") && (method == HttpMethod.Get || method == HttpMethod.Head))
            {
                var arr = path.Split("/");
                var digest = arr[arr.Length - 1];


                if (digest == exampleManifestDescriptor.Digest)
                {
                    byte[] content = exampleManifest;
                    res.Content = new ByteArrayContent(content);
                    res.Content.Headers.Add("Content-Type", exampleManifestDescriptor.MediaType);
                    res.Content.Headers.Add("Content-Length", content.Length.ToString());
                }

                res.Headers.Add(_dockerContentDigestHeader, digest);

                return res;
            }

            if (path.Contains("/manifests/") && method == HttpMethod.Put)
            {
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }

            return res;
        };

        var reg = new Registry.Remote.Registry(new RepositoryOptions()
        {
            Reference = new Reference("localhost:5000"),
            HttpClient = CustomClient(func),
        });
        var src = await reg.GetRepositoryAsync("source", CancellationToken.None);

        var dst = new MemoryStore();
        var tagName = "latest";
        var desc = await src.CopyAsync(tagName, dst, tagName, CancellationToken.None);
    }

    [Fact]
    public async Task ManifestStore_generateDescriptorWithVariousDockerContentDigestHeaders()
    {
        var reference = new Reference("eastern.haan.com", "from25to220ce");
        var tests = GetTestIOStructMapForGetDescriptorClass();
        foreach ((string testName, TestIOStruct dcdIOStruct) in tests)
        {
            var repo = new Repository(reference.Repository + "/" + reference.Repository);
            HttpMethod[] methods = new HttpMethod[] { HttpMethod.Get, HttpMethod.Head };
            var s = new ManifestStore(repo);
            foreach ((int i, HttpMethod method) in methods.Select((value, i) => (i, value)))
            {
                reference.ContentReference = dcdIOStruct.ClientSuppliedReference;
                var resp = new HttpResponseMessage();
                if (method == HttpMethod.Get)
                {
                    resp.Content = new ByteArrayContent(_theAmazingBanClan);
                    resp.Content.Headers.Add("Content-Type", new string[] { "application/vnd.docker.distribution.manifest.v2+json" });
                    resp.Headers.Add(_dockerContentDigestHeader, new string[] { dcdIOStruct.ServerCalculatedDigest });
                }
                else
                {
                    resp.Content.Headers.Add("Content-Type", new string[] { "application/vnd.docker.distribution.manifest.v2+json" });
                    resp.Headers.Add(_dockerContentDigestHeader, new string[] { dcdIOStruct.ServerCalculatedDigest });
                }
                resp.RequestMessage = new HttpRequestMessage()
                {
                    Method = method
                };

                var errExpected = new bool[] { dcdIOStruct.ErrExpectedOnGET, dcdIOStruct.ErrExpectedOnHEAD }[i];

                var err = false;
                try
                {
                    await resp.GenerateDescriptorAsync(reference, CancellationToken.None);
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
    
        /// <summary>
    /// Repository_MountAsync tests the MountAsync method of the Repository
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Repository_MountAsync()
    {
        var blob = @"hello world"u8.ToArray();
        var blobDesc = new Descriptor()
        {
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        var gotMount = 0;
        var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var resp = new HttpResponseMessage();
            resp.RequestMessage = req;
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == "/v2/test2/blobs/uploads/")
            {
                var queries = HttpUtility.ParseQueryString(req.RequestUri.Query);
                if (queries["mount"] != blobDesc.Digest)
                {
                    resp.StatusCode = HttpStatusCode.InternalServerError;
                    return resp;
                }
                if (queries["from"] != "test")
                {
                    resp.StatusCode = HttpStatusCode.InternalServerError;
                    return resp;
                }
                gotMount++;
                resp.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                resp.StatusCode = HttpStatusCode.Created;
                return resp;
            }
            resp.StatusCode = HttpStatusCode.InternalServerError;
            return resp;
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test2"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();

        await repo.MountAsync(blobDesc, "test", null, cancellationToken);
        Assert.Equal(1, gotMount);
    }

    /// <summary>
    /// Repository_MountAsync_Fallback tests the MountAsync method of the Repository when the server doesn't support mount query parameters.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Repository_MountAsync_Fallback()
    {
        var blob = @"hello world"u8.ToArray();
        var blobDesc = new Descriptor()
        {
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        string sequence = "";
        byte[] gotBlob = Array.Empty<byte>();
        var uuid = "4fd53bc9-565d-4527-ab80-3e051ac4880c";
        var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var resp = new HttpResponseMessage();
            resp.RequestMessage = req;
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == "/v2/test2/blobs/uploads/")
            {
                resp.Headers.Location = new Uri("/v2/test2/blobs/uploads/" + uuid, UriKind.Relative);
                resp.StatusCode = HttpStatusCode.Accepted;
                sequence += "post ";
                return resp;
            }
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath == "/v2/test/blobs/" + blobDesc.Digest)
            {
                resp.Content.Headers.Add("Content-Type", "application/octet-stream");
                resp.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                resp.Content = new ByteArrayContent(blob);
                resp.StatusCode = HttpStatusCode.OK;
                sequence += "get ";
                return resp;
            }
            if (req.Method == HttpMethod.Put && req.RequestUri!.AbsolutePath == "/v2/test2/blobs/uploads/" + uuid)
            {
                if (req.Content?.Headers.GetValues("Content-Type").FirstOrDefault() != "application/octet-stream")
                {
                    resp.StatusCode = HttpStatusCode.BadRequest;
                    return resp;
                }
                if (HttpUtility.ParseQueryString(req.RequestUri.Query)["digest"] != blobDesc.Digest)
                {
                    resp.StatusCode = HttpStatusCode.BadRequest;
                    return resp;
                }
                gotBlob = req.Content!.ReadAsByteArrayAsync(cancellationToken).Result;
                resp.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                resp.StatusCode = HttpStatusCode.Created;
                sequence += "put ";
                return resp;
            }
            resp.StatusCode = HttpStatusCode.Forbidden;
            return resp;
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test2"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();

        // getContent is null
        sequence = "";
        await repo.MountAsync(blobDesc, "localhost:5000/test", null, cancellationToken);
        Assert.Equal(blob, gotBlob);
        Assert.Equal("post get put ", sequence);

        // getContent is non-null
        sequence = "";
        await repo.MountAsync(blobDesc, "localhost:5000/test", _ => Task.FromResult<Stream>(new MemoryStream(blob)), cancellationToken);
        Assert.Equal(blob, gotBlob);
        Assert.Equal("post put ", sequence);
    }

    /// <summary>
    /// Repository_MountAsync_Error tests the error handling of the MountAsync method of the Repository.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Repository_MountAsync_Error()
    {
        var blob = @"hello world"u8.ToArray();
        var blobDesc = new Descriptor()
        {
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var resp = new HttpResponseMessage();
            resp.RequestMessage = req;
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == "/v2/test/blobs/uploads/")
            {
                resp.StatusCode = HttpStatusCode.BadRequest;
                resp.Content = new StringContent(@"{ ""errors"": [ { ""code"": ""NAME_UNKNOWN"", ""message"": ""some error"" } ] }");
                return resp;
            }
            resp.StatusCode = HttpStatusCode.InternalServerError;
            return resp;
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();

        var ex = await Assert.ThrowsAsync<ResponseException>(async () =>
        {
            await repo.MountAsync(blobDesc, "foo", null, cancellationToken);
        });

        Assert.NotNull(ex.Errors);
        Assert.Single(ex.Errors);
        Assert.Equal("NAME_UNKNOWN", ex.Errors[0].Code);
        Assert.Equal("some error", ex.Errors[0].Message);
    }

    /// <summary>
    /// Repository_MountAsync_Fallback_GetContent tests the case where the server doesn't recognize mount query parameters,
    /// falling back to the regular push flow, using the getContent function parameter.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Repository_MountAsync_Fallback_GetContent()
    {
        var blob = @"hello world"u8.ToArray();
        var blobDesc = new Descriptor()
        {
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        string sequence = "";
        byte[] gotBlob = Array.Empty<byte>();
        var uuid = "4fd53bc9-565d-4527-ab80-3e051ac4880c";
        var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var resp = new HttpResponseMessage();
            resp.RequestMessage = req;
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == "/v2/test2/blobs/uploads/")
            {
                resp.Headers.Location = new Uri("/v2/test2/blobs/uploads/" + uuid, UriKind.Relative);
                resp.StatusCode = HttpStatusCode.Accepted;
                sequence += "post ";
                return resp;
            }
            if (req.Method == HttpMethod.Put && req.RequestUri!.AbsolutePath == "/v2/test2/blobs/uploads/" + uuid)
            {
                if (req.Content?.Headers.GetValues("Content-Type").FirstOrDefault() != "application/octet-stream")
                {
                    resp.StatusCode = HttpStatusCode.BadRequest;
                    return resp;
                }
                if (HttpUtility.ParseQueryString(req.RequestUri.Query)["digest"] != blobDesc.Digest)
                {
                    resp.StatusCode = HttpStatusCode.BadRequest;
                    return resp;
                }
                gotBlob = req.Content!.ReadAsByteArrayAsync(cancellationToken).Result;
                resp.Headers.Add(_dockerContentDigestHeader, blobDesc.Digest);
                resp.StatusCode = HttpStatusCode.Created;
                sequence += "put ";
                return resp;
            }
            resp.StatusCode = HttpStatusCode.Forbidden;
            return resp;
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test2"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();

        await repo.MountAsync(blobDesc, "test", _ => Task.FromResult<Stream>(new MemoryStream(blob)), cancellationToken);

        // Assert that the blob was pushed correctly
        Assert.Equal(blob, gotBlob);
        // Assert that the request sequence matches the expected behavior
        Assert.Equal("post put ", sequence);
    }

    /// <summary>
    /// Repository_MountAsync_Fallback_GetContentError tests the case where the server doesn't recognize mount query parameters,
    /// falling back to the regular push flow, but the caller wants to avoid the pull/push pattern, so an error is returned from getContent.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Repository_MountAsync_Fallback_GetContentError()
    {
        var blob = @"hello world"u8.ToArray();
        var blobDesc = new Descriptor()
        {
            Digest = ComputeSHA256(blob),
            MediaType = "test",
            Size = (uint)blob.Length
        };
        string sequence = "";
        var uuid = "4fd53bc9-565d-4527-ab80-3e051ac4880c";
        var func = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var resp = new HttpResponseMessage();
            resp.RequestMessage = req;
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == "/v2/test2/blobs/uploads/")
            {
                resp.Headers.Location = new Uri("/v2/test2/blobs/uploads/" + uuid, UriKind.Relative);
                resp.StatusCode = HttpStatusCode.Accepted;
                sequence += "post ";
                return resp;
            }
            resp.StatusCode = HttpStatusCode.Forbidden;
            return resp;
        };

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test2"),
            HttpClient = CustomClient(func),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();

        var testErr = new Exception("test error");
        var ex = await Assert.ThrowsAsync<Exception>(async () =>
        {
            await repo.MountAsync(blobDesc, "test", _ => throw testErr, cancellationToken);
        });

        Assert.Equal(testErr, ex);
        Assert.Equal("post ", sequence);
    }
    
    [Fact]
    public void SetReferrersState_ShouldSet_WhenInitiallyUnknown()
    {
        var repo = new Repository("localhost:5000/test2");
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        repo.ReferrersState = Referrers.ReferrersState.Supported;
        Assert.Equal(Referrers.ReferrersState.Supported, repo.ReferrersState);
    }
    
    [Fact]
    public void SetReferrersState_ShouldThrowException_WhenChangingAfterSet()
    {
        var repo = new Repository("localhost:5000/test2");
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        repo.ReferrersState = Referrers.ReferrersState.Supported;
        Assert.Equal(Referrers.ReferrersState.Supported, repo.ReferrersState);
        
        var exception = Assert.Throws<ReferrersStateAlreadySetException>(() =>
            repo.ReferrersState = Referrers.ReferrersState.NotSupported
        );

        Assert.Equal("current referrers state: Supported, latest referrers state: NotSupported", exception.Message);
    }
    
    [Fact]
    public void SetReferrersState_ShouldNotThrowException_WhenSettingSameValue()
    {
        var repo = new Repository("localhost:5000/test2");
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        repo.ReferrersState = Referrers.ReferrersState.Supported;
        Assert.Equal(Referrers.ReferrersState.Supported, repo.ReferrersState);
        
        var exception = Record.Exception(() => repo.ReferrersState = Referrers.ReferrersState.Supported);
        Assert.Null(exception);
    }

    [Fact]
    public void PingReferrers_ShouldReturnTrueWhenReferrersAPISupported()
    {
        var mockHttpRequestHandler = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/referrers/{Referrers.ZeroDigest}")
            {
                res.Content.Headers.Add("Content-Type", MediaType.ImageIndex);
                res.StatusCode = HttpStatusCode.OK;
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        };
        
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockHttpRequestHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        Assert.True(repo.PingReferrers(cancellationToken));
        Assert.Equal(Referrers.ReferrersState.Supported, repo.ReferrersState);
    }
    
    [Fact]
    public void PingReferrers_ShouldReturnFalseWhenReferrersAPINotSupportedNoContentTypeHeader()
    {
        var mockHttpRequestHandler = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/referrers/{Referrers.ZeroDigest}")
            {
                res.StatusCode = HttpStatusCode.OK;
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        };
        
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockHttpRequestHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        Assert.False(repo.PingReferrers(cancellationToken));
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo.ReferrersState);
    }
    
    [Fact]
    public void PingReferrers_ShouldFailWhenReturnNotFound()
    {
        var mockHttpRequestHandler = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            res.StatusCode = HttpStatusCode.NotFound;

            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/v2/test/referrers/{Referrers.ZeroDigest}")
            {
                return res;
            }

            var errors = new
            {
                errors = new[]
                {
                    new
                    {
                        message = "The repository could not be found.",
                        code = ResponseException.ErrorCodeNameUnknown 
                    }
                }
            };
            res.Content = new StringContent(JsonSerializer.Serialize(errors), Encoding.UTF8, "application/json");
            return res;
        };
        var cancellationToken = new CancellationToken();

        // repo abc is not found
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/abc"),
            HttpClient = CustomClient(mockHttpRequestHandler),
            PlainHttp = true,
        });
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        Assert.Throws<ResponseException>(() => repo.PingReferrers(cancellationToken));
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        
        // referrer API is not supported
        var repo1 = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockHttpRequestHandler),
            PlainHttp = true,
        });
        Assert.Equal(Referrers.ReferrersState.Unknown, repo1.ReferrersState);
        Assert.False(repo1.PingReferrers(cancellationToken));
        Assert.Equal(Referrers.ReferrersState.NotSupported, repo1.ReferrersState);
    }
    
    [Fact]
    public void PingReferrers_ShouldFailWhenBadRequestReturns()
    {
        var mockHttpRequestHandler = (HttpRequestMessage req, CancellationToken cancellationToken) =>
        {
            var res = new HttpResponseMessage();
            res.RequestMessage = req;
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        };
        
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            HttpClient = CustomClient(mockHttpRequestHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
        Assert.Throws<ResponseException>(() => repo.PingReferrers(cancellationToken));
        Assert.Equal(Referrers.ReferrersState.Unknown, repo.ReferrersState);
    }
}
