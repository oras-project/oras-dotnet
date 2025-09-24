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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrasProject.Oras.Registry.Remote.Exceptions;

public class ResponseException : HttpRequestException
{
    private class ErrorResponse
    {
        [JsonPropertyName("errors")]
        public required IList<Error> Errors { get; set; }
    }

    public HttpMethod? Method { get; }

    public Uri? RequestUri { get; }

    public IList<Error>? Errors { get; }

    public ResponseException(HttpResponseMessage response, string? responseBody = null)
        : this(response, responseBody, null)
    {
    }

    public ResponseException(HttpResponseMessage response, string? responseBody, string? message)
        : this(response, responseBody, response.StatusCode == HttpStatusCode.Unauthorized ? HttpRequestError.UserAuthenticationError : HttpRequestError.Unknown, message, null)
    {
    }

    public ResponseException(HttpResponseMessage response, string? responseBody, HttpRequestError httpRequestError, string? message, Exception? inner)
        : base(httpRequestError, message ?? GenerateDefaultMessage(response, responseBody), inner, response.StatusCode)
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

    private static string GenerateDefaultMessage(HttpResponseMessage response, string? responseBody)
    {
        var request = response.RequestMessage;
        var method = request?.Method?.ToString() ?? "UNKNOWN";
        var uri = request?.RequestUri?.ToString() ?? "unknown";
        var statusCode = (int)response.StatusCode;
        var statusName = response.StatusCode.ToString();

        var message = $"{method} {uri}: {statusCode} {statusName}";

        // Try to parse and include error details from response body
        if (!string.IsNullOrEmpty(responseBody))
        {
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody);
                if (errorResponse?.Errors != null && errorResponse.Errors.Count > 0)
                {
                    var errorMessages = errorResponse.Errors.Select(e => $"{e.Code}: {e.Message}").ToArray();
                    message += $" - {string.Join(", ", errorMessages)}";
                }
            }
            catch
            {
                // If we can't parse the response body, include it as-is if it's short enough
                if (responseBody.Length <= 200)
                {
                    message += $" - {responseBody}";
                }
            }
        }

        return message;
    }
}
