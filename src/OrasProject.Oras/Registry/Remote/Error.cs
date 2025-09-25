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

namespace OrasProject.Oras.Registry.Remote;

using System.Text.Json;
using System.Text.Json.Serialization;

public enum ErrorCode
{
    NAME_UNKNOWN
}

public class Error
{
    [JsonPropertyName("code")]
    public required string Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement? Detail { get; set; }

    /// <summary>
    /// Returns a formatted string representation of the error including code, message, and detail if available.
    /// </summary>
    /// <returns>A formatted error string.</returns>
    public override string ToString()
    {
        // Ensure Code and Message are not null for robust formatting
        string safeCode = Code ?? "UNKNOWN";
        string safeMessage = Message ?? "unknown message";

        // Start with the basic error information
        var result = $"{safeCode}: {safeMessage}";

        // Add detail information if available and valid
        if (Detail is { } detailValue &&
            detailValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            try
            {
                var detailJson = JsonSerializer.Serialize(detailValue, JsonSerializerOptions.Default);
                return $"{result} (Detail: {detailJson})";
            }
            catch
            {
                // If serialization fails, continue without detail
            }
        }

        return result;
    }
}
