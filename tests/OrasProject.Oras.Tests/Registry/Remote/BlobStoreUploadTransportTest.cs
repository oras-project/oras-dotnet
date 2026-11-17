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

        public async Task<byte[]> ReadBodyAsync()
        {
            var length = headers.Split("\r\n")
                .FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            var body = new byte[length is null ? 0 : int.Parse(length.Split(':')[1])];
            await connection.GetStream().ReadExactlyAsync(body, token);
            return body;
        }

        public async Task ReplyAsync(int status, string? location = null)
        {
            var response = $"HTTP/1.1 {status} Test\r\nContent-Length: 0\r\nConnection: close\r\n";
            if (location is not null)
            {
                response += $"Location: {location}\r\n";
            }
            await connection.GetStream().WriteAsync(Encoding.ASCII.GetBytes(response + "\r\n"), token);
        }

        public void ResetConnection() => connection.Client.LingerState = new LingerOption(true, 0);
    }
}
