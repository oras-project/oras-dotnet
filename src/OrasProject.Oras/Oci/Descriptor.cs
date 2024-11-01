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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;

namespace OrasProject.Oras.Oci;

public class Descriptor
{
    public Descriptor()
    {
    }

    [SetsRequiredMembers]
    public Descriptor(string mediaType, string digest) =>
        (MediaType, Digest) = (mediaType, digest);

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

    public static Descriptor Empty => new Descriptor
    {
        MediaType = OrasProject.Oras.Oci.MediaType.EmptyJson,
        Digest = "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
        Size = 2,
        Data = Encoding.UTF8.GetBytes("{}")
    };

    internal BasicDescriptor BasicDescriptor => new BasicDescriptor(MediaType, Digest, Size);
}
