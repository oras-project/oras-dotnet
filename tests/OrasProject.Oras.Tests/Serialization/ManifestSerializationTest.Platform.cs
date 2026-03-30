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
    #region Platform JSON Fixtures

    private const string FullPlatformJson = """
        {
            "architecture": "amd64",
            "os": "linux",
            "os.version": "5.4.0-42-generic",
            "os.features": ["feature1", "feature2"],
            "variant": "v8"
        }
        """;

    private const string MinimalPlatformJson = """
        {
            "architecture": "amd64",
            "os": "linux"
        }
        """;

    private const string PlatformWithOsFeaturesJson = """
        {
            "architecture": "arm64",
            "os": "linux",
            "os.features": ["sse4", "aes", "sha1", "sha2"]
        }
        """;

    #endregion

    [Fact]
    public void Serialize_Platform_AllFields()
    {
        var platform = new Platform
        {
            Architecture = "amd64",
            Os = "linux",
            OsVersion = "5.4.0-42-generic",
            OsFeatures = ["feature1", "feature2"],
            Variant = "v8"
        };

        var json = Encoding.UTF8.GetString(
            OciJsonSerializer.SerializeToUtf8Bytes(platform));

        Assert.Contains("\"architecture\":\"amd64\"", json);
        Assert.Contains("\"os\":\"linux\"", json);
        Assert.Contains(
            "\"os.version\":\"5.4.0-42-generic\"", json);
        Assert.Contains("\"os.features\"", json);
        Assert.Contains("\"variant\":\"v8\"", json);
    }

    [Fact]
    public void Serialize_Platform_OmitsDefaultFields()
    {
        var platform = new Platform
        {
            Architecture = "amd64",
            Os = "linux"
        };

        var json = Encoding.UTF8.GetString(
            OciJsonSerializer.SerializeToUtf8Bytes(platform));

        Assert.Contains("\"architecture\":\"amd64\"", json);
        Assert.Contains("\"os\":\"linux\"", json);
        Assert.DoesNotContain("\"os.version\"", json);
        Assert.DoesNotContain("\"os.features\"", json);
        Assert.DoesNotContain("\"variant\"", json);
    }

    [Fact]
    public void Deserialize_Platform_MissingOptionals_NoException()
    {
        var bytes = Encoding.UTF8.GetBytes(MinimalPlatformJson);
        var platform =
            OciJsonSerializer.Deserialize<Platform>(bytes)!;

        Assert.Equal("amd64", platform.Architecture);
        Assert.Equal("linux", platform.Os);
        Assert.Null(platform.OsVersion);
        Assert.Null(platform.OsFeatures);
        Assert.Null(platform.Variant);
    }

    [Fact]
    public void Deserialize_Platform_WithOsFeatures_ListPopulated()
    {
        var bytes =
            Encoding.UTF8.GetBytes(PlatformWithOsFeaturesJson);
        var platform =
            OciJsonSerializer.Deserialize<Platform>(bytes)!;

        Assert.NotNull(platform.OsFeatures);
        Assert.Equal(4, platform.OsFeatures!.Count);
        Assert.Contains("sse4", platform.OsFeatures);
    }
}
