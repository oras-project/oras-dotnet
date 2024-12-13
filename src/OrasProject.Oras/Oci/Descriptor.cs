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
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using OrasProject.Oras.Content;

namespace OrasProject.Oras.Oci;

public class Descriptor
{
    [JsonPropertyName("mediaType")]
    public required string MediaType { get; set; }

    [JsonPropertyName("digest")]
    public required string Digest { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("urls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IList<string>? URLs { get; set; }

    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IDictionary<string, string>? Annotations { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? Data { get; set; }

    [JsonPropertyName("platform")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Platform? Platform { get; set; }

    [JsonPropertyName("artifactType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ArtifactType { get; set; }

    public static Descriptor Create(Span<byte> data, string mediaType)
    {
        byte[] byteData = data.ToArray();
        return new Descriptor
        {
            MediaType = mediaType,
            Digest = Content.Digest.ComputeSHA256(byteData),
            Size = byteData.Length
        };
    }

    public static Descriptor Empty => new()
    {
        MediaType = Oci.MediaType.EmptyJson,
        Digest = "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
        Size = 2,
        Data = [0x7B, 0x7D]
    };

    internal BasicDescriptor BasicDescriptor => new BasicDescriptor(MediaType, Digest, Size);

    internal static bool IsEmptyOrInvalid(Descriptor? descriptor)
    {
        return descriptor == null || string.IsNullOrEmpty(descriptor.Digest) || string.IsNullOrEmpty(descriptor.MediaType);
    }
    
    internal static Descriptor ZeroDescriptor() => new ()
    {
        MediaType = "",
        Digest = "",
        Size = 0
    };
}
