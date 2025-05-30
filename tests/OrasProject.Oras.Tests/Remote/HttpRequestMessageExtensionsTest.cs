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
        var clonedRequest = await originalRequest.CloneAsync();

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
        var clonedRequest = await originalRequest.CloneAsync();

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
        var clonedRequest = await originalRequest.CloneAsync();

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
        var clonedRequest = await originalRequest.CloneAsync();

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
        var clonedRequest = await originalRequest.CloneAsync();

        // Assert
        Assert.Equal(originalRequest.Version, clonedRequest.Version);
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
    public async Task CloneAsync_ShouldCloneContentData()
    {
        // Arrange
        var originalContent = new StringContent("Test content");

        // Act
        var clonedContent = await originalContent.CloneAsync();

        // Assert
        Assert.NotNull(clonedContent);
        Assert.Equal(await originalContent.ReadAsStringAsync(), await clonedContent.ReadAsStringAsync());
    }

    [Fact]
    public async Task CloneAsync_ShouldCloneContentHeaders()
    {
        // Arrange
        var originalContent = new StringContent("Test content");
        originalContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        originalContent.Headers.ContentLength = 12;

        // Act
        var clonedContent = await originalContent.CloneAsync();

        // Assert
        Assert.NotNull(clonedContent.Headers.ContentType);
        Assert.Equal(originalContent.Headers.ContentType, clonedContent.Headers.ContentType);
        Assert.Equal(originalContent.Headers.ContentLength, clonedContent.Headers.ContentLength);
    }

    [Fact]
    public async Task CloneAsync_ShouldHandleEmptyContent()
    {
        // Arrange
        var originalContent = new StringContent(string.Empty);

        // Act
        var clonedContent = await originalContent.CloneAsync();

        // Assert
        Assert.NotNull(clonedContent);
        Assert.Equal(await originalContent.ReadAsStringAsync(), await clonedContent.ReadAsStringAsync());
    }

    [Fact]
    public async Task CloneAsync_ShouldHandleNullHeaders()
    {
        // Arrange
        var originalContent = new ByteArrayContent(Array.Empty<byte>());

        // Act
        var clonedContent = await originalContent.CloneAsync();

        // Assert
        Assert.NotNull(clonedContent);
        Assert.Empty(clonedContent.Headers);
    }

    [Fact]
    public async Task CloneAsync_ShouldCloneNonSeekableContent()
    {
        // Arrange
        byte[] testData = { 1, 2, 3, 4, 5 };
        var nonSeekableStream = new NonSeekableStream(testData);
        var originalContent = new StreamContent(nonSeekableStream);

        // Act
        var clonedContent = await originalContent.CloneAsync();

        // Assert
        Assert.NotNull(clonedContent);
        byte[] clonedBytes = await clonedContent.ReadAsByteArrayAsync();
        Assert.Equal(testData, clonedBytes);
    }

    [Fact]
    public async Task CloneAsync_WithEmptyNonSeekableContent_ShouldCloneCorrectly()
    {
        // Arrange
        // Create an empty non-seekable stream
        using var nonSeekableStream = new NonSeekableStream(Array.Empty<byte>());
        var originalContent = new StreamContent(nonSeekableStream);

        // Act
        var clonedContent = await originalContent.CloneAsync();

        // Assert
        Assert.NotNull(clonedContent);
        byte[] clonedBytes = await clonedContent.ReadAsByteArrayAsync();
        Assert.Empty(clonedBytes);
    }

    [Fact]
    public async Task CloneAsync_ShouldCloneNonSeekableContentHeaders()
    {
        // Arrange
        // Arrange
        byte[] testData = { 1, 2, 3, 4, 5 };
        using var nonSeekableStream = new NonSeekableStream(testData);
        var originalContent = new StreamContent(nonSeekableStream);
        originalContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        originalContent.Headers.ContentLength = testData.Length;

        // Act
        var clonedContent = await originalContent.CloneAsync();

        // Assert
        Assert.NotNull(clonedContent.Headers.ContentType);
        Assert.Equal(originalContent.Headers.ContentType, clonedContent.Headers.ContentType);
        Assert.Equal(originalContent.Headers.ContentLength, clonedContent.Headers.ContentLength);
    }
}
