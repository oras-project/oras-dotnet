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

using System.Text;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Serialization;
using Xunit;

namespace OrasProject.Oras.Tests.Serialization;

public partial class ManifestSerializationTest
{
    #region Descriptor JSON Fixtures

    private const string DescriptorWithUrlsJson = """
        {
            "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
            "digest": "sha256:9834876dcfb05cb167a5c24953eba58c4ac89b1adf57f28f2f9d09af107ee8f0",
            "size": 32654,
            "urls": [
                "https://example.com/layer1.tar.gz",
                "https://mirror.example.com/layer1.tar.gz"
            ]
        }
        """;

    private const string DescriptorWithDataJson = """
        {
            "mediaType": "application/vnd.oci.empty.v1+json",
            "digest": "sha256:44136fa355b311bfa706c319d8f39c36e47d288aca2e1cc38b1c",
            "size": 2,
            "data": "e30="
        }
        """;

    private const string DescriptorAllFieldsJson = """
        {
            "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
            "digest": "sha256:9834876dcfb05cb167a5c24953eba58c4ac89b1adf57f28f2f9d09af107ee8f0",
            "size": 32654,
            "urls": [
                "https://example.com/layer1.tar.gz"
            ],
            "annotations": {
                "org.opencontainers.image.title": "layer1.tar.gz"
            },
            "data": "e30=",
            "artifactType": "application/vnd.example+type"
        }
        """;

    private const string DescriptorMinimalJson = """
        {
            "mediaType": "application/vnd.oci.empty.v1+json",
            "digest": "sha256:44136fa355b311bfa706c319d8f39c36e47d288aca2e1cc38b1c",
            "size": 2
        }
        """;

    #endregion

    [Fact]
    public void Deserialize_DescriptorWithUrls_UrlsPopulated()
    {
        var bytes = Encoding.UTF8.GetBytes(DescriptorWithUrlsJson);
        var desc = OciJsonSerializer.Deserialize<Descriptor>(bytes)!;

        Assert.NotNull(desc.Urls);
        Assert.Equal(2, desc.Urls!.Count);
        Assert.Contains(
            "https://example.com/layer1.tar.gz",
            desc.Urls);
        Assert.Contains(
            "https://mirror.example.com/layer1.tar.gz",
            desc.Urls);
    }

    [Fact]
    public void Deserialize_DescriptorWithData_DataPopulated()
    {
        var bytes = Encoding.UTF8.GetBytes(DescriptorWithDataJson);
        var desc = OciJsonSerializer.Deserialize<Descriptor>(bytes)!;

        Assert.NotNull(desc.Data);
        Assert.Equal(new byte[] { 0x7B, 0x7D }, desc.Data);
    }

    [Fact]
    public void Serialize_Descriptor_NullOptionals_Absent()
    {
        var desc = new Descriptor
        {
            MediaType = MediaType.EmptyJson,
            Digest = "sha256:44136fa355b311bfa706c319d8f39c36",
            Size = 2
        };

        var json = Encoding.UTF8.GetString(
            OciJsonSerializer.SerializeToUtf8Bytes(desc));

        Assert.DoesNotContain("urls", json);
        Assert.DoesNotContain("data", json);
        Assert.DoesNotContain("annotations", json);
        Assert.DoesNotContain("artifactType", json);
        Assert.DoesNotContain("platform", json);
    }

    [Fact]
    public void Serialize_Descriptor_AllFields_MatchesWireFormat()
    {
        var bytes = Encoding.UTF8.GetBytes(DescriptorAllFieldsJson);
        var desc = OciJsonSerializer.Deserialize<Descriptor>(bytes)!;

        var json = Encoding.UTF8.GetString(
            OciJsonSerializer.SerializeToUtf8Bytes(desc));

        Assert.Contains("urls", json);
        Assert.Contains("annotations", json);
        Assert.Contains("data", json);
        Assert.Contains("artifactType", json);
    }
}
