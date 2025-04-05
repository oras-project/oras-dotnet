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

        // Act
        var clonedRequest = await originalRequest.CloneAsync();

        // Assert
        Assert.True(clonedRequest.Headers.Contains("Custom-Header"));
        Assert.Equal("HeaderValue", clonedRequest.Headers.GetValues("Custom-Header").FirstOrDefault());
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
        var originalContent = new ByteArrayContent(new byte[0]);

        // Act
        var clonedContent = await originalContent.CloneAsync();

        // Assert
        Assert.NotNull(clonedContent);
        Assert.Empty(clonedContent.Headers);
    }
}
