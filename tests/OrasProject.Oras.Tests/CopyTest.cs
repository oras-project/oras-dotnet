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

using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OrasProject.Oras.Tests;

public class CopyTest
{
    /// <summary>
    /// Can copy a rooted directed acyclic graph (DAG) with the tagged root node
    /// in the source Memory Target to the destination Memory Target.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CanCopyBetweenMemoryTargetsWithTaggedNode()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();
        var appendBlob = (string mediaType, byte[] blob) =>
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSHA256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        };
        var generateManifest = (Descriptor config, List<Descriptor> layers) =>
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            appendBlob(MediaType.ImageManifest, manifestBytes);
        };
        var getBytes = (string data) => Encoding.UTF8.GetBytes(data);
        appendBlob(MediaType.ImageConfig, getBytes("config")); // blob 0
        appendBlob(MediaType.ImageLayer, getBytes("foo")); // blob 1
        appendBlob(MediaType.ImageLayer, getBytes("bar")); // blob 2
        generateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);

        }

        var root = descs[3];
        var reference = "foobar";
        await sourceTarget.TagAsync(root, reference, cancellationToken);
        var destinationTarget = new MemoryStore();
        var gotDesc = await sourceTarget.CopyAsync(reference, destinationTarget, "", cancellationToken);
        Assert.Equal(gotDesc, root);
        Assert.Equal(await destinationTarget.ResolveAsync(reference, cancellationToken), root);

        for (var i = 0; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
            var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            var memoryStream = new MemoryStream();
            await fetchContent.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();
            Assert.Equal(blobs[i], bytes);
        }
    }

    /// <summary>
    ///  Can copy a rooted directed acyclic graph (DAG) from the source Memory Target to the destination Memory Target.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CanCopyBetweenMemoryTargets()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();
        var appendBlob = (string mediaType, byte[] blob) =>
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSHA256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        };
        var generateManifest = (Descriptor config, List<Descriptor> layers) =>
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            appendBlob(MediaType.ImageManifest, manifestBytes);
        };
        var getBytes = (string data) => Encoding.UTF8.GetBytes(data);
        appendBlob(MediaType.ImageConfig, getBytes("config")); // blob 0
        appendBlob(MediaType.ImageLayer, getBytes("foo")); // blob 1
        appendBlob(MediaType.ImageLayer, getBytes("bar")); // blob 2
        generateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);

        }
        var root = descs[3];
        var destinationTarget = new MemoryStore();
        await sourceTarget.CopyGraphAsync(destinationTarget, root, cancellationToken);
        for (var i = 0; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
            var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            var memoryStream = new MemoryStream();
            await fetchContent.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();
            Assert.Equal(blobs[i], bytes);

        }
    }
    
    [Fact]
    public async Task CanCopyBetweenMemoryTargetsMountingFromDestination()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();
        var appendBlob = (string mediaType, byte[] blob) =>
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSHA256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        };
        var generateManifest = (Descriptor config, List<Descriptor> layers) =>
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            appendBlob(MediaType.ImageManifest, manifestBytes);
        };
        var getBytes = (string data) => Encoding.UTF8.GetBytes(data);
        appendBlob(MediaType.ImageConfig, getBytes("config")); // blob 0
        appendBlob(MediaType.ImageLayer, getBytes("foo")); // blob 1
        appendBlob(MediaType.ImageLayer, getBytes("bar")); // blob 2
        generateManifest(descs[0], descs.GetRange(1, 2)); // blob 3
        
        appendBlob(MediaType.ImageConfig, getBytes("config2")); // blob 4
        appendBlob(MediaType.ImageLayer, getBytes("bar2")); // blob 5
        generateManifest(descs[4], [descs[1], descs[5]]); // blob 6

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);
        }

        var root = descs[3];
        var reference = "foobar";
        await sourceTarget.TagAsync(root, reference, cancellationToken);

        var root2 = descs[6];
        var reference2 = "other/foobar";
        await sourceTarget.TagAsync(root2, reference2, cancellationToken);
        
        var destinationTarget = new MemoryStore();
        var gotDesc = await sourceTarget.CopyAsync(reference, destinationTarget, "", cancellationToken);
        Assert.Equal(gotDesc, root);
        Assert.Equal(await destinationTarget.ResolveAsync(reference, cancellationToken), root);

        for (var i = 0; i < 3; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
            var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            var memoryStream = new MemoryStream();
            await fetchContent.CopyToAsync(memoryStream, cancellationToken);
            var bytes = memoryStream.ToArray();
            Assert.Equal(blobs[i], bytes);
        }

        var copyOpts = new CopyOptions()
        {
            MountFrom = d => [reference]
        };
        var mounted = false;
        copyOpts.OnMounted += (d, s) =>
        {
            mounted = true;
        };
        var gotDesc2 = await sourceTarget.CopyAsync(reference2, destinationTarget, reference2, cancellationToken, copyOpts);

        Assert.Equal(gotDesc2, root2);
        Assert.Equal(await destinationTarget.ResolveAsync(reference2, cancellationToken), root2);
        Assert.True(mounted);
        
        for (var i = 4; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
            var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            var memoryStream = new MemoryStream();
            await fetchContent.CopyToAsync(memoryStream, cancellationToken);
            var bytes = memoryStream.ToArray();
            Assert.Equal(blobs[i], bytes);
        }
    }
}
