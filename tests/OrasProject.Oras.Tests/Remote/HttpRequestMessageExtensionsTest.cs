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

using System.Net.Http.Headers;
using OrasProject.Oras.Registry.Remote;
using Xunit;

namespace OrasProject.Oras.Tests.Remote;

public class HttpRequestMessageExtensionsTest
{
    [Fact]
    public async Task CloneAsync_ShouldCloneRequest()
    {
        // Arrange
        var originalRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com")
        {
            Version = new Version(2, 0),
            Content = new StringContent("Test content")
        };
        originalRequest.Headers.Add("Custom-Header", "HeaderValue");
        originalRequest.Headers.Add("Custom-Header", "HeaderValue1");
        originalRequest.Headers.Add("key", "value");
        var customOptionKey = new HttpRequestOptionsKey<string>("Custom-Option");
        originalRequest.Options.TryAdd("Custom-Option", "OptionValue");


        // Act
        var clonedRequest = await originalRequest.CloneAsync();

        // Assert
        Assert.NotNull(clonedRequest);
        Assert.NotSame(originalRequest, clonedRequest);

        // Check method and URI
        Assert.Equal(originalRequest.Method, clonedRequest.Method);
        Assert.Equal(originalRequest.RequestUri, clonedRequest.RequestUri);
        // Check version
        Assert.Equal(originalRequest.Version, clonedRequest.Version);
        // Checck content
        Assert.NotNull(clonedRequest.Content);
        Assert.NotSame(originalRequest.Content, clonedRequest.Content);
        Assert.Equal(await originalRequest.Content.ReadAsStringAsync(), await clonedRequest.Content.ReadAsStringAsync());
        // Check headers
        Assert.True(clonedRequest.Headers.Contains("Custom-Header"));
        Assert.True(clonedRequest.Headers.Contains("key"));
        var expectedValues = new List<string> { "HeaderValue", "HeaderValue1" };
        foreach (var value in expectedValues)
        {
            Assert.Contains(value, clonedRequest.Headers.GetValues("Custom-Header"));
        }
        Assert.Equal("value", clonedRequest.Headers.GetValues("key").FirstOrDefault());
        // Check options
        Assert.True(clonedRequest.Options.TryGetValue(customOptionKey, out var clonedOptionValue));
        Assert.Equal("OptionValue", clonedOptionValue);
    }

    [Fact]
    public async Task CloneAsync_ShouldCloneRequest_ReuseContent()
    {
        // Arrange
        var originalRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com")
        {
            Version = new Version(2, 0),
            Content = new StringContent("Test content")
        };
        originalRequest.Headers.Add("Custom-Header", "HeaderValue");
        originalRequest.Headers.Add("Custom-Header", "HeaderValue1");
        originalRequest.Headers.Add("key", "value");
        var customOptionKey = new HttpRequestOptionsKey<string>("Custom-Option");
        originalRequest.Options.TryAdd("Custom-Option", "OptionValue");


        // Act
        var clonedRequest = await originalRequest.CloneAsync(rewindContent: false);

        // Assert
        Assert.NotNull(clonedRequest);
        Assert.NotSame(originalRequest, clonedRequest);

        // Check method and URI
        Assert.Equal(originalRequest.Method, clonedRequest.Method);
        Assert.Equal(originalRequest.RequestUri, clonedRequest.RequestUri);
        // Check version
        Assert.Equal(originalRequest.Version, clonedRequest.Version);
        // Checck content
        Assert.NotNull(clonedRequest.Content);
        Assert.Same(originalRequest.Content, clonedRequest.Content);
        Assert.Equal(await originalRequest.Content.ReadAsStringAsync(), await clonedRequest.Content.ReadAsStringAsync());
        // Check headers
        Assert.True(clonedRequest.Headers.Contains("Custom-Header"));
        Assert.True(clonedRequest.Headers.Contains("key"));
        var expectedValues = new List<string> { "HeaderValue", "HeaderValue1" };
        foreach (var value in expectedValues)
        {
            Assert.Contains(value, clonedRequest.Headers.GetValues("Custom-Header"));
        }
        Assert.Equal("value", clonedRequest.Headers.GetValues("key").FirstOrDefault());
        // Check options
        Assert.True(clonedRequest.Options.TryGetValue(customOptionKey, out var clonedOptionValue));
        Assert.Equal("OptionValue", clonedOptionValue);
    }

