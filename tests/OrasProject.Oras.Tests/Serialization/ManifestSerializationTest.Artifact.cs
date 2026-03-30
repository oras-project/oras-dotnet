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
    #region Artifact JSON Constants

    private const string MinimalArtifactJson = """
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
            ],
            "artifactType": "application/vnd.example.sbom.v1"
        }
        """;

    private const string ArtifactWithConfigJson = """
        {
            "schemaVersion": 2,
            "mediaType": "application/vnd.oci.image.manifest.v1+json",
            "config": {
                "mediaType": "application/vnd.example.config.v1+json",
                "digest": "sha256:aaa111bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333",
                "size": 512
            },
            "layers": [
                {
                    "mediaType": "application/vnd.example.data.v1.tar+gzip",
                    "digest": "sha256:bbb222ccc333ddd444eee555fff666aaa111bbb222ccc333ddd444",
                    "size": 1024
                }
            ],
            "subject": {
                "mediaType": "application/vnd.oci.image.manifest.v1+json",
                "digest": "sha256:ccc333ddd444eee555fff666aaa111bbb222ccc333ddd444eee555",
                "size": 2048
            },
            "artifactType": "application/vnd.example.sbom.v1"
        }
        """;

    private const string ArtifactPlusSignJson = """
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
            ],
            "artifactType": "application/vnd.example+sbom.v1+json",
            "annotations": {
                "org.example+key/ann+1": "value+with+plus"
            }
        }
        """;

    #endregion

    #region Artifact Tests

    [Fact]
    public void Deserialize_MinimalArtifact_FieldAssertions()
    {
        var bytes =
            Encoding.UTF8.GetBytes(MinimalArtifactJson);
        var m =
            OciJsonSerializer.Deserialize<Manifest>(bytes)!;

        Assert.Equal(
            "application/vnd.example.sbom.v1",
            m.ArtifactType);
        Assert.Equal(MediaType.EmptyJson, m.Config.MediaType);
        Assert.Single(m.Layers);
        Assert.Null(m.Subject);
        Assert.Null(m.Annotations);
    }

    [Fact]
    public void Deserialize_ArtifactWithConfig_SubjectPresent()
    {
        var bytes =
            Encoding.UTF8.GetBytes(ArtifactWithConfigJson);
        var m =
            OciJsonSerializer.Deserialize<Manifest>(bytes)!;

        Assert.NotNull(m.Subject);
        Assert.StartsWith("sha256:", m.Subject!.Digest);
        Assert.Equal(
            "application/vnd.example.sbom.v1",
            m.ArtifactType);
        Assert.Equal(
            "application/vnd.example.config.v1+json",
            m.Config.MediaType);
    }

    [Fact]
    public void Serialize_ManifestWithSubject_SubjectInJson()
    {
        var manifest = new Manifest
        {
            SchemaVersion = 2,
            MediaType = MediaType.ImageManifest,
            Config = Descriptor.Empty,
            Layers = new List<Descriptor>(),
            Subject = new Descriptor
            {
                MediaType = MediaType.ImageManifest,
                Digest = "sha256:aaa111bbb222ccc333",
                Size = 100
            }
        };

        var bytes =
            OciJsonSerializer.SerializeToUtf8Bytes(manifest);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"subject\"", json);
    }

    [Fact]
    public void Serialize_ArtifactPlusSign_NoEscaping()
    {
        var bytes =
            Encoding.UTF8.GetBytes(ArtifactPlusSignJson);
        var m =
            OciJsonSerializer.Deserialize<Manifest>(bytes)!;
        var reserialized =
            OciJsonSerializer.SerializeToUtf8Bytes(m);
        var output = Encoding.UTF8.GetString(reserialized);

        Assert.DoesNotContain("\\u002B", output);
        Assert.Contains(
            "application/vnd.example+sbom.v1+json", output);
    }

    #endregion
}
