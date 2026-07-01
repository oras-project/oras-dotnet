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
using System.Text.Json.Serialization;
using OrasProject.Oras.Exceptions;

namespace OrasProject.Oras.Oci;

/// <summary>
/// Descriptor describes a content addressable blob.
/// Specification: https://github.com/opencontainers/image-spec/blob/v1.1.1/descriptor.md
/// </summary>
public class Descriptor
{
    // Validation error constants for programmatic consumption.
    internal const string ErrMediaTypeEmpty = "Invalid descriptor. The 'mediaType' property must not be empty.";
    internal const string ErrSizeNegative = "Invalid descriptor. The 'size' property must be non-negative.";

    [JsonPropertyName("mediaType")]
    public required string MediaType { get; set; }

    [JsonPropertyName("digest")]
    public required string Digest { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("urls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IList<string>? Urls { get; set; }

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
            Digest = Content.Digest.ComputeSha256(byteData),
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

    internal static bool IsNullOrInvalid(Descriptor? descriptor)
    {
        return descriptor == null || string.IsNullOrWhiteSpace(descriptor.Digest) || string.IsNullOrWhiteSpace(descriptor.MediaType) || descriptor.Size < 0;
    }

    /// <summary>
    /// Validates the descriptor per OCI image-spec v1.1.1 requirements.
    /// Checks mediaType (non-empty), size (non-negative), and digest format.
    /// </summary>
    /// <param name="error">When this method returns false, contains the validation error message.</param>
    /// <returns>true if the descriptor is valid; otherwise, false.</returns>
    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(MediaType))
        {
            error = ErrMediaTypeEmpty;
            return false;
        }

        if (Size < 0)
        {
            error = ErrSizeNegative;
            return false;
        }

        return Content.Digest.TryValidate(Digest, out error);
    }

    /// <summary>
    /// Validates the descriptor per OCI image-spec v1.1.1 requirements.
    /// Checks mediaType (non-empty), size (non-negative), and digest format.
    /// </summary>
    /// <remarks>
    /// In performance-sensitive code paths, prefer <see cref="TryValidate"/> to avoid
    /// the cost of exception allocation and stack unwinding on invalid input.
    /// </remarks>
    /// <exception cref="InvalidDescriptorException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (!TryValidate(out var error))
        {
            throw new InvalidDescriptorException(error);
        }
    }

    /// <summary>
    /// IsManifestType is to check if the given descriptor a manifest
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    internal static bool IsManifestType(Descriptor descriptor) =>
        descriptor.MediaType is
            Docker.MediaType.Manifest or
            Oci.MediaType.ImageManifest or
            Docker.MediaType.ManifestList or
            Oci.MediaType.ImageIndex;
}