    [Fact]
    public async Task CloneAsync_ShouldHandleNullContent()
    {
        // Arrange
        var originalRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        var clonedRequest = await originalRequest.CloneAsync();

        // Assert
        Assert.Null(clonedRequest.Content);
    }

    [Fact]
    public async Task RewindAndCloneAsync_ShouldCloneContentData()
    {
        // Arrange
        var originalContent = new StringContent("Test content");

        // Act
        var clonedContent = await originalContent.RewindAndCloneAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(clonedContent);
        Assert.Equal(await originalContent.ReadAsStringAsync(), await clonedContent.ReadAsStringAsync());
    }

    [Fact]
    public async Task RewindAndCloneAsync_ShouldCloneContentHeaders()
    {
        // Arrange
        var originalContent = new StringContent("Test content");
        originalContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        originalContent.Headers.ContentLength = 12;

        // Act
        var clonedContent = await originalContent.RewindAndCloneAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(clonedContent);
        Assert.NotNull(clonedContent.Headers.ContentType);
        Assert.Equal(originalContent.Headers.ContentType, clonedContent.Headers.ContentType);
        Assert.Equal(originalContent.Headers.ContentLength, clonedContent.Headers.ContentLength);
    }

    [Fact]
    public async Task RewindAndCloneAsync_ShouldHandleEmptyContent()
    {
        // Arrange
        var originalContent = new StringContent(string.Empty);

        // Act
        var clonedContent = await originalContent.RewindAndCloneAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(clonedContent);
        Assert.Equal(await originalContent.ReadAsStringAsync(), await clonedContent.ReadAsStringAsync());
    }

    [Fact]
    public async Task RewindAndCloneAsync_CloneMultipleTimes_ShouldCloneSameContent()
    {
        // Arrange
        var originalContent = new StringContent("Test content");
        originalContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        originalContent.Headers.ContentLength = 12;

        // Act & Assert
        var clonedContent1 = await originalContent.RewindAndCloneAsync(CancellationToken.None);
        Assert.NotNull(clonedContent1);
        Assert.Equal(await originalContent.ReadAsStringAsync(), await clonedContent1.ReadAsStringAsync());

        var clonedContent2 = await originalContent.RewindAndCloneAsync(CancellationToken.None);
        Assert.NotNull(clonedContent2);
        Assert.Equal(await originalContent.ReadAsStringAsync(), await clonedContent2.ReadAsStringAsync());
    }

    [Fact]
    public async Task RewindAndCloneAsync_ShouldHandleNullHeaders()
    {
        // Arrange
        var originalContent = new ByteArrayContent(Array.Empty<byte>());

        // Act
        var clonedContent = await originalContent.RewindAndCloneAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(clonedContent);
        Assert.Empty(clonedContent.Headers);
    }

    [Fact]
    public async Task RewindAndCloneAsync_ShouldHandleNullContent()
    {
        // Arrange
        HttpContent? originalContent = null;
        // Act
        var clonedContent = await originalContent.RewindAndCloneAsync(CancellationToken.None);
        // Assert
        Assert.Null(clonedContent);
    }

    [Fact]
    public async Task RewindAndCloneAsync_NonSeekableContent_ShouldThrow()
    {
        // Arrange
        var nonSeekableStream = new NonSeekableStream(new byte[] { 1, 2, 3, 4, 5 });
        var originalContent = new StreamContent(nonSeekableStream);

        // Act & Assert
        await Assert.ThrowsAsync<IOException>(() => originalContent.RewindAndCloneAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RewindAndCloneAsync_DisposedStream_ShouldThrow()
    {
        // Arrange
        var disposedStream = new MemoryStream();
        var originalContent = new StreamContent(disposedStream);
        originalContent.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => originalContent.RewindAndCloneAsync(CancellationToken.None));
    }
}
