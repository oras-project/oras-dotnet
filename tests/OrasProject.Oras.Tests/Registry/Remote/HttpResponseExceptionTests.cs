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

using OrasProject.Oras.Registry.Remote;
using Xunit;

namespace OrasProject.Oras.Tests.Remote;

public class HttpResponseMessageExtensionsTests
{
    [Theory]
    [InlineData("", "")] // no header
    [InlineData("<http://example.com/next>", "http://example.com/next")]
    [InlineData("<http://example.com/next>; rel=\"next\"", "http://example.com/next")]
    public void ParseLink_ValidLinkHeader_ReturnsUri(string link, string expectedLink)
    {
        // Arrange
        var response = new HttpResponseMessage
        {
            RequestMessage = new HttpRequestMessage()
            {
                RequestUri = new Uri("http://example.com")
            }
        };

        if (!string.IsNullOrEmpty(link))
        {
            response.Headers.Add("Link", link);
        }

        // Act
        var linkUri = response.ParseLink();

        // Validate the uri in the link header 
        if (string.IsNullOrEmpty(expectedLink))
        {
            Assert.Null(linkUri);
            return;
        }
        Assert.NotNull(linkUri);
        Assert.Equal(new Uri(expectedLink), linkUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<")]
    [InlineData("<http://example.com/first; rel=\"next\"")]
    [InlineData("<in valid URI>; rel=\"next\"")]
    [InlineData("; rel=\"next\"")]
    public void ParseLink_InvalidLinkHeader_ThrowsHttpIOException(string link)
    {
        // Arrange
        var response = new HttpResponseMessage();
        response.Headers.Add("Link", link);

        // Act & Assert
        var exception = Assert.Throws<HttpIOException>(() => response.ParseLink());
        Assert.Equal(HttpRequestError.InvalidResponse, exception.HttpRequestError);
    }


    [Fact]
    public async Task VerifyContentDigest_ValidDigest_DoesNotThrow()
    {
        string content = "test content";
        // Arrange
        var response = new HttpResponseMessage
        {
            Content = new StringContent("")
        };

        // Compute hash of test content
        var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        var digest = $"sha256:{BitConverter.ToString(hash).Replace("-", "").ToLower()}";
        response.Headers.Add("Docker-Content-Digest", digest);
        var expectedDigest = digest;

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => Task.Run(() => response.VerifyContentDigest(expectedDigest)));
        Assert.Null(exception);
    }

    [Fact]
    public async Task VerifyContentDigest_InvalidDigest_ThrowsHttpIOException()
    {
        // Arrange
        var response = new HttpResponseMessage
        {
            Content = new StringContent("test content"),
            RequestMessage = new HttpRequestMessage()
            {
                RequestUri = new Uri("http://example.com")
            }
        };

        response.Headers.Add("Docker-Content-Digest", "sha256:invaliddigest");
        var expectedDigest = "sha256:validdigest";

        // Act & Assert
        await Assert.ThrowsAsync<HttpIOException>(() => Task.Run(() => response.VerifyContentDigest(expectedDigest)));
    }
}
