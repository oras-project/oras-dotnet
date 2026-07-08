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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrasProject.Oras.Serialization;

/// <summary>
/// OciDictionaryConverter serializes IDictionary&lt;string, string&gt;
/// with Go-compatible escaping for both keys and values.
/// JsonConverter&lt;string&gt; is not called for dictionary keys,
/// so this converter handles the full dictionary serialization.
/// </summary>
internal sealed class OciDictionaryConverter
    : JsonConverter<IDictionary<string, string>>
{
    // Token ordering (PropertyName after StartObject, no truncation)
    // is enforced by Utf8JsonReader before this method is called.
    // Only the value type check is needed — annotations must be strings.
    public override IDictionary<string, string>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var dict = new Dictionary<string, string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dict;
            }

            var key = reader.GetString()!;
            reader.Read();

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException(
                    $"Expected string value, got {reader.TokenType}.");
            }

            dict[key] = reader.GetString()!;
        }

        throw new JsonException("Unexpected end of JSON.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        IDictionary<string, string> value,
        JsonSerializerOptions options)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        var sorted = value.ToList();
        SortByUtf8Key(sorted);
        foreach (var kvp in sorted)
        {
            if (!first)
            {
                sb.Append(',');
            }
            first = false;
            sb.Append('"');
            sb.Append(OciJsonSerializer.NeedsEscaping(kvp.Key)
                ? OciJsonSerializer.EscapeJsonString(kvp.Key)
                : kvp.Key);
            sb.Append("\":\"");
            sb.Append(
                OciJsonSerializer.NeedsEscaping(kvp.Value)
                ? OciJsonSerializer.EscapeJsonString(
                    kvp.Value)
                : kvp.Value);
            sb.Append('"');
        }
        sb.Append('}');
        writer.WriteRawValue(sb.ToString());
    }

    /// <summary>
    /// Sorts key-value pairs by UTF-8 byte order to match
    /// Go's encoding/json.Marshal (bytewise over UTF-8).
    /// Fast path: when all keys are ASCII, ordinal comparison
    /// is equivalent (zero extra allocations).
    /// Slow path: pre-encodes all keys once into a single
    /// pooled buffer, then sorts via index indirection —
    /// O(N) encoding instead of O(N log N).
    /// </summary>
    private static void SortByUtf8Key(
        List<KeyValuePair<string, string>> items)
    {
        if (items.Count <= 1) return;

        if (AllKeysAscii(items))
        {
            // ASCII: UTF-16 ordinal == UTF-8 bytewise
            // when all code points are < 0x80.
            items.Sort(static (a, b) =>
                string.CompareOrdinal(a.Key, b.Key));
            return;
        }

        SortByPreEncodedUtf8Key(items);
    }

    private static bool AllKeysAscii(
        List<KeyValuePair<string, string>> items)
    {
        foreach (var kvp in items)
        {
            foreach (var c in kvp.Key)
            {
                if (c >= 0x80) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Pre-encodes all keys into a single ArrayPool buffer,
    /// sorts an index array by UTF-8 byte comparison, then
    /// reorders items to match.
    /// </summary>
    private static void SortByPreEncodedUtf8Key(
        List<KeyValuePair<string, string>> items)
    {
        var count = items.Count;
        var offsets = new int[count];
        var lengths = new int[count];
        var totalBytes = 0;
        for (var i = 0; i < count; i++)
        {
            offsets[i] = totalBytes;
            lengths[i] = Encoding.UTF8
                .GetByteCount(items[i].Key);
            totalBytes += lengths[i];
        }

        var buffer = ArrayPool<byte>.Shared
            .Rent(Math.Max(totalBytes, 1));
        try
        {
            for (var i = 0; i < count; i++)
            {
                Encoding.UTF8.GetBytes(
                    items[i].Key,
                    buffer.AsSpan(
                        offsets[i], lengths[i]));
            }

            var indices = new int[count];
            for (var i = 0; i < count; i++)
                indices[i] = i;

            Array.Sort(indices, (x, y) =>
                buffer
                    .AsSpan(offsets[x], lengths[x])
                    .SequenceCompareTo(
                        buffer.AsSpan(
                            offsets[y],
                            lengths[y])));

            var temp =
                new KeyValuePair<string, string>[count];
            for (var i = 0; i < count; i++)
                temp[i] = items[indices[i]];
            for (var i = 0; i < count; i++)
                items[i] = temp[i];
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
