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

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Exceptions;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote.Exceptions;

public class ResponseExceptionTests
{
    [Fact]
    public void ResponseException_Message_ShouldContainBasicInfo()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repo/manifests/tag")
        };
        
        var responseBody = @"{
            ""errors"": [
                {
                    ""code"": ""NAME_UNKNOWN"",
                    ""message"": ""repository name not known to registry""
                }
            ]
        }";

        // Act
        var exception = new ResponseException(response, responseBody);
        
        // Assert
        var message = exception.Message;
        
        // Should contain HTTP status
        Assert.Contains("HTTP 404", message);
        Assert.Contains("NotFound", message);
        
        // Should contain request method and URL
        Assert.Contains("GET", message);
        Assert.Contains("https://example.com/v2/repo/manifests/tag", message);
        
        // Should contain error details (more concise format now)
        Assert.Contains("NAME_UNKNOWN:", message);
        Assert.Contains("repository name not known to registry", message);
    }
    
    [Fact]
    public void ResponseException_Message_WithCustomMessage_ShouldIncludeCustomMessage()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://example.com/v2/token")
        };
        
        var customMessage = "Authentication failed";
        
        // Act
        var exception = new ResponseException(response, null, customMessage);
        
        // Assert
        string expectedMessage = "HTTP 401 Unauthorized for POST https://example.com/v2/token: Authentication failed";
        Assert.Equal(expectedMessage, exception.Message);
    }
    
    [Fact]
    public void ResponseException_Message_WithMultipleErrors_ShouldIncludeAllErrors()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Put, "https://example.com/v2/repo/blobs/uploads/")
        };
        
        var responseBody = @"{
            ""errors"": [
                {
                    ""code"": ""BLOB_UPLOAD_INVALID"",
                    ""message"": ""blob upload invalid""
                },
                {
                    ""code"": ""DIGEST_INVALID"",
                    ""message"": ""provided digest did not match uploaded content""
                }
            ]
        }";

        // Act
        var exception = new ResponseException(response, responseBody);
        
        // Assert
        string expectedMessage = 
            "HTTP 400 BadRequest for PUT https://example.com/v2/repo/blobs/uploads/: Registry errors:" +
            Environment.NewLine +
            "  - BLOB_UPLOAD_INVALID: blob upload invalid" +
            Environment.NewLine +
            "  - DIGEST_INVALID: provided digest did not match uploaded content";
            
        Assert.Equal(expectedMessage, exception.Message);
    }
    
    [Fact]
    public void ResponseException_Message_WithCustomMessageAndMultipleErrors_ShouldFormatCorrectly()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Put, "https://example.com/v2/repo/blobs/uploads/")
        };
        
        var responseBody = @"{
            ""errors"": [
                {
                    ""code"": ""BLOB_UPLOAD_INVALID"",
                    ""message"": ""blob upload invalid""
                },
                {
                    ""code"": ""DIGEST_INVALID"",
                    ""message"": ""provided digest did not match uploaded content""
                }
            ]
        }";
        
        var customMessage = "Failed to upload blob";

        // Act
        var exception = new ResponseException(response, responseBody, customMessage);
        
        // Assert
        string expectedMessage = 
            "HTTP 400 BadRequest for PUT https://example.com/v2/repo/blobs/uploads/: Failed to upload blob; Registry errors:" +
            Environment.NewLine +
            "  - BLOB_UPLOAD_INVALID: blob upload invalid" +
            Environment.NewLine +
            "  - DIGEST_INVALID: provided digest did not match uploaded content";
            
        Assert.Equal(expectedMessage, exception.Message);
    }
    
    [Fact]
    public void ResponseException_Message_Format_ShouldMatchExactExpectedFormat()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repo/manifests/tag")
        };
        
        var responseBody = @"{
            ""errors"": [
                {
                    ""code"": ""NAME_UNKNOWN"",
                    ""message"": ""repository name not known to registry""
                }
            ]
        }";

        // Act
        var exception = new ResponseException(response, responseBody);
        
        // Expected format: HTTP status + request info + error details
        string expectedMessage = "HTTP 404 NotFound for GET https://example.com/v2/repo/manifests/tag: NAME_UNKNOWN: repository name not known to registry";
            
        Assert.Equal(expectedMessage, exception.Message);
    }
}