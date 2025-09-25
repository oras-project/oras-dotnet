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
            var message = new StringBuilder();
            
            // Start with request details (Method and URI) if available
            if (Method != null && RequestUri != null)
            {
                message.Append($"{Method} {RequestUri}");
                message.Append($" returned {(int)StatusCode} {StatusCode}");
            }
            else
            {
                // Fallback if request details aren't available
                message.Append($"HTTP {(int)StatusCode} {StatusCode}");
            }
            
            // Add custom message if provided (and it's not the default exception message)
            string? customMessage = null;
            if (!string.IsNullOrWhiteSpace(base.Message) && 
                !base.Message.StartsWith("Exception of type"))
            {
                customMessage = base.Message;
                message.Append($": {customMessage}");
            }
            
            // Add error details
            if (Errors != null && Errors.Count > 0)
            {
                // If no custom message was added, add a colon before errors
                if (customMessage == null)
                {
                    message.Append(": ");
                }
                else
                {
                    message.Append("; ");
                }
                
                // Join multiple errors with semicolons
                for (int i = 0; i < Errors.Count; i++)
                {
                    if (i > 0)
                    {
                        message.Append("; ");
                    }
                    message.Append($"{Errors[i].Code}: {Errors[i].Message}");
                    
                    // Include detail field if present
                    var detail = Errors[i].Detail;
                    if (detail != null && detail.HasValue)
                    {
                        JsonElement detailValue = detail.Value;
                        if (detailValue.ValueKind != JsonValueKind.Null && detailValue.ValueKind != JsonValueKind.Undefined)
                        {
                            try
                            {
                                var detailJson = JsonSerializer.Serialize(detailValue, new JsonSerializerOptions { WriteIndented = false });
                                message.Append($" (Detail: {detailJson})");
                            }
                            catch
                            {
                                // If serialization fails, continue without detail
                            }
                        }
                    }
                }
            }

            return message.ToString();
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
