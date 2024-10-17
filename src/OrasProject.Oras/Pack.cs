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

using OrasProject.Oras.Oci;
using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras;

public static class Pack 
{
    // MediaTypeUnknownConfig is the default config mediaType used
	//   - for [Pack] when PackOptions.PackImageManifest is true and
	//     PackOptions.ConfigDescriptor is not specified.
	//   - for [PackManifest] when packManifestVersion is PackManifestVersion1_0
	//     and PackManifestOptions.ConfigDescriptor is not specified.
    public const string MediaTypeUnknownConfig = "application/vnd.unknown.config.v1+json";
    // MediaTypeUnknownArtifact is the default artifactType used for [Pack]
	// when PackOptions.PackImageManifest is false and artifactType is
	// not specified.
	public const string MediaTypeUnknownArtifact = "application/vnd.unknown.artifact.v1";
    // ErrInvalidDateTimeFormat is returned by [Pack] and [PackManifest] when
	// "org.opencontainers.artifact.created" or "org.opencontainers.image.created"
	// is provided, but its value is not in RFC 3339 format.
	// Reference: https://www.rfc-editor.org/rfc/rfc3339#section-5.6
    public const string ErrInvalidDateTimeFormat = "invalid date and time format";
    // ErrMissingArtifactType is returned by [PackManifest] when
	// packManifestVersion is PackManifestVersion1_1 and artifactType is
	// empty and the config media type is set to
	// "application/vnd.oci.empty.v1+json".
    public const string ErrMissingArtifactType = "missing artifact type";

    // PackManifestVersion represents the manifest version used for [PackManifest].
    public enum PackManifestVersion
    {
        // PackManifestVersion1_0 represents the OCI Image Manifest defined in
        // image-spec v1.0.2.
        // Reference: https://github.com/opencontainers/image-spec/blob/v1.0.2/manifest.md
        PackManifestVersion1_0 = 1,
        // PackManifestVersion1_1 represents the OCI Image Manifest defined in
        // image-spec v1.1.0.
        // Reference: https://github.com/opencontainers/image-spec/blob/v1.1.0/manifest.md
        PackManifestVersion1_1 = 2
    }

    public struct PackManifestOptions
    {
        // MediaType SHOULD be used and this field MUST contain the media type 
        // "application/vnd.oci.image.manifest.v1+json"
        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; }

        // Config is references a configuration object for a container, by digest
        // It is a REQUIRED property
        // Following additional restrictions:
        // if this media type is unknown, consider the referenced content as arbitrary binary data, and MUST NOT attempt to parse the referenced content
	    // if this media type is unknown, storing or copying image manifests MUST NOT error 
        // MUST support at least the following media types: "application/vnd.oci.image.config.v1+json"
        // For artifact, config.mediaType value MUST be set to a value specific to the artifact type or the empty value. 
	    // If the config.mediaType is set to the empty value, the artifactType MUST be defined
        [JsonPropertyName("config")]
        public Descriptor Config { get; set; }

        // Layers is the layers of the manifest
        // Each item in the array MUST be a descriptor
        // layers SHOULD have at least one entry
        // if config.mediaType is set to application/vnd.oci.image.config.v1+json, following restrictions:
        // The array MUST have the base layer at index 0
        // Subsequent layers MUST then follow in stack order (i.e. from layers[0] to layers[len(layers)-1])
        // The final filesystem layout MUST match the result of applying the layers to an empty directory
        // The ownership, mode, and other attributes of the initial empty directory are unspecified
        // mediaType string restrictions: MUST support at least the following media types
        // application/vnd.oci.image.layer.v1.tar
        // application/vnd.oci.image.layer.v1.tar+gzip
        // application/vnd.oci.image.layer.nondistributable.v1.tar
        // application/vnd.oci.image.layer.nondistributable.v1.tar+gzip
        // Implementations storing or copying image manifests MUST NOT error on encountering a mediaType that is unknown to the implementation
        [JsonPropertyName("layers")]
        public List<Descriptor> Layers { get; set; }

        // Subject is the subject of the manifest.
        // This option is only valid when PackManifestVersion is
        // NOT PackManifestVersion1_0.
        [JsonPropertyName("subject")]
        public Descriptor Subject { get; set; }

        // ManifestAnnotations is OPTIONAL property contains arbitrary metadata for the image manifest
        // MUST use the annotation rules
        [JsonPropertyName("manifestAnnotations")]
        public IDictionary<string, string> ManifestAnnotations { get; set; }

