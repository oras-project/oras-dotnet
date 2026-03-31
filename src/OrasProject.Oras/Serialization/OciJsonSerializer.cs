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
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;

namespace OrasProject.Oras.Serialization;

/// <summary>
/// OciJsonSerializer provides JSON serialization for OCI content
/// with Go-compatible encoding (literal '+' instead of '\u002B').
/// </summary>
internal static class OciJsonSerializer
{
    private static readonly JsonSerializerOptions s_options = CreateOptions();

    /// <summary>
    /// Serializes the value to a UTF-8 JSON byte array with
    /// Go-compatible string escaping. Throws if the result exceeds
    /// <see cref="OciLimits.MaxManifestBytes"/>.
    /// </summary>
    /// <remarks>
    /// The full byte array is materialized before the size check.
    /// This is acceptable because OCI manifests are bounded in
    /// practice, but callers constructing adversarially large
    /// objects will allocate before the guard fires.
    /// </remarks>
    internal static byte[] SerializeToUtf8Bytes<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, s_options);
        if (bytes.Length > OciLimits.MaxManifestBytes)
        {
            throw new SizeLimitExceededException(
                $"Serialized size {bytes.Length} bytes exceeds"
                + $" limit of {OciLimits.MaxManifestBytes} bytes.");
        }
        return bytes;
    }

    /// <summary>
    /// Deserializes a UTF-8 JSON byte array to the specified type.
    /// </summary>
    internal static T? Deserialize<T>(byte[] utf8Json)
    {
        return JsonSerializer.Deserialize<T>(utf8Json, s_options);
    }

    /// <summary>
    /// Deserializes a JSON string to the specified type.
    /// </summary>
    internal static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, s_options);
    }

    /// <summary>
    /// Deserializes a UTF-8 JSON stream to the specified type.
    /// </summary>
    internal static async Task<T?> DeserializeAsync<T>(
        Stream utf8Json,
        CancellationToken cancellationToken)
    {
        return await JsonSerializer.DeserializeAsync<T>(
            utf8Json, s_options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Formats a JsonElement as a JSON string for error display.
    /// </summary>
    internal static string FormatErrorDetail(JsonElement detail)
    {
        return JsonSerializer.Serialize(detail, s_options);
    }

    /// <summary>
    /// Returns true if the string contains any character that
    /// <see cref="EscapeJsonString"/> would escape. Used to skip the
    /// full escape pass for the common case of simple ASCII strings
    /// (digests, media types, tags).
    /// </summary>
    internal static bool NeedsEscaping(string value)
    {
        foreach (var ch in value)
        {
            if (ch <= '\u001F' || ch == '"' || ch == '\\' ||
                ch == '<' || ch == '>' || ch == '&' ||
                ch == '\u2028' || ch == '\u2029')
            {
                return true;
            }
        }
        return false;
    }

    private static ReadOnlySpan<byte> HexDigits =>
        "0123456789abcdef"u8;

    /// <summary>
    /// Escapes a JSON string value.
    /// Escapes: ", \, control chars, &lt;, &gt;, &amp;, U+2028, U+2029.
    /// Does NOT escape '+', matching Go's encoding/json.Marshal for
    /// cross-runtime content-addressable storage compatibility.
    /// </summary>
    internal static string EscapeJsonString(string value)
    {
        // Fast path: if no chars need escaping, return as-is.
        if (!NeedsEscaping(value))
        {
            return value;
        }

        // Worst case: every char becomes 6 chars (\uXXXX).
        var maxLen = value.Length * 6;
        var pooled = ArrayPool<char>.Shared.Rent(maxLen);
        try
        {
            var pos = 0;
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"':
                        pooled[pos++] = '\\';
                        pooled[pos++] = '"';
                        break;
                    case '\\':
                        pooled[pos++] = '\\';
                        pooled[pos++] = '\\';
                        break;
                    case '\b':
                        pooled[pos++] = '\\';
                        pooled[pos++] = 'b';
                        break;
                    case '\f':
                        pooled[pos++] = '\\';
                        pooled[pos++] = 'f';
                        break;
                    case '\n':
                        pooled[pos++] = '\\';
                        pooled[pos++] = 'n';
                        break;
                    case '\r':
                        pooled[pos++] = '\\';
                        pooled[pos++] = 'r';
                        break;
                    case '\t':
                        pooled[pos++] = '\\';
                        pooled[pos++] = 't';
                        break;
                    case '<':
                        WriteHexEscape(pooled, ref pos, ch);
                        break;
                    case '>':
                        WriteHexEscape(pooled, ref pos, ch);
                        break;
                    case '&':
                        WriteHexEscape(pooled, ref pos, ch);
                        break;
                    case '\u2028':
                        WriteHexEscape(pooled, ref pos, ch);
                        break;
                    case '\u2029':
                        WriteHexEscape(pooled, ref pos, ch);
                        break;
                    default:
                        if (ch <= '\u001F')
                        {
                            WriteHexEscape(pooled, ref pos, ch);
                        }
                        else
                        {
                            pooled[pos++] = ch;
                        }
                        break;
                }
            }
            return new string(pooled, 0, pos);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(pooled);
        }
    }

    /// <summary>
    /// Writes a \uXXXX hex escape sequence into the buffer at
    /// the current position.
    /// </summary>
    private static void WriteHexEscape(
        char[] buffer, ref int pos, char ch)
    {
        var hex = HexDigits;
        buffer[pos++] = '\\';
        buffer[pos++] = 'u';
        buffer[pos++] = (char)hex[(ch >> 12) & 0xF];
        buffer[pos++] = (char)hex[(ch >> 8) & 0xF];
        buffer[pos++] = (char)hex[(ch >> 4) & 0xF];
        buffer[pos++] = (char)hex[ch & 0xF];
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new OciStringConverter());
        options.Converters.Add(new OciDictionaryConverter());
        return options;
    }
}
