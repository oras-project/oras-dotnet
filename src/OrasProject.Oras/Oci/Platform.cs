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
using System.Text.Json.Serialization;

namespace OrasProject.Oras.Oci;

/// <summary>
/// Platform describes the platform which the image in the manifest runs on.
/// This should only be used when referring to a manifest.
/// Specification: https://github.com/opencontainers/image-spec/blob/v1.1.0/image-index.md#platform-object
/// </summary>
public class Platform
{
    [JsonPropertyName("architecture")]
    public required string Architecture { get; set; }

    [JsonPropertyName("os")]
    public required string Os { get; set; }

    [JsonPropertyName("os.version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? OsVersion { get; set; }

    [JsonPropertyName("os.features")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IList<string>? OsFeatures { get; set; }

    [JsonPropertyName("variant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Variant { get; set; }
}
