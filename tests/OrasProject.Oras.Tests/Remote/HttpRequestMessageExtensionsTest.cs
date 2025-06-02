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
    public async Task CloneAsync_ShouldCloneRequestMethodAndUri()
    {
        // Arrange
        var originalRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        var clonedRequest = await originalRequest.CloneAsync(CancellationToken.None);

        // Assert
        Assert.Equal(originalRequest.Method, clonedRequest.Method);
        Assert.Equal(originalRequest.RequestUri, clonedRequest.RequestUri);
    }

    [Fact]
    public async Task CloneAsync_ShouldCloneHeaders()
    {
        // Arrange
        var originalRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        originalRequest.Headers.Add("Custom-Header", "HeaderValue");
        originalRequest.Headers.Add("Custom-Header", "HeaderValue1");

        originalRequest.Headers.Add("key", "value");


        // Act
        var clonedRequest = await originalRequest.CloneAsync(CancellationToken.None);

        // Assert
        Assert.True(clonedRequest.Headers.Contains("Custom-Header"));
        Assert.True(clonedRequest.Headers.Contains("key"));
        var expectedValues = new List<string> { "HeaderValue", "HeaderValue1" };
        foreach (var value in expectedValues)
        {
            Assert.Contains(value, clonedRequest.Headers.GetValues("Custom-Header"));
        }
        Assert.Equal("value", clonedRequest.Headers.GetValues("key").FirstOrDefault());
    }

    [Fact]
    public async Task CloneAsync_ShouldCloneContent()
    {
        // Arrange
        var originalRequest = new HttpRequestMessage(HttpMethod.Post, "https://example.com")
        {
            Content = new StringContent("Test content")
        };

        // Act
        var clonedRequest = await originalRequest.CloneAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(clonedRequest.Content);
        Assert.Equal(await originalRequest.Content.ReadAsStringAsync(), await clonedRequest.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CloneAsync_ShouldCloneOptions()
    {
        // Arrange
        var originalRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var customOptionKey = new HttpRequestOptionsKey<string>("Custom-Option");
        originalRequest.Options.TryAdd("Custom-Option", "OptionValue");

        // Act
        var clonedRequest = await originalRequest.CloneAsync(CancellationToken.None);

        // Assert
        Assert.True(clonedRequest.Options.TryGetValue(customOptionKey, out var clonedOptionValue));
        Assert.Equal("OptionValue", clonedOptionValue);
    }

    [Fact]
    public async Task CloneAsync_ShouldCloneVersion()
    {
        // Arrange
        var originalRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com")
        {
            Version = new Version(2, 0)
        };

        // Act
        var clonedRequest = await originalRequest.CloneAsync(CancellationToken.None);

        // Assert
        Assert.Equal(originalRequest.Version, clonedRequest.Version);
    }

    [Fact]
    public async Task CloneAsync_ShouldHandleNullContent()
    {
        // Arrange
        var originalRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        var clonedRequest = await originalRequest.CloneAsync(CancellationToken.None);

        // Assert
        Assert.Null(clonedRequest.Content);
    }

    [Fact]
    public async Task RewindAsync_ShouldCloneContentData()
    {
        // Arrange
        var originalContent = new StringContent("Test content");

        // Act
        var clonedContent = await originalContent.RewindAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(clonedContent);
        Assert.Equal(await originalContent.ReadAsStringAsync(), await clonedContent.ReadAsStringAsync());
    }

    [Fact]
    public async Task RewindAsync_ShouldCloneContentHeaders()
    {
        // Arrange
        var originalContent = new StringContent("Test content");
        originalContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        originalContent.Headers.ContentLength = 12;

        // Act
        var clonedContent = await originalContent.RewindAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(clonedContent);
        Assert.NotNull(clonedContent.Headers.ContentType);
        Assert.Equal(originalContent.Headers.ContentType, clonedContent.Headers.ContentType);
        Assert.Equal(originalContent.Headers.ContentLength, clonedContent.Headers.ContentLength);
    }

    [Fact]
    public async Task RewindAsync_ShouldHandleEmptyContent()
    {
        // Arrange
        var originalContent = new StringContent(string.Empty);

        // Act
        var clonedContent = await originalContent.RewindAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(clonedContent);
        Assert.Equal(await originalContent.ReadAsStringAsync(), await clonedContent.ReadAsStringAsync());
    }

    [Fact]
    public async Task RewindAsync_CloneMultipleTimes_ShouldCloneSameContent()
    {
        // Arrange
        var originalContent = new StringContent("Test content");
        originalContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        originalContent.Headers.ContentLength = 12;

        // Act & Assert
        var clonedContent1 = await originalContent.RewindAsync(CancellationToken.None);
        Assert.NotNull(clonedContent1);
        Assert.Equal(await originalContent.ReadAsStringAsync(), await clonedContent1.ReadAsStringAsync());

        var clonedContent2 = await originalContent.RewindAsync(CancellationToken.None);
        Assert.NotNull(clonedContent2);
        Assert.Equal(await originalContent.ReadAsStringAsync(), await clonedContent2.ReadAsStringAsync());
    }

    [Fact]
    public async Task RewindAsync_ShouldHandleNullHeaders()
    {
        // Arrange
        var originalContent = new ByteArrayContent(Array.Empty<byte>());

        // Act
        var clonedContent = await originalContent.RewindAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(clonedContent);
        Assert.Empty(clonedContent.Headers);
    }

    [Fact]
    public async Task RewindAsync_ShouldHandleNullContent()
    {
        // Arrange
        HttpContent? originalContent = null;
        // Act
        var clonedContent = await originalContent.RewindAsync(CancellationToken.None);
        // Assert
        Assert.Null(clonedContent);
    }

    [Fact]
    public async Task RewindAsync_NonSeekableContent_ShouldThrow()
    {
        // Arrange
        var nonSeekableStream = new NonSeekableStream(new byte[] { 1, 2, 3, 4, 5 });
        var originalContent = new StreamContent(nonSeekableStream);

        // Act & Assert
        await Assert.ThrowsAsync<IOException>(() => originalContent.RewindAsync(CancellationToken.None));
    }
}
