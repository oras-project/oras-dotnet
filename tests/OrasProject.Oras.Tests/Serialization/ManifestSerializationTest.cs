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

/// <summary>
/// Comprehensive serialization tests for OCI and Docker manifests,
/// indexes, descriptors, and platforms. Organized as partial classes
/// with cross-cutting theories in this base file.
/// </summary>
public partial class ManifestSerializationTest
{
    /// <summary>
    /// Round-trip: deserialize → serialize → deserialize → serialize.
    /// Verifies no \u002B escaping and byte-level idempotency.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllManifestRoundTripFixtures))]
    public void RoundTrip_PreservesWireFormat(
        string json, bool isIndex)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        byte[] reserialized;
        if (isIndex)
        {
            var idx =
                OciJsonSerializer.Deserialize<OciIndex>(bytes)!;
            reserialized =
                OciJsonSerializer.SerializeToUtf8Bytes(idx);
        }
        else
        {
            var m =
                OciJsonSerializer.Deserialize<Manifest>(bytes)!;
            reserialized =
                OciJsonSerializer.SerializeToUtf8Bytes(m);
        }

        var output = Encoding.UTF8.GetString(reserialized);
        Assert.DoesNotContain("\\u002B", output);

        // Byte-level idempotency on second round-trip
        byte[] reserialized2;
        if (isIndex)
        {
            var idx2 =
                OciJsonSerializer.Deserialize<OciIndex>(
                    reserialized)!;
            reserialized2 =
                OciJsonSerializer.SerializeToUtf8Bytes(idx2);
        }
        else
        {
            var m2 =
                OciJsonSerializer.Deserialize<Manifest>(
                    reserialized)!;
            reserialized2 =
                OciJsonSerializer.SerializeToUtf8Bytes(m2);
        }
        Assert.Equal(reserialized, reserialized2);
    }

    /// <summary>
    /// Verify serialized output of every fixture never contains
    /// the escaped form \u002B.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllSerializableManifestFixtures))]
    public void Serialize_NeverEscapesPlus(
        string json, bool isIndex)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        byte[] serialized;
        if (isIndex)
        {
            var idx =
                OciJsonSerializer.Deserialize<OciIndex>(bytes)!;
            serialized =
                OciJsonSerializer.SerializeToUtf8Bytes(idx);
        }
        else
        {
            var m =
                OciJsonSerializer.Deserialize<Manifest>(bytes)!;
            serialized =
                OciJsonSerializer.SerializeToUtf8Bytes(m);
        }

        var output = Encoding.UTF8.GetString(serialized);
        Assert.DoesNotContain("\\u002B", output);
    }

    /// <summary>
    /// Forward compatibility: inject unknown JSON field into every
    /// fixture and verify deserialization still succeeds.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllSerializableManifestFixtures))]
    public void Deserialize_IgnoresUnknownFields(
        string json, bool isIndex)
    {
        // Inject unknown field after first '{'
        var modified = json.Insert(
            json.IndexOf('{') + 1,
            "\"unknownExtra\":42,");
        var bytes = Encoding.UTF8.GetBytes(modified);

        if (isIndex)
        {
            var idx =
                OciJsonSerializer.Deserialize<OciIndex>(bytes);
            Assert.NotNull(idx);
        }
        else
        {
            var m =
                OciJsonSerializer.Deserialize<Manifest>(bytes);
            Assert.NotNull(m);
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

    #region Fixture Aggregators

    /// <summary>
    /// All manifest and index fixtures for round-trip testing.
    /// Each entry: (json, isIndex).
    /// </summary>
    public static IEnumerable<object[]>
        AllManifestRoundTripFixtures()
    {
        // OCI Manifests (isIndex = false)
        yield return new object[]
            { SpecImageManifestJson, false };
        yield return new object[]
            { MinimalManifestJson, false };
        yield return new object[]
            { FullManifestJson, false };
        yield return new object[]
            { LayerAnnotationManifestJson, false };

        // Docker Manifest (isIndex = false)
        yield return new object[]
            { DockerManifestJson, false };

        // Artifacts (isIndex = false)
        yield return new object[]
            { MinimalArtifactJson, false };
        yield return new object[]
            { ArtifactWithConfigJson, false };
        yield return new object[]
            { ArtifactPlusSignJson, false };

        // Indexes (isIndex = true)
        yield return new object[]
            { MinimalIndexJson, true };
        yield return new object[]
            { FullIndexJson, true };
        yield return new object[]
            { SpecMultiPlatformIndexJson, true };
        yield return new object[]
            { NestedIndexJson, true };

        // Docker manifest list (deserialized as Index)
        yield return new object[]
            { DockerManifestListJson, true };
    }

    /// <summary>
    /// All serializable manifest/index fixtures.
    /// Same set as round-trip but may include additional
    /// fixtures in the future.
    /// </summary>
    public static IEnumerable<object[]>
        AllSerializableManifestFixtures()
    {
        return AllManifestRoundTripFixtures();
    }

    #endregion
}
