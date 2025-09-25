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
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrasProject.Oras.Registry.Remote.Exceptions;

/// <summary>
/// Exception thrown for HTTP responses from registry operations.
/// </summary>
public class ResponseException : HttpRequestException
{
    private class ErrorResponse
    {
        [JsonPropertyName("errors")]
        public required IList<Error> Errors { get; set; }
    }

    /// <summary>
    /// Gets the HTTP method used in the request.
    /// </summary>
    public HttpMethod? Method { get; }

    /// <summary>
    /// Gets the URI of the request.
    /// </summary>
    public Uri? RequestUri { get; }

    /// <summary>
    /// Gets the list of errors returned in the response.
    /// </summary>
    public IList<Error>? Errors { get; }

    /// <summary>
    /// Gets the HTTP status code from the response.
    /// </summary>
    public new HttpStatusCode StatusCode => base.StatusCode ?? HttpStatusCode.InternalServerError;

    /// <summary>
    /// Gets the error message including HTTP details and registry errors.
    /// </summary>
    public override string Message
    {
        get
        {
            var messageBuilder = new StringBuilder();
            
            // Add HTTP request and status information
            if (Method != null && RequestUri != null)
            {
                messageBuilder.Append($"{Method} {RequestUri}");
                messageBuilder.Append($" returned {(int)StatusCode} {StatusCode}");
            }
            else
            {
                messageBuilder.Append($"HTTP {(int)StatusCode} {StatusCode}");
            }
            
            // Add custom message if provided and it's not the default exception message
            string? customMessage = null;
            if (!string.IsNullOrWhiteSpace(base.Message) && 
                !base.Message.StartsWith("Exception of type"))
            {
                customMessage = base.Message;
                messageBuilder.Append($": {customMessage}");
            }
            
            // Add error details if available
            if (Errors != null && Errors.Count > 0)
            {
                // Add appropriate delimiter before errors
                messageBuilder.Append(customMessage == null ? ": " : "; ");
                
                // Format and add all error information
                for (int i = 0; i < Errors.Count; i++)
                {
                    if (i > 0)
                    {
                        messageBuilder.Append("; ");
                    }
                    
                    var error = Errors[i];
                    messageBuilder.Append($"{error.Code}: {error.Message}");
                    
                    // Add detail information if available
                    if (error.Detail is { } detailValue && 
                        detailValue.ValueKind != JsonValueKind.Null && 
                        detailValue.ValueKind != JsonValueKind.Undefined)
                    {
                        try
                        {
                            var detailJson = JsonSerializer.Serialize(detailValue, new JsonSerializerOptions { WriteIndented = false });
                            messageBuilder.Append($" (Detail: {detailJson})");
                        }
                        catch
                        {
                            // If serialization fails, continue without detail
                        }
                    }
                }
            }

            return messageBuilder.ToString();
        }
    }

    public ResponseException(HttpResponseMessage response, string? responseBody = null)
        : this(response, responseBody, null)
    {
    }

    public ResponseException(HttpResponseMessage response, string? responseBody, string? message)
        : this(response, responseBody, response.StatusCode == HttpStatusCode.Unauthorized ? HttpRequestError.UserAuthenticationError : HttpRequestError.Unknown, message, null)
    {
    }

    public ResponseException(HttpResponseMessage response, string? responseBody, HttpRequestError httpRequestError, string? message, Exception? inner)
        : base(httpRequestError, message, inner, response.StatusCode)
    {
        var request = response.RequestMessage;
        Method = request?.Method;
        RequestUri = request?.RequestUri;
        if (responseBody != null)
        {
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody);
                Errors = errorResponse?.Errors;
            }
            catch { }
        }
    }
}
