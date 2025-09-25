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
        string expectedMessage = "GET https://example.com/v2/repo/manifests/tag returned 404 NotFound: NAME_UNKNOWN: repository name not known to registry";
        Assert.Equal(expectedMessage, exception.Message);
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
        string expectedMessage = "POST https://example.com/v2/token returned 401 Unauthorized: Authentication failed";
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
        string expectedMessage = "PUT https://example.com/v2/repo/blobs/uploads/ returned 400 BadRequest: BLOB_UPLOAD_INVALID: blob upload invalid; DIGEST_INVALID: provided digest did not match uploaded content";
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
        string expectedMessage = "PUT https://example.com/v2/repo/blobs/uploads/ returned 400 BadRequest: Failed to upload blob; BLOB_UPLOAD_INVALID: blob upload invalid; DIGEST_INVALID: provided digest did not match uploaded content";
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
        string expectedMessage = "GET https://example.com/v2/repo/manifests/tag returned 404 NotFound: NAME_UNKNOWN: repository name not known to registry";
            
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void ResponseException_Message_WithoutRequestMessage_UsesHttpPrefix()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
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
        string expectedMessage = "HTTP 404 NotFound: NAME_UNKNOWN: repository name not known to registry";
            
        Assert.Equal(expectedMessage, exception.Message);
    }
    
    [Fact]
    public void ResponseException_Message_WithNoErrorsAtAll_OnlyShowsStatusCode()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repo/manifests/tag")
        };

        // Act
        var exception = new ResponseException(response, null);
        
        // Assert
        string expectedMessage = "GET https://example.com/v2/repo/manifests/tag returned 500 InternalServerError";
            
        Assert.Equal(expectedMessage, exception.Message);
    }
    
    [Fact]
    public void ResponseException_Message_WithEmptyErrorsList_OnlyShowsStatusCode()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repo/manifests/tag")
        };
        
        var responseBody = @"{""errors"": []}";

        // Act
        var exception = new ResponseException(response, responseBody);
        
        // Assert
        string expectedMessage = "GET https://example.com/v2/repo/manifests/tag returned 500 InternalServerError";
            
        Assert.Equal(expectedMessage, exception.Message);
    }
    
    [Fact]
    public void ResponseException_Message_NoErrors_ShowsBasicInfo()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Head, "https://registry.example/v2/library/alpine/blobs/sha256:deadbeef")
        };

        // Act
        var ex = new ResponseException(response, "not-json");

        // Assert
        Assert.Equal("HEAD https://registry.example/v2/library/alpine/blobs/sha256:deadbeef returned 500 InternalServerError", ex.Message);
    }
    
    [Fact]
    public void ResponseException_Message_WithDetailField_IncludesDetailInMessage()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Put, "https://registry.example/v2/library/alpine/blobs/uploads/")
        };
        
        var responseBody = @"{
            ""errors"": [
                {
                    ""code"": ""MANIFEST_INVALID"",
                    ""message"": ""manifest invalid"",
                    ""detail"": {
                        ""validationErrors"": [
                            {
                                ""field"": ""layers.0.mediaType"",
                                ""message"": ""invalid media type""
                            }
                        ]
                    }
                }
            ]
        }";

        // Act
        var exception = new ResponseException(response, responseBody);
        
        // Assert
        string expectedMessage = "PUT https://registry.example/v2/library/alpine/blobs/uploads/ returned 400 BadRequest: MANIFEST_INVALID: manifest invalid (Detail: {\"validationErrors\":[{\"field\":\"layers.0.mediaType\",\"message\":\"invalid media type\"}]})";
        Assert.Equal(expectedMessage, exception.Message);
    }
    
    [Fact]
    public void ResponseException_Message_WithMultipleErrorsAndDetails_FormatsCorrectly()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Put, "https://registry.example/v2/library/alpine/manifests/latest")
        };
        
        var responseBody = @"{
            ""errors"": [
                {
                    ""code"": ""MANIFEST_INVALID"",
                    ""message"": ""manifest invalid"",
                    ""detail"": {
                        ""validationErrors"": [
                            {
                                ""field"": ""layers.0.mediaType"",
                                ""message"": ""invalid media type""
                            }
                        ]
                    }
                },
                {
                    ""code"": ""TAG_INVALID"",
                    ""message"": ""tag name invalid"",
                    ""detail"": {
                        ""reason"": ""tag contains invalid characters""
                    }
                }
            ]
        }";

        // Act
        var exception = new ResponseException(response, responseBody);
        
        // Assert
        string expectedMessage = "PUT https://registry.example/v2/library/alpine/manifests/latest returned 400 BadRequest: MANIFEST_INVALID: manifest invalid (Detail: {\"validationErrors\":[{\"field\":\"layers.0.mediaType\",\"message\":\"invalid media type\"}]}); TAG_INVALID: tag name invalid (Detail: {\"reason\":\"tag contains invalid characters\"})";
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void ResponseException_Message_WithDefaultNetExceptionMessage_ShouldNotIncludeDefaultMessage()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repo/manifests/tag")
        };
        
        var responseBody = @"{
            ""errors"": [
                {
                    ""code"": ""SERVER_ERROR"",
                    ""message"": ""internal server error""
                }
            ]
        }";

        // Act
        var exception = new ResponseException(response, responseBody, "Exception of type 'System.Net.Http.HttpRequestException' was thrown.");
        
        // Assert
        // Verify that the default exception message is excluded but registry errors are included
        string expectedMessage = "GET https://example.com/v2/repo/manifests/tag returned 502 BadGateway: SERVER_ERROR: internal server error";
        Assert.Equal(expectedMessage, exception.Message);
    }
    
    [Fact]
    public void ResponseException_Message_WithHttpClientDefaultMessage_ShouldNotIncludeDefaultMessage()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repo/manifests/tag")
        };
        
        var responseBody = @"{
            ""errors"": [
                {
                    ""code"": ""SERVER_ERROR"",
                    ""message"": ""internal server error""
                }
            ]
        }";

        // Act
        var exception = new ResponseException(response, responseBody, "An error occurred while sending the request.");
        
        // Assert
        // Verify that the default exception message is excluded but registry errors are included
        string expectedMessage = "GET https://example.com/v2/repo/manifests/tag returned 502 BadGateway: SERVER_ERROR: internal server error";
        Assert.Equal(expectedMessage, exception.Message);
    }
    
    [Fact]
    public void ResponseException_Message_WithStatusCodeDefaultMessage_ShouldNotIncludeDefaultMessage()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repo/manifests/tag")
        };
        
        var responseBody = @"{
            ""errors"": [
                {
                    ""code"": ""SERVER_ERROR"",
                    ""message"": ""internal server error""
                }
            ]
        }";

        // Act
        var exception = new ResponseException(response, responseBody, "Response status code does not indicate success.");
        
        // Assert
        // Verify that the default exception message is excluded but registry errors are included
        string expectedMessage = "GET https://example.com/v2/repo/manifests/tag returned 502 BadGateway: SERVER_ERROR: internal server error";
        Assert.Equal(expectedMessage, exception.Message);
    }
    
    [Fact]
    public void ResponseException_Message_WithGenericErrorMessage_ShouldNotIncludeDefaultMessage()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/v2/repo/manifests/tag")
        };
        
        var responseBody = @"{
            ""errors"": [
                {
                    ""code"": ""SERVER_ERROR"",
                    ""message"": ""internal server error""
                }
            ]
        }";

        // Act
        var exception = new ResponseException(response, responseBody, "Error.");
        
        // Assert
        // Verify that the default exception message is excluded but registry errors are included
        string expectedMessage = "GET https://example.com/v2/repo/manifests/tag returned 502 BadGateway: SERVER_ERROR: internal server error";
        Assert.Equal(expectedMessage, exception.Message);
    }
}
