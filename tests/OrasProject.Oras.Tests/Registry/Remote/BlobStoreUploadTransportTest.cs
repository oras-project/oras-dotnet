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

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;
using Xunit;
using static OrasProject.Oras.Content.Digest;

namespace OrasProject.Oras.Tests.Registry.Remote;

public class BlobStoreUploadTransportTest
{
    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    public async Task RedirectedPatchResolvesRelativeLocationAtUploadOrigin(int size)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var upload = new UploadServer();
        await using var registry = new UploadServer();
        var blob = Enumerable.Range(0, size).Select(i => (byte)i).ToArray();
        var descriptor = Describe(blob);
        using var received = new MemoryStream();
        var registryTask = registry.ServeAsync(2, async request =>
        {
            if (request.Method == "POST")
            {
                await request.ReplyAsync(202, "/session");
            }
            else
            {
                Assert.Equal("PATCH", request.Method);
                await request.ReadBodyAsync();
                await request.ReplyAsync(307, upload.Url + "redirected");
            }
        }, timeout.Token);
        var uploadTask = upload.ServeAsync(size / 4 + 1, async request =>
        {
            if (request.Method == "PATCH")
            {
                Assert.Equal(received.Length == 0 ? "/redirected" : "/sessions/u?state=1", request.Target);
                received.Write(await request.ReadBodyAsync());
                await request.ReplyAsync(202, "/sessions/u?state=1");
            }
            else
            {
                Assert.Equal("PUT", request.Method);
                Assert.StartsWith("/sessions/u?", request.Target);
                var query = HttpUtility.ParseQueryString(new Uri(upload.Url, request.Target).Query);
                Assert.Equal("1", query["state"]);
                Assert.Equal(descriptor.Digest, query["digest"]);
                Assert.Empty(await request.ReadBodyAsync());
                await request.ReplyAsync(201);
            }
        }, timeout.Token);

        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        await Store(registry.Url, client, BlobUploadMode.Chunked, 4)
            .PushAsync(descriptor, new MemoryStream(blob), timeout.Token);
        await Task.WhenAll(registryTask, uploadTask);
        Assert.Equal(blob, received.ToArray());
    }

    [Fact]
    public async Task EarlyMonolithicRejectionFallsBackFromOriginalStreamPosition()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var registry = new UploadServer();
        var blob = new byte[16 * 1024 * 1024];
        new Random(42).NextBytes(blob);
        var descriptor = Describe(blob);
        using var received = new MemoryStream();
        var sequence = new List<string>();
        var sessions = 0;
        var serving = registry.ServeAsync(8, async request =>
        {
            sequence.Add(request.Method);
            if (request.Method == "POST")
            {
                await request.ReplyAsync(202, $"/session-{++sessions}");
            }
            else if (request.Target.StartsWith("/session-1", StringComparison.Ordinal))
            {
                // Reject without draining the large request body, as a proxy may do.
                await request.ReplyAsync(413);
                request.ResetConnection();
            }
            else if (request.Method == "PATCH")
            {
                Assert.Equal("/session-2", request.Target);
                received.Write(await request.ReadBodyAsync());
                await request.ReplyAsync(202, "/session-2");
            }
            else
            {
                Assert.Equal("PUT", request.Method);
                await request.ReplyAsync(201);
            }
        }, timeout.Token);
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        using var source = new MemoryStream();
        source.Write(new byte[17]);
        source.Write(blob);
        source.Position = 17;
        await Store(registry.Url, client, BlobUploadMode.MonolithicWithChunkedFallback, 4 * 1024 * 1024)
            .PushAsync(descriptor, source, timeout.Token);
        await serving;
        Assert.Equal(["POST", "PUT", "POST", "PATCH", "PATCH", "PATCH", "PATCH", "PUT"], sequence);
        Assert.Equal(blob, received.ToArray());
        Assert.True(source.CanRead);
    }

    [Theory]
    [InlineData(BlobUploadMode.Monolithic)]
    [InlineData(BlobUploadMode.MonolithicWithChunkedFallback)]
    public async Task AuthenticationReplayPreservesInitialStreamPosition(BlobUploadMode mode)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var registry = new UploadServer();
        // Above Auth.Client's buffering limit so the retry must rewind the source stream.
        var blob = new byte[5 * 1024 * 1024];
        new Random(42).NextBytes(blob);
        var descriptor = Describe(blob);
        var puts = 0;
        var serving = registry.ServeAsync(3, async request =>
        {
            if (request.Method == "POST")
            {
                await request.ReplyAsync(202, "/session");
                return;
            }
            Assert.Equal("PUT", request.Method);
            Assert.Equal(blob, await request.ReadBodyAsync());
            puts++;
            if (puts == 1)
            {
                Assert.Null(request.Header("Authorization"));
                await request.ReplyAsync(401, headers: "WWW-Authenticate: Basic realm=registry\r\n");
            }
            else
            {
                Assert.Equal("Basic dXNlcjpwYXNz", request.Header("Authorization"));
                await request.ReplyAsync(201);
            }
        }, timeout.Token);
        using var httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        var authClient = new Client(httpClient,
            new SingleRegistryCredentialProvider(registry.Url.Authority, new Credential("user", "pass")));
        var store = new BlobStore(new Repository(new RepositoryOptions
        {
            Client = authClient,
            Reference = Reference.Parse(registry.Url.Authority + "/test"),
            PlainHttp = true,
            BlobUploadMode = mode,
        }));
        using var source = new MemoryStream();
        source.Write(new byte[17]);
        source.Write(blob);
        source.Position = 17;
        await store.PushAsync(descriptor, source, timeout.Token);
        await serving;
        Assert.Equal(2, puts);
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task ChunkedAuthenticationReplayPreservesBodyRangeAndOpaqueQuery()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var registry = new UploadServer();
        const int chunkSize = 5 * 1024 * 1024;
        const string location = "/session?state=a%2Fb%2Bc%3D&empty=&state=second";
        var blob = new byte[chunkSize + 13];
        new Random(42).NextBytes(blob);
        var descriptor = Describe(blob);
        using var received = new MemoryStream();
        var challenged = false;
        var serving = registry.ServeAsync(5, async request =>
        {
            if (request.Method == "POST")
            {
                await request.ReplyAsync(202, location);
                return;
            }
            if (request.Method == "PATCH")
            {
                Assert.Equal(location, request.Target);
                var body = await request.ReadBodyAsync();
                if (!challenged)
                {
                    Assert.Equal(blob[..chunkSize], body);
                    challenged = true;
                    await request.ReplyAsync(401, headers: "WWW-Authenticate: Basic realm=registry\r\n");
                    return;
                }
                Assert.Equal("Basic dXNlcjpwYXNz", request.Header("Authorization"));
                Assert.Equal($"{received.Length}-{received.Length + body.Length - 1}", request.Header("Content-Range"));
                received.Write(body);
                await request.ReplyAsync(202, location);
                return;
            }
            Assert.Equal("PUT", request.Method);
            Assert.StartsWith(location + "&digest=", request.Target);
            Assert.Equal(descriptor.Digest, HttpUtility.ParseQueryString(new Uri(registry.Url, request.Target).Query)["digest"]);
            Assert.Empty(await request.ReadBodyAsync());
            await request.ReplyAsync(201);
        }, timeout.Token);
        using var httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        var authClient = new Client(httpClient,
            new SingleRegistryCredentialProvider(registry.Url.Authority, new Credential("user", "pass")));
        var repository = new Repository(new RepositoryOptions
        {
            Client = authClient,
            Reference = Reference.Parse(registry.Url.Authority + "/test"),
            PlainHttp = true,
            BlobUploadMode = BlobUploadMode.Chunked,
            BlobUploadChunkSize = chunkSize,
        });
        using var source = new NonSeekableStream(blob);
        await repository.Blobs.PushAsync(descriptor, source, timeout.Token);
        await serving;
        Assert.Equal(blob, received.ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MountAvoidsOpeningContentOnSuccessAndReusesFallbackStream(bool mounted)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var registry = new UploadServer();
        byte[] blob = [1, 2, 3, 4, 5, 6, 7];
        var descriptor = Describe(blob);
        using var received = new MemoryStream();
        var opened = 0;
        var posts = 0;
        var serving = registry.ServeAsync(mounted ? 1 : 6, async request =>
        {
            if (request.Method == "POST")
            {
                posts++;
                if (posts == 1)
                {
                    var query = HttpUtility.ParseQueryString(new Uri(registry.Url, request.Target).Query);
                    Assert.Equal(descriptor.Digest, query["mount"]);
                    Assert.Equal("base/image", query["from"]);
                    await request.ReplyAsync(mounted ? 201 : 202, "/mounted-session");
                }
                else
                {
                    await request.ReplyAsync(202, "/chunked-session");
                }
                return;
            }
            if (request.Target.StartsWith("/mounted-session", StringComparison.Ordinal))
            {
                Assert.Equal("PUT", request.Method);
                Assert.Equal(blob, await request.ReadBodyAsync());
                await request.ReplyAsync(413);
                return;
            }
            if (request.Method == "PATCH")
            {
                Assert.Equal("/chunked-session", request.Target);
                received.Write(await request.ReadBodyAsync());
                await request.ReplyAsync(202, "/chunked-session");
                return;
            }
            Assert.Equal("PUT", request.Method);
            Assert.Empty(await request.ReadBodyAsync());
            await request.ReplyAsync(201);
        }, timeout.Token);
        using var httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        var repository = new Repository(new RepositoryOptions
        {
            Client = new PlainClient(httpClient),
            Reference = Reference.Parse(registry.Url.Authority + "/test"),
            PlainHttp = true,
            BlobUploadMode = BlobUploadMode.MonolithicWithChunkedFallback,
            BlobUploadChunkSize = 4,
        });
        await repository.MountAsync(descriptor, "base/image", _ =>
        {
            opened++;
            return Task.FromResult<Stream>(new MemoryStream(blob));
        }, timeout.Token);
        await serving;
        Assert.Equal(mounted ? 0 : 1, opened);
        Assert.Equal(mounted ? [] : blob, received.ToArray());
    }

    [Fact]
    public async Task CrossOriginRedirectDoesNotForwardCachedRegistryAuthorization()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var registry = new UploadServer();
        await using var upload = new UploadServer();
        byte[] blob = [1, 2, 3];
        var posts = 0;
        var registryTask = registry.ServeAsync(3, async request =>
        {
            if (request.Method == "POST")
            {
                if (++posts == 1)
                {
                    await request.ReplyAsync(401, headers: "WWW-Authenticate: Basic realm=registry\r\n");
                }
                else
                {
                    Assert.Equal("Basic dXNlcjpwYXNz", request.Header("Authorization"));
                    await request.ReplyAsync(202, "/session");
                }
                return;
            }
            Assert.Equal("PATCH", request.Method);
            Assert.Equal("Basic dXNlcjpwYXNz", request.Header("Authorization"));
            Assert.Equal(blob, await request.ReadBodyAsync());
            await request.ReplyAsync(307, upload.Url + "session");
        }, timeout.Token);
        var uploadTask = upload.ServeAsync(2, async request =>
        {
            Assert.Null(request.Header("Authorization"));
            if (request.Method == "PATCH")
            {
                Assert.Equal(blob, await request.ReadBodyAsync());
                await request.ReplyAsync(202, "/session?state=1");
            }
            else
            {
                Assert.Equal("PUT", request.Method);
                Assert.StartsWith("/session?state=1&digest=", request.Target);
                await request.ReplyAsync(201);
            }
        }, timeout.Token);
        using var httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        var client = new Client(httpClient,
            new SingleRegistryCredentialProvider(registry.Url.Authority, new Credential("user", "pass")));
        var repository = new Repository(new RepositoryOptions
        {
            Client = client,
            Reference = Reference.Parse(registry.Url.Authority + "/test"),
            PlainHttp = true,
            BlobUploadMode = BlobUploadMode.Chunked,
            BlobUploadChunkSize = 4,
        });
        await repository.Blobs.PushAsync(Describe(blob), new MemoryStream(blob), timeout.Token);
        await Task.WhenAll(registryTask, uploadTask);
    }

    private static Descriptor Describe(byte[] blob) => new()
    {
        Digest = ComputeSha256(blob),
        MediaType = "application/octet-stream",
        Size = blob.Length,
    };

    private static BlobStore Store(Uri url, HttpClient client, BlobUploadMode mode, int chunkSize) => new(new Repository(new RepositoryOptions
    {
        Reference = Reference.Parse(url.Authority + "/test"),
        Client = new PlainClient(client),
        PlainHttp = true,
        BlobUploadMode = mode,
        BlobUploadChunkSize = chunkSize,
    }));

    // A small HTTP/1.1 peer lets tests close a connection before reading its body.
    // Each response closes the connection, so no persistent connection parser is needed.
    private sealed class UploadServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        public Uri Url { get; }

        public UploadServer()
        {
            _listener.Start();
            Url = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        }

        public async Task ServeAsync(int count, Func<Request, Task> handler, CancellationToken token)
        {
            for (var i = 0; i < count; i++)
            {
                using var connection = await _listener.AcceptTcpClientAsync(token);
                var stream = connection.GetStream();
                var header = new StringBuilder();
                var one = new byte[1];
                while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                {
                    if (await stream.ReadAsync(one, token) == 0)
                    {
                        throw new EndOfStreamException("Client closed before sending headers");
                    }
                    header.Append((char)one[0]);
                }
                await handler(new Request(connection, header.ToString(), token));
            }
        }

        public ValueTask DisposeAsync()
        {
            _listener.Stop();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Request(TcpClient connection, string headers, CancellationToken token)
    {
        public string Method { get; } = headers.Split(' ')[0];
        public string Target { get; } = headers.Split(' ')[1];

        public string? Header(string name) => headers.Split("\r\n")
            .FirstOrDefault(line => line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))?
            .Split(':', 2)[1].Trim();

        public async Task<byte[]> ReadBodyAsync()
        {
            var length = headers.Split("\r\n")
                .FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            var body = new byte[length is null ? 0 : int.Parse(length.Split(':')[1])];
            await connection.GetStream().ReadExactlyAsync(body, token);
            return body;
        }

        public async Task ReplyAsync(int status, string? location = null, string? headers = null)
        {
            var response = $"HTTP/1.1 {status} Test\r\nContent-Length: 0\r\nConnection: close\r\n";
            if (location is not null)
            {
                response += $"Location: {location}\r\n";
            }
            await connection.GetStream().WriteAsync(Encoding.ASCII.GetBytes(response + headers + "\r\n"), token);
        }

        public void ResetConnection() => connection.Client.LingerState = new LingerOption(true, 0);
    }
}
