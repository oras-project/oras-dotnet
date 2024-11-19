using System.Text.Json;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using Xunit;
using Index = OrasProject.Oras.Oci.Index;

namespace OrasProject.Oras.Tests.Oci;

public class IndexTest
{
    [Fact]
    public void GenerateIndex_CorrectlyGeneratesIndexDescriptor()
    {
        var expectedManifests = new List<Descriptor>
        {
            new Descriptor
            {
                Digest = "digest1",
                MediaType = MediaType.ImageManifest,
                Size = 100
            },
            new Descriptor
            {
                Digest = "digest2",
                MediaType = MediaType.ImageManifest,
                Size = 200
            }
        };

        var (generatedIndexDesc, generatedIndexContent) = Index.GenerateIndex(expectedManifests);
        Assert.NotNull(generatedIndexDesc);
        Assert.Equal(MediaType.ImageIndex, generatedIndexDesc.MediaType);
        Assert.Equal(generatedIndexContent.Length, generatedIndexDesc.Size);
        Assert.Equal(Digest.ComputeSHA256(generatedIndexContent), generatedIndexDesc.Digest);

        var generatedIndex = JsonSerializer.Deserialize<Index>(generatedIndexContent);
        Assert.NotNull(generatedIndex);
        Assert.Equal(2, generatedIndex.Manifests.Count);
        for (var i = 0; i < generatedIndex.Manifests.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(generatedIndex.Manifests[i], expectedManifests[i]));
        }
        Assert.Equal(MediaType.ImageIndex, generatedIndex.MediaType);
        Assert.Equal(2, generatedIndex.SchemaVersion);
    }
    
    [Fact]
    public void GenerateIndex_CorrectlyGeneratesIndexDescriptorWithEmptyManifests()
    {
        var expectedManifests = new List<Descriptor>();
        var (generatedIndexDesc, generatedIndexContent) = Index.GenerateIndex(expectedManifests);

        Assert.NotNull(generatedIndexDesc);
        Assert.Equal(MediaType.ImageIndex, generatedIndexDesc.MediaType);
        Assert.Equal(generatedIndexContent.Length, generatedIndexDesc.Size);
        Assert.Equal(Digest.ComputeSHA256(generatedIndexContent), generatedIndexDesc.Digest);

        var generatedIndex = JsonSerializer.Deserialize<Index>(generatedIndexContent);
        Assert.NotNull(generatedIndex);
        Assert.Empty(generatedIndex.Manifests);
        Assert.Equal(expectedManifests, generatedIndex.Manifests);
        Assert.Equal(MediaType.ImageIndex, generatedIndex.MediaType);
        Assert.Equal(2, generatedIndex.SchemaVersion);
    }
}
