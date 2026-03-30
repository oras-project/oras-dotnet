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
using OrasProject.Oras.Oci;
using OrasProject.Oras.Serialization;
using Xunit;

namespace OrasProject.Oras.Tests.Serialization;

public partial class ManifestSerializationTest
{
    #region OCI Manifest JSON Constants

    /// <summary>
    /// OCI image-spec v1.1.1 manifest.md example image manifest.
    /// </summary>
    private const string SpecImageManifestJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.manifest.v1+json",
            "config": {
                "mediaType": "application/vnd.oci.image.config.v1+json",
                "digest": "sha256:b5b2b2c507a0944348e0303114d8d93aaaa081732b86451d9bce1f432a537bc7",
                "size": 7023
            },
            "layers": [
                {
                    "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
                    "digest": "sha256:9834876dcfb05cb167a5c24953eba58c4ac89b1adf57f28f2f9d09af107ee8f0",
                    "size": 32654
                },
                {
                    "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
                    "digest": "sha256:3c3a4604a545cdc127456d94e421cd355bca5b528f4a9c1905b15da2eb4a4c6b",
                    "size": 16724
                },
                {
                    "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
                    "digest": "sha256:ec4b8955958665577945c89419d1af06b5f7636b4ac3da7f12184802ad867736",
                    "size": 73109
                }
            ],
            "annotations": {
                "com.example.key1": "value1",
                "com.example.key2": "value2"
            }
        }
        """;

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

    #endregion

    #region OCI Manifest Tests

    [Theory]
    [MemberData(nameof(OciManifestFieldFixtures))]
    public void Deserialize_OciManifest_PreservesAllFields(
        string json,
        string expectedMediaType,
        string? expectedArtifactType,
        int expectedLayerCount,
        bool hasSubject,
        bool hasAnnotations)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var m =
            OciJsonSerializer.Deserialize<Manifest>(bytes)!;

        Assert.Equal(2, m.SchemaVersion);
        Assert.Equal(expectedMediaType, m.MediaType);
        Assert.Equal(expectedArtifactType, m.ArtifactType);
        Assert.Equal(expectedLayerCount, m.Layers.Count);

        if (hasSubject)
            Assert.NotNull(m.Subject);
        else
            Assert.Null(m.Subject);

        if (hasAnnotations)
        {
            Assert.NotNull(m.Annotations);
            Assert.NotEmpty(m.Annotations!);
        }
    }

    [Theory]
    [MemberData(nameof(OciAnnotationFixtures))]
    public void Deserialize_OciManifest_AnnotationsPreserved(
        string json,
        string expectedKey,
        string expectedValue)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var m =
            OciJsonSerializer.Deserialize<Manifest>(bytes)!;

        Assert.NotNull(m.Annotations);
        Assert.True(
            m.Annotations!.ContainsKey(expectedKey),
            $"Missing annotation key: {expectedKey}");
        Assert.Equal(
            expectedValue, m.Annotations[expectedKey]);
    }

    [Fact]
    public void Deserialize_SpecImageManifest_FieldAssertions()
    {
        var bytes =
            Encoding.UTF8.GetBytes(SpecImageManifestJson);
        var m =
            OciJsonSerializer.Deserialize<Manifest>(bytes)!;

        Assert.Equal(
            MediaType.ImageConfig, m.Config.MediaType);
        Assert.Equal(3, m.Layers.Count);
        Assert.NotNull(m.Annotations);
        Assert.True(
            m.Annotations!.ContainsKey("com.example.key1"));
        Assert.True(
            m.Annotations!.ContainsKey("com.example.key2"));
    }

    [Fact]
    public void Serialize_OciManifest_NullOptionals_AbsentInOutput()
    {
        var manifest = new Manifest
        {
            SchemaVersion = 2,
            MediaType = MediaType.ImageManifest,
            Config = Descriptor.Empty,
            Layers = new List<Descriptor>()
        };

        var json = Encoding.UTF8.GetString(
            OciJsonSerializer.SerializeToUtf8Bytes(manifest));

        Assert.DoesNotContain("\"subject\"", json);
        Assert.DoesNotContain("\"annotations\"", json);
        Assert.DoesNotContain("\"artifactType\"", json);
    }

    [Fact]
    public void Deserialize_MinimalOciManifest_NoException()
    {
        var minimalJson = """
            {
                "schemaVersion": 2,
                "config": {
                    "mediaType": "application/vnd.oci.empty.v1+json",
                    "digest": "sha256:44136fa355b311bfa706c319d8f39c36",
                    "size": 2
                },
                "layers": []
            }
            """;
        var bytes = Encoding.UTF8.GetBytes(minimalJson);
        var m =
            OciJsonSerializer.Deserialize<Manifest>(bytes)!;

        Assert.NotNull(m);
        Assert.Null(m.MediaType);
        Assert.Null(m.ArtifactType);
        Assert.Null(m.Subject);
        Assert.Null(m.Annotations);
    }

    #endregion

    #region OCI Manifest Fixture Providers

    public static IEnumerable<object[]>
        OciManifestFieldFixtures()
    {
        yield return new object[]
        {
            MinimalManifestJson,
            "application/vnd.oci.image.manifest.v1+json",
            null!, 1, false, false
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
            SpecImageManifestJson,
            "application/vnd.oci.image.manifest.v1+json",
            null!, 3, false, true
        };
    }

    public static IEnumerable<object[]>
        OciAnnotationFixtures()
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

    #endregion
}
