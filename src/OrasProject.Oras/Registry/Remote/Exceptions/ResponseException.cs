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
            
            // Start with HTTP status info
            message.Append($"HTTP {(int)StatusCode} {StatusCode}");
            
            // Add request details if available
            if (Method != null && RequestUri != null)
            {
                message.Append($" for {Method} {RequestUri}");
            }
            
            // Add custom message if provided
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
                // Add semicolon before errors if there's already content
                message.Append(customMessage == null ? ": " : "; ");
                
                if (Errors.Count == 1)
                {
                    // For a single error, include it in the same line
                    message.Append($"{Errors[0].Code}: {Errors[0].Message}");
                }
                else
                {
                    // For multiple errors, list them on separate lines
                    message.Append("Registry errors:");
                    foreach (var error in Errors)
                    {
                        message.AppendLine();
                        message.Append($"  - {error.Code}: {error.Message}");
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
