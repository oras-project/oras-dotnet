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

using OrasProject.Oras.Content.Exceptions;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Exceptions;
using System.Net;
using System.Web;
using Xunit;
using static OrasProject.Oras.Content.Digest;
using static OrasProject.Oras.Tests.Remote.Util.Util;

namespace OrasProject.Oras.Tests.Registry.Remote;

public class BlobStoreChunkedUploadTest
{
    [Fact]
    public void RepositoryOptionsUseExpectedBlobUploadDefaults()
    {
        var options = new RepositoryOptions
        {
            Client = CustomClient((request, _) => Response(request, HttpStatusCode.NotFound)),
            Reference = Reference.Parse("localhost:5000/test"),
        };

        Assert.Equal(BlobUploadMode.Monolithic, options.BlobUploadMode);
        Assert.Equal(5 * 1024 * 1024, options.BlobUploadChunkSize);
    }

    [Fact]
    public void RepositoryOptionsRejectNonPositiveChunkSize()
    {
        var options = new RepositoryOptions
        {
            Client = CustomClient((request, _) => Response(request, HttpStatusCode.NotFound)),
            Reference = Reference.Parse("localhost:5000/test"),
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.BlobUploadChunkSize = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.BlobUploadChunkSize = -1);
    }

    [Fact]
    public async Task PushAsyncUploadsBlobInChunksAndFinalizesAfterCreatedPatch()
    {
        var blob = Enumerable.Range(0, 11).Select(value => (byte)value).ToArray();
        var descriptor = DescriptorFor(blob);
        var ranges = new List<string>();
        var uploaded = new List<byte>();
        var patchCount = 0;

        var store = CreateBlobStore(Handler, BlobUploadMode.Chunked, chunkSize: 4);
        await store.PushAsync(descriptor, new MemoryStream(blob));

        Assert.Equal(["0-3", "4-7", "8-10"], ranges);
        Assert.Equal(blob, uploaded);
        return;

        async Task<HttpResponseMessage> Handler(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal(0, request.Content?.Headers.ContentLength);
                Assert.Equal("application/octet-stream", request.Content?.Headers.ContentType?.MediaType);
                return Response(request, HttpStatusCode.Accepted, "/v2/test/blobs/uploads/session?state=0");
            }

            if (request.Method == HttpMethod.Patch)
            {
                patchCount++;
                var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
                Assert.Equal((patchCount - 1).ToString(), query["state"]);
                ranges.Add(Assert.Single(request.Content!.Headers.GetValues("Content-Range")));
                uploaded.AddRange(await request.Content.ReadAsByteArrayAsync(cancellationToken));
                var statusCode = patchCount == 3 ? HttpStatusCode.Created : HttpStatusCode.Accepted;
                return Response(request, statusCode, $"/v2/test/blobs/uploads/session?state={patchCount}");
            }

            if (request.Method == HttpMethod.Put)
            {
                var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
                Assert.Equal(patchCount.ToString(), query["state"]);
                Assert.Equal(descriptor.Digest, query["digest"]);
                Assert.Equal(0, request.Content?.Headers.ContentLength);
                return Response(request, HttpStatusCode.Created);
            }

            return Response(request, HttpStatusCode.MethodNotAllowed);
        }
    }

    [Fact]
    public async Task PushAsyncHonorsRegistryMinimumChunkSize()
    {
        var blob = Enumerable.Range(0, 11).Select(value => (byte)value).ToArray();
        var descriptor = DescriptorFor(blob);
        var ranges = new List<string>();

        var store = CreateBlobStore(Handler, BlobUploadMode.Chunked, chunkSize: 4);
        await store.PushAsync(descriptor, new MemoryStream(blob));

        Assert.Equal(["0-5", "6-10"], ranges);
        return;

        HttpResponseMessage Handler(HttpRequestMessage request, CancellationToken _)
        {
            if (request.Method == HttpMethod.Post)
            {
                var response = Response(request, HttpStatusCode.Accepted, "/v2/test/blobs/uploads/session");
                response.Headers.Add("OCI-Chunk-Min-Length", "6");
                return response;
            }
            if (request.Method == HttpMethod.Patch)
            {
                ranges.Add(Assert.Single(request.Content!.Headers.GetValues("Content-Range")));
                return Response(request, HttpStatusCode.Accepted, "/v2/test/blobs/uploads/session");
            }
            if (request.Method == HttpMethod.Put)
            {
                return Response(request, HttpStatusCode.Created);
            }
            return Response(request, HttpStatusCode.MethodNotAllowed);
        }
    }

    [Fact]
    public async Task PushAsyncFallsBackToChunkedUploadWithNewSession()
    {
        var blob = Enumerable.Range(0, 9).Select(value => (byte)value).ToArray();
        var descriptor = DescriptorFor(blob);
        var sequence = new List<string>();
        var postCount = 0;
        var uploaded = new List<byte>();

        var store = CreateBlobStore(Handler, BlobUploadMode.MonolithicWithChunkedFallback, chunkSize: 4);
        await store.PushAsync(descriptor, new MemoryStream(blob));

        Assert.Equal(["post-1", "put-monolithic", "post-2", "patch", "patch", "patch", "put-finalize"], sequence);
        Assert.Equal(blob, uploaded);
        return;

        async Task<HttpResponseMessage> Handler(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                postCount++;
                sequence.Add($"post-{postCount}");
                Assert.Equal(postCount == 1 ? null : 0, request.Content?.Headers.ContentLength);
                return Response(request, HttpStatusCode.Accepted, $"/v2/test/blobs/uploads/session-{postCount}");
            }
            if (request.Method == HttpMethod.Put &&
                request.RequestUri!.AbsolutePath.EndsWith("session-1", StringComparison.Ordinal))
            {
                sequence.Add("put-monolithic");
                Assert.Equal(blob, await request.Content!.ReadAsByteArrayAsync(cancellationToken));
                return Response(request, HttpStatusCode.BadRequest, content: "monolithic upload rejected");
            }
            if (request.Method == HttpMethod.Patch)
            {
                sequence.Add("patch");
                uploaded.AddRange(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
                return Response(request, HttpStatusCode.Accepted, "/v2/test/blobs/uploads/session-2");
            }
            if (request.Method == HttpMethod.Put)
            {
                sequence.Add("put-finalize");
                return Response(request, HttpStatusCode.Created);
            }
            return Response(request, HttpStatusCode.MethodNotAllowed);
        }
    }

    [Fact]
    public async Task PushAsyncDoesNotRetryNonSeekableStream()
    {
        var blob = Enumerable.Range(0, 9).Select(value => (byte)value).ToArray();
        var descriptor = DescriptorFor(blob);
        var requestCount = 0;

        var store = CreateBlobStore(Handler, BlobUploadMode.MonolithicWithChunkedFallback, chunkSize: 4);
        await Assert.ThrowsAsync<ResponseException>(() =>
            store.PushAsync(descriptor, new NonSeekableStream(blob)));
        Assert.Equal(2, requestCount);
        return;

        HttpResponseMessage Handler(HttpRequestMessage request, CancellationToken _)
        {
            requestCount++;
            return request.Method == HttpMethod.Post
                ? Response(request, HttpStatusCode.Accepted, "/v2/test/blobs/uploads/session")
                : Response(request, HttpStatusCode.BadRequest, content: "monolithic upload rejected");
        }
    }

    [Fact]
    public async Task MountFallbackUsesChunkedUpload()
    {
        var blob = Enumerable.Range(0, 7).Select(value => (byte)value).ToArray();
        var descriptor = DescriptorFor(blob);
        var methods = new List<HttpMethod>();

        var repository = CreateRepository(Handler, BlobUploadMode.Chunked, chunkSize: 4);
        await repository.MountAsync(descriptor, "source", _ => Task.FromResult<Stream>(new MemoryStream(blob)));

        Assert.Equal([HttpMethod.Post, HttpMethod.Patch, HttpMethod.Patch, HttpMethod.Put], methods);
        return;

        HttpResponseMessage Handler(HttpRequestMessage request, CancellationToken _)
        {
            methods.Add(request.Method);
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal(descriptor.Digest, HttpUtility.ParseQueryString(request.RequestUri!.Query)["mount"]);
                return Response(request, HttpStatusCode.Accepted, "/v2/test/blobs/uploads/session");
            }
            if (request.Method == HttpMethod.Patch)
            {
                return Response(request, HttpStatusCode.Accepted, "/v2/test/blobs/uploads/session");
            }
            if (request.Method == HttpMethod.Put)
            {
                return Response(request, HttpStatusCode.Created);
            }
            return Response(request, HttpStatusCode.MethodNotAllowed);
        }
    }

    [Fact]
    public async Task PushAsyncRejectsContentLargerThanDescriptor()
    {
        var blob = Enumerable.Range(0, 5).Select(value => (byte)value).ToArray();
        var descriptor = DescriptorFor(blob[..4]);

        var store = CreateBlobStore(Handler, BlobUploadMode.Chunked, chunkSize: 4);
        await Assert.ThrowsAsync<MismatchedSizeException>(() => store.PushAsync(descriptor, new MemoryStream(blob)));
        return;

        static HttpResponseMessage Handler(HttpRequestMessage request, CancellationToken _) =>
            request.Method switch
            {
                { } method when method == HttpMethod.Post =>
                    Response(request, HttpStatusCode.Accepted, "/v2/test/blobs/uploads/session"),
                { } method when method == HttpMethod.Patch =>
                    Response(request, HttpStatusCode.Accepted, "/v2/test/blobs/uploads/session"),
                _ => Response(request, HttpStatusCode.MethodNotAllowed),
            };
    }

    private static BlobStore CreateBlobStore(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler,
        BlobUploadMode mode,
        int chunkSize) => new(CreateRepository(handler, mode, chunkSize));

    private static BlobStore CreateBlobStore(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        BlobUploadMode mode,
        int chunkSize) => new(CreateRepository(handler, mode, chunkSize));

    private static Repository CreateRepository(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler,
        BlobUploadMode mode,
        int chunkSize) => new(new RepositoryOptions
        {
            Client = CustomClient(handler),
            Reference = Reference.Parse("localhost:5000/test"),
            PlainHttp = true,
            BlobUploadMode = mode,
            BlobUploadChunkSize = chunkSize,
        });

    private static Repository CreateRepository(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        BlobUploadMode mode,
        int chunkSize) => new(new RepositoryOptions
        {
            Client = CustomClient(handler),
            Reference = Reference.Parse("localhost:5000/test"),
            PlainHttp = true,
            BlobUploadMode = mode,
            BlobUploadChunkSize = chunkSize,
        });

    private static Descriptor DescriptorFor(byte[] content) => new()
    {
        Digest = ComputeSha256(content),
        MediaType = "application/octet-stream",
        Size = content.Length,
    };

    private static HttpResponseMessage Response(
        HttpRequestMessage request,
        HttpStatusCode statusCode,
        string? location = null,
        string? content = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = content is null ? new ByteArrayContent([]) : new StringContent(content),
        };
        if (location is not null)
        {
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        }
        return response;
    }
}