        // ConfigAnnotations is the annotation map of the config descriptor.
	    // This option is valid only when Config is null.
        [JsonPropertyName("configAnnotations")]
        public IDictionary<string, string> ConfigAnnotations { get; set; }
    }

    // mediaTypeRegexp checks the format of media types.
    // References:
    //   - https://github.com/opencontainers/image-spec/blob/v1.1.0/schema/defs-descriptor.json#L7
    //   - https://datatracker.ietf.org/doc/html/rfc6838#section-4.2
    // ^[A-Za-z0-9][A-Za-z0-9!#$&-^_.+]{0,126}/[A-Za-z0-9][A-Za-z0-9!#$&-^_.+]{0,126}$
    private const string _mediaTypeRegexp = @"^[A-Za-z0-9][A-Za-z0-9!#$&-^_.+]{0,126}/[A-Za-z0-9][A-Za-z0-9!#$&-^_.+]{0,126}$";
    //private static readonly Regex _mediaTypeRegex = new Regex(_mediaTypeRegexp, RegexOptions.Compiled);
    private static readonly Regex _mediaTypeRegex = new Regex(@"^[A-Za-z0-9][A-Za-z0-9!#$&-^_.+]{0,126}/[A-Za-z0-9][A-Za-z0-9!#$&-^_.+]{0,126}(\+json)?$", RegexOptions.Compiled);


    // PackManifest generates an OCI Image Manifestbased on the given parameters
    // and pushes the packed manifest to a content storage/registry using pusher. The version
    // of the manifest to be packed is determined by packManifestVersion
    // (Recommended value: PackManifestVersion1_1).
    //
    //   - If packManifestVersion is [PackManifestVersion1_1]:
    //     artifactType MUST NOT be empty unless PackManifestOptions.ConfigDescriptor is specified.
    //   - If packManifestVersion is [PackManifestVersion1_0]:
    //     if PackManifestOptions.ConfigDescriptor is null, artifactType will be used as the
    //     config media type; if artifactType is empty,
    //     "application/vnd.unknown.config.v1+json" will be used.
    //     if PackManifestOptions.ConfigDescriptor is NOT null, artifactType will be ignored.
    //
    // artifactType and PackManifestOptions.ConfigDescriptor.MediaType MUST comply with RFC 6838.
    //
    // Each time when PackManifest is called, if a time stamp is not specified, a new time
    // stamp is generated in the manifest annotations with the key ocispec.AnnotationCreated
    // (i.e. "org.opencontainers.image.created"). To make [PackManifest] reproducible,
    // set the key ocispec.AnnotationCreated to a fixed value in
    // opts.Annotations. The value MUST conform to RFC 3339.
    //
    // If succeeded, returns a descriptor of the packed manifest.
    //
    // Note: PackManifest can also pack artifact other than OCI image, but the config.mediaType value 
    // should not be a known OCI image config media type [PackManifestVersion1_1]
    public static async Task<Descriptor> PackManifest(
            ITarget pusher,
            PackManifestVersion version,
            string? artifactType,
            PackManifestOptions options,
            CancellationToken cancellationToken = default)
    {
        switch (version)
        {
            case PackManifestVersion.PackManifestVersion1_0:
                return await PackManifestV1_0(pusher, artifactType, options, cancellationToken);
            case PackManifestVersion.PackManifestVersion1_1:
                return await PackManifestV1_1(pusher, artifactType, options, cancellationToken);
            default:
                throw new NotSupportedException($"PackManifestVersion({version}) is not supported");
        }
    }

    private static async Task<Descriptor> PackManifestV1_0(ITarget pusher, string? artifactType, PackManifestOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Subject != null)
        {
            throw new NotSupportedException("Subject is not supported for manifest version 1.0.");
        }

        Descriptor configDescriptor;

        if (options.Config != null)
        {
            ValidateMediaType(options.Config.MediaType);
            configDescriptor = options.Config;
        }
        else
        {
            if (string.IsNullOrEmpty(artifactType))
            {
                artifactType = MediaTypeUnknownConfig;
            }
            ValidateMediaType(artifactType);
            configDescriptor = await PushCustomEmptyConfig(pusher, artifactType, options.ConfigAnnotations, cancellationToken);
        }

        var annotations = EnsureAnnotationCreated(options.ManifestAnnotations, "org.opencontainers.image.created");
        var manifest = new Manifest
        {
            SchemaVersion = 2,
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Config = configDescriptor,
            Layers = options.Layers ?? new List<Descriptor>(),
            Annotations = annotations
        };

        return await PushManifest(pusher, manifest, manifest.MediaType, manifest.Config.MediaType, manifest.Annotations, cancellationToken);
    }

    private static async Task<Descriptor> PackManifestV1_1(ITarget pusher, string? artifactType, PackManifestOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(artifactType) && (options.Config == null || options.Config.MediaType == "application/vnd.oci.empty.v1+json"))
        {
            throw new MissingArtifactTypeException(ErrMissingArtifactType);
        } else if (!string.IsNullOrEmpty(artifactType)) {
            ValidateMediaType(artifactType);
		}

        Descriptor configDescriptor;

        if (options.Config != null)
        {
            ValidateMediaType(options.Config.MediaType);
            configDescriptor = options.Config;
        }
        else
        {
            var expectedConfigBytes = Encoding.UTF8.GetBytes("{}");
            configDescriptor = new Descriptor
            {
                MediaType = "application/vnd.oci.empty.v1+json",
                Digest = Digest.ComputeSHA256(expectedConfigBytes),
                Size = expectedConfigBytes.Length,
                Data = expectedConfigBytes
            };
            options.Config = configDescriptor;
            var configBytes = JsonSerializer.SerializeToUtf8Bytes(new { });
            await PushIfNotExist(pusher, configDescriptor, configBytes, cancellationToken);
        }

        if (options.Layers == null || options.Layers.Count == 0)
        {
            options.Layers ??= new List<Descriptor>();
            // use the empty descriptor as the single layer
            var expectedConfigBytes = Encoding.UTF8.GetBytes("{}");
            var emptyLayer = new Descriptor { 
                MediaType = "application/vnd.oci.empty.v1+json",
                Digest = Digest.ComputeSHA256(expectedConfigBytes),
                Data = expectedConfigBytes,
                Size = expectedConfigBytes.Length
            };
            options.Layers.Add(emptyLayer);
        }

        var annotations = EnsureAnnotationCreated(options.ManifestAnnotations, "org.opencontainers.image.created");

        var manifest = new Manifest
        {
            SchemaVersion = 2,
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            ArtifactType = artifactType,
            Subject = options.Subject,
            Config = options.Config,
            Layers = options.Layers,
            Annotations = annotations
        };

        return await PushManifest(pusher, manifest, manifest.MediaType, manifest.ArtifactType, manifest.Annotations, cancellationToken);
    }

    private static async Task<Descriptor> PushManifest(ITarget pusher, object manifest, string mediaType, string? artifactType, IDictionary<string, string>? annotations, CancellationToken cancellationToken = default)
    {
        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var manifestDesc = new Descriptor {
            MediaType = mediaType, 
            Digest = Digest.ComputeSHA256(manifestJson),
            Size = manifestJson.Length
        };
        manifestDesc.ArtifactType = artifactType;
        manifestDesc.Annotations = annotations;

        await pusher.PushAsync(manifestDesc, new MemoryStream(manifestJson), cancellationToken);
        return manifestDesc;
    }

    private static void ValidateMediaType(string mediaType)
    {
        if (!_mediaTypeRegex.IsMatch(mediaType))
        {
            throw new InvalidMediaTypeException($"{mediaType} is an invalid media type");
        }
    }

