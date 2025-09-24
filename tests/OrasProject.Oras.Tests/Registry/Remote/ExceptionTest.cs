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

using OrasProject.Oras.Registry.Remote.Exceptions;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote;

public class ExceptionTest
{
    [Fact]
    public async Task ReferrersSupportLevelAlreadySetException()
    {
        await Assert.ThrowsAsync<ReferrersStateAlreadySetException>(() => throw new ReferrersStateAlreadySetException());
        await Assert.ThrowsAsync<ReferrersStateAlreadySetException>(() => throw new ReferrersStateAlreadySetException("Referrers state has already been set"));
        await Assert.ThrowsAsync<ReferrersStateAlreadySetException>(() => throw new ReferrersStateAlreadySetException("Referrers state has already been set", null));
    }

    [Fact]
    public async Task InvalidResponseException()
    {
        await Assert.ThrowsAsync<InvalidResponseException>(() => throw new InvalidResponseException());
        await Assert.ThrowsAsync<InvalidResponseException>(() =>
            throw new InvalidResponseException("Invalid response"));
        await Assert.ThrowsAsync<InvalidResponseException>(() =>
            throw new InvalidResponseException("Invalid response", null));
    }

    [Fact]
    public void ResponseException_WithDefaultMessage_GeneratesDescriptiveMessage()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repository/manifests/latest");
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            RequestMessage = request
        };

        // Act
        var exception = new ResponseException(response);

        // Assert
        Assert.Contains("GET https://example.com/v2/repository/manifests/latest", exception.Message);
        Assert.Contains("401 Unauthorized", exception.Message);
    }

    [Fact]
    public void ResponseException_WithErrorResponseBody_IncludesErrorDetails()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repository/manifests/latest");
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            RequestMessage = request
        };
        var errorJson = """{"errors":[{"code":"UNAUTHORIZED","message":"authentication required"}]}""";

        // Act
        var exception = new ResponseException(response, errorJson);

        // Assert
        Assert.Contains("GET https://example.com/v2/repository/manifests/latest", exception.Message);
        Assert.Contains("401 Unauthorized", exception.Message);
        Assert.Contains("UNAUTHORIZED: authentication required", exception.Message);
    }

    [Fact]
    public void ResponseException_WithMultipleErrors_IncludesAllErrors()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/v2/repository/blobs/uploads/");
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            RequestMessage = request
        };
        var errorJson = """{"errors":[{"code":"INVALID_REQUEST","message":"missing required field"},{"code":"BAD_FORMAT","message":"invalid json"}]}""";

        // Act
        var exception = new ResponseException(response, errorJson);

        // Assert
        Assert.Contains("POST https://example.com/v2/repository/blobs/uploads/", exception.Message);
        Assert.Contains("400 BadRequest", exception.Message);
        Assert.Contains("INVALID_REQUEST: missing required field", exception.Message);
        Assert.Contains("BAD_FORMAT: invalid json", exception.Message);
    }

    [Fact]
    public void ResponseException_WithInvalidJsonResponseBody_IncludesRawBody()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repository/manifests/latest");
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            RequestMessage = request
        };
        var invalidJson = "Not valid JSON content";

        // Act
        var exception = new ResponseException(response, invalidJson);

        // Assert
        Assert.Contains("GET https://example.com/v2/repository/manifests/latest", exception.Message);
        Assert.Contains("500 InternalServerError", exception.Message);
        Assert.Contains("Not valid JSON content", exception.Message);
    }

    [Fact]
    public void ResponseException_WithLongInvalidResponseBody_TruncatesBody()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repository/manifests/latest");
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            RequestMessage = request
        };
        var longBody = new string('x', 300); // Longer than 200 characters

        // Act
        var exception = new ResponseException(response, longBody);

        // Assert
        Assert.Contains("GET https://example.com/v2/repository/manifests/latest", exception.Message);
        Assert.Contains("500 InternalServerError", exception.Message);
        Assert.DoesNotContain(longBody, exception.Message); // Should not include the long body
    }

    [Fact]
    public void ResponseException_WithExplicitMessage_UsesProvidedMessage()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repository/manifests/latest");
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            RequestMessage = request
        };
        var customMessage = "Custom error message";

        // Act
        var exception = new ResponseException(response, null, customMessage);

        // Assert
        Assert.Equal(customMessage, exception.Message);
    }

    [Fact]
    public void ResponseException_WithNullRequest_HandlesGracefully()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            RequestMessage = null
        };

        // Act
        var exception = new ResponseException(response);

        // Assert
        Assert.Contains("UNKNOWN unknown", exception.Message);
        Assert.Contains("400 BadRequest", exception.Message);
    }
}
