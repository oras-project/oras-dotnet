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
using System.Text;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Serialization;
using Xunit;
using OciIndex = OrasProject.Oras.Oci.Index;

namespace OrasProject.Oras.Tests.Serialization;

public class ManifestSerializationTest
{
    /// <summary>
    /// Deserialize fully defined JSON manifest strings and verify
    /// all fields are preserved including plus signs.
    /// </summary>
    [Theory]
    [MemberData(nameof(JsonManifestFixtures))]
    public void Deserialize_Manifest_PreservesAllFields(
        string json,
        string expectedMediaType,
        string? expectedArtifactType,
        int expectedLayerCount,
        bool hasSubject,
        bool hasAnnotations)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var m = OciJsonSerializer.Deserialize<Manifest>(bytes)!;

        Assert.Equal(2, m.SchemaVersion);
        Assert.Equal(expectedMediaType, m.MediaType);
        Assert.Equal(expectedArtifactType, m.ArtifactType);
        Assert.Equal(expectedLayerCount, m.Layers.Count);

        if (hasSubject)
        {
            Assert.NotNull(m.Subject);
        }
        else
        {
            Assert.Null(m.Subject);
        }

        if (hasAnnotations)
        {
            Assert.NotNull(m.Annotations);
            Assert.NotEmpty(m.Annotations!);
        }
    }

    /// <summary>
    /// Verify annotations with '+' in both keys and values survive
    /// deserialization from raw JSON strings.
    /// </summary>
    [Theory]
    [MemberData(nameof(JsonAnnotationFixtures))]
    public void Deserialize_Manifest_AnnotationsPreserved(
        string json,
        string expectedKey,
        string expectedValue)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var m = OciJsonSerializer.Deserialize<Manifest>(bytes)!;

        Assert.NotNull(m.Annotations);
        Assert.True(
            m.Annotations!.ContainsKey(expectedKey),
            $"Missing annotation key: {expectedKey}");
        Assert.Equal(expectedValue, m.Annotations[expectedKey]);
    }

    /// <summary>
    /// Deserialize fully defined JSON index strings and verify
    /// manifest list and annotations are preserved.
    /// </summary>
    [Theory]
    [MemberData(nameof(JsonIndexFixtures))]
    public void Deserialize_Index_PreservesAllFields(
        string json,
        int expectedManifestCount,
        bool hasAnnotations)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var idx = OciJsonSerializer.Deserialize<OciIndex>(bytes)!;

        Assert.Equal(2, idx.SchemaVersion);
        Assert.Equal(expectedManifestCount, idx.Manifests.Count);

        if (hasAnnotations)
        {
            Assert.NotNull(idx.Annotations);
            Assert.NotEmpty(idx.Annotations!);
        }

        foreach (var desc in idx.Manifests)
        {
            Assert.False(string.IsNullOrEmpty(desc.MediaType));
            Assert.False(string.IsNullOrEmpty(desc.Digest));
            Assert.True(desc.Size > 0);
        }
    }

    /// <summary>
    /// Deserialize → re-serialize round-trip: output must contain
    /// no escaped '+' and be semantically equivalent to input.
    /// </summary>
    [Theory]
    [MemberData(nameof(JsonRoundTripFixtures))]
    public void RoundTrip_NoEscapedPlus_Equivalent(
        string json, bool isIndex)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        byte[] reserialized;
        if (isIndex)
        {
            var idx = OciJsonSerializer.Deserialize<OciIndex>(bytes)!;
            reserialized = OciJsonSerializer.SerializeToUtf8Bytes(idx);
        }
        else
        {
            var m = OciJsonSerializer.Deserialize<Manifest>(bytes)!;
            reserialized = OciJsonSerializer.SerializeToUtf8Bytes(m);
        }

        var output = Encoding.UTF8.GetString(reserialized);
        Assert.DoesNotContain("\\u002B", output);
        Assert.Contains("+", output);

        // Verify semantic equivalence via re-deserialization
        if (isIndex)
        {
            var orig = OciJsonSerializer.Deserialize<OciIndex>(bytes)!;
            var got = OciJsonSerializer.Deserialize<OciIndex>(reserialized)!;
            Assert.Equal(orig.Manifests.Count, got.Manifests.Count);
        }
        else
        {
            var orig = OciJsonSerializer.Deserialize<Manifest>(bytes)!;
            var got = OciJsonSerializer.Deserialize<Manifest>(reserialized)!;
            Assert.Equal(orig.MediaType, got.MediaType);
            Assert.Equal(orig.ArtifactType, got.ArtifactType);
            Assert.Equal(orig.Layers.Count, got.Layers.Count);
            Assert.Equal(
                orig.Subject?.Digest, got.Subject?.Digest);
            Assert.Equal(
                orig.Annotations?.Count,
                got.Annotations?.Count);
        }
    }

    [Fact]
    public void Serialize_ThrowsWhenExceedingMaxSize()
    {
        var m = new Manifest
        {
            SchemaVersion = 2,
            MediaType = MediaType.ImageManifest,
            Config = Descriptor.Empty,
            Layers = new List<Descriptor>(),
            Annotations = new Dictionary<string, string>()
        };
        var big = new string('x', 1024 * 1024);
        for (var i = 0; i < 5; i++)
        {
            m.Annotations[$"key{i}"] = big;
        }

        Assert.Throws<SizeLimitExceededException>(
            () => OciJsonSerializer.SerializeToUtf8Bytes(m));
    }

    #region Fixture Providers

    public static IEnumerable<object[]> JsonManifestFixtures()
    {
        yield return new object[]
        {
            MinimalManifestJson,
            "application/vnd.oci.image.manifest.v1+json",
            null!,
            1, false, false
        };
        yield return new object[]
        {
            FullManifestJson,
            "application/vnd.oci.image.manifest.v1+json",
            "application/vnd.example+type",
            2, true, true
        };
        yield return new object[]
        {
            DockerManifestJson,
            "application/vnd.docker.distribution.manifest.v2+json",
            null!,
            1, false, false
        };
    }

    public static IEnumerable<object[]> JsonAnnotationFixtures()
    {
        yield return new object[]
        {
            FullManifestJson,
            "org.example+custom/key+1",
            "value+with+plus"
        };
        yield return new object[]
        {
            FullManifestJson,
            "org.opencontainers.image.created",
            "2026-01-01T00:00:00Z"
        };
        yield return new object[]
        {
            LayerAnnotationManifestJson,
            "org.layer+anno/key",
            "layer+value"
        };
    }

    public static IEnumerable<object[]> JsonIndexFixtures()
    {
        yield return new object[]
            { MinimalIndexJson, 2, false };
        yield return new object[]
            { FullIndexJson, 2, true };
    }

    public static IEnumerable<object[]> JsonRoundTripFixtures()
    {
        yield return new object[]
            { MinimalManifestJson, false };
        yield return new object[]
            { FullManifestJson, false };
        yield return new object[]
            { DockerManifestJson, false };
        yield return new object[]
            { MinimalIndexJson, true };
        yield return new object[]
            { FullIndexJson, true };
    }

    #endregion

    #region JSON String Constants

    private const string MinimalManifestJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.manifest.v1+json",
            "config": {
                "mediaType": "application/vnd.oci.empty.v1+json",
                "digest": "sha256:44136fa355b311bfa706c319d8f39c36e47d288aca2e1cc38b1c",
                "size": 2
            },
            "layers": [
                {
                    "mediaType": "application/vnd.oci.empty.v1+json",
                    "digest": "sha256:44136fa355b311bfa706c319d8f39c36e47d288aca2e1cc38b1c",
                    "size": 2
                }
            ]
        }
        """;

    private const string FullManifestJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.manifest.v1+json",
            "artifactType": "application/vnd.example+type",
            "config": {
                "mediaType": "application/vnd.oci.empty.v1+json",
                "digest": "sha256:44136fa355b311bfa706c319d8f39c36e47d288aca2e1cc38b1c",
                "size": 2
            },
            "layers": [
                {
                    "mediaType": "application/vnd.oci.empty.v1+json",
                    "digest": "sha256:44136fa355b311bfa706c319d8f39c36e47d288aca2e1cc38b1c",
                    "size": 2
                },
                {
                    "mediaType": "application/vnd.custom+layer",
                    "digest": "sha256:aaa111bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333",
                    "size": 100
                }
            ],
            "subject": {
                "mediaType": "application/vnd.oci.image.manifest.v1+json",
                "digest": "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef",
                "size": 1234
            },
            "annotations": {
                "org.example+custom/key+1": "value+with+plus",
                "org.opencontainers.image.created": "2026-01-01T00:00:00Z"
            }
        }
        """;

    private const string DockerManifestJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
            "config": {
                "mediaType": "application/vnd.docker.container.image.v1+json",
                "digest": "sha256:aaa111bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333",
                "size": 1024
            },
            "layers": [
                {
                    "mediaType": "application/vnd.docker.image.rootfs.diff.tar.gzip",
                    "digest": "sha256:bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333ddd444",
                    "size": 2048
                }
            ]
        }
        """;

    private const string LayerAnnotationManifestJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.manifest.v1+json",
            "config": {
                "mediaType": "application/vnd.oci.empty.v1+json",
                "digest": "sha256:44136fa355b311bfa706c319d8f39c36e47d288aca2e1cc38b1c",
                "size": 2
            },
            "layers": [
                {
                    "mediaType": "application/vnd.custom+layer",
                    "digest": "sha256:aaa111bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333",
                    "size": 100,
                    "annotations": {
                        "org.layer+anno/key": "layer+value"
                    }
                }
            ],
            "annotations": {
                "org.layer+anno/key": "layer+value"
            }
        }
        """;

    private const string MinimalIndexJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.index.v1+json",
            "manifests": [
                {
                    "mediaType": "application/vnd.oci.image.manifest.v1+json",
                    "digest": "sha256:aaa111bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333",
                    "size": 500
                },
                {
                    "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
                    "digest": "sha256:bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333ddd444",
                    "size": 600
                }
            ]
        }
        """;

    private const string FullIndexJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.index.v1+json",
            "manifests": [
                {
                    "mediaType": "application/vnd.oci.image.manifest.v1+json",
                    "digest": "sha256:aaa111bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333",
                    "size": 500,
                    "platform": {
                        "architecture": "amd64",
                        "os": "linux"
                    }
                },
                {
                    "mediaType": "application/vnd.oci.image.manifest.v1+json",
                    "digest": "sha256:bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333ddd444",
                    "size": 600,
                    "platform": {
                        "architecture": "arm64",
                        "os": "linux",
                        "variant": "v8"
                    }
                }
            ],
            "annotations": {
                "org.opencontainers.image.ref.name": "latest",
                "org.example+index/key+1": "index+value"
            }
        }
        """;

    #endregion
}