private static async Task<Descriptor> PushCustomEmptyConfig(ITarget pusher, string mediaType, IDictionary<string, string> annotations, CancellationToken cancellationToken = default)
    {
        var configBytes = JsonSerializer.SerializeToUtf8Bytes(new { });
        var configDescriptor = new Descriptor
        {
            MediaType = mediaType, 
            Digest = Digest.ComputeSHA256(configBytes),
            Size = configBytes.Length
        };
        configDescriptor.Annotations = annotations;

        await PushIfNotExist(pusher, configDescriptor, configBytes, cancellationToken);
        return configDescriptor;
    }

    private static async Task PushIfNotExist(ITarget pusher, Descriptor descriptor, byte[] data, CancellationToken cancellationToken = default)
    {
        await pusher.PushAsync(descriptor, new MemoryStream(data), cancellationToken);
    }

    private static IDictionary<string, string>? EnsureAnnotationCreated(IDictionary<string, string> annotations, string key)
    {
        if (annotations is null)
        {
            annotations = new Dictionary<string, string>();
        }
        if (annotations.ContainsKey(key))
        {
            if (!DateTime.TryParse(annotations[key], out _))
            {
                throw new InvalidDateTimeFormatException(ErrInvalidDateTimeFormat);
            }

            return annotations;
        }

        var copiedAnnotations = new Dictionary<string, string>(annotations);
        copiedAnnotations[key] = DateTime.UtcNow.ToString("o");

        return copiedAnnotations;
    }
}