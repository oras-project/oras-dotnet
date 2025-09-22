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

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        }

        void GenerateManifest(Descriptor config, List<Descriptor> layers)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("foo")); // blob 1
        AppendBlob(MediaType.ImageLayer, GetBytes("bar")); // blob 2
        GenerateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

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
    /// Can copy a rooted directed acyclic graph (DAG) from the source Memory Target to the destination Memory Target.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CanCopyBetweenMemoryTargets()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        }

        void GenerateManifest(Descriptor config, List<Descriptor> layers)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("foo")); // blob 1
        AppendBlob(MediaType.ImageLayer, GetBytes("bar")); // blob 2
        GenerateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);
        }

        var root = descs[3];
        var destinationTarget = new MemoryStore();
        var proxy = new Proxy()
        {
            Cache = new MemoryStorage(),
            Source = sourceTarget
        };
        await sourceTarget.CopyGraphAsync(destinationTarget, root, proxy, new CopyGraphOptions(), cancellationToken);
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
    /// Can copy a rooted directed acyclic graph (DAG) from the source Memory Target to the destination Memory Target
    /// with customized CopyGraphOptions.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CanCopyWithCopyGraphOptions()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();
        var preCopyCount = 0;
        var postCopyCount = 0;
        var copySkipCount = 0;

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        }

        void GenerateManifest(Descriptor config, List<Descriptor> layers)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("foo")); // blob 1
        AppendBlob(MediaType.ImageLayer, GetBytes("bar")); // blob 2
        GenerateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);
        }

        var root = descs[3];
        var destinationTarget = new MemoryStore();
        var copyGraphOptions = new CopyGraphOptions
        {
            Concurrency = 3,
            MaxMetadataBytes = 2 * 1024 * 1024,
            PreCopy = (desc, ct) =>
            {
                preCopyCount++;
                return Task.FromResult(CopyNodeDecision.Continue);
            },
            PostCopy = (desc, ct) =>
            {
                postCopyCount++;
                return Task.CompletedTask;
            },
            OnCopySkipped = (desc, ct) =>
            {
                copySkipCount++;
                return Task.CompletedTask;
            }
        };

        Assert.Equal(3, copyGraphOptions.Concurrency);
        Assert.Equal(2 * 1024 * 1024, copyGraphOptions.MaxMetadataBytes);
        await sourceTarget.CopyGraphAsync(destinationTarget, root, copyGraphOptions, cancellationToken);
        for (var i = 0; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
            var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            var memoryStream = new MemoryStream();
            await fetchContent.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();
            Assert.Equal(blobs[i], bytes);
        }

        // do another copy to trigger OnCopySkipped
        await sourceTarget.CopyGraphAsync(destinationTarget, root, copyGraphOptions, cancellationToken);
        Assert.Equal(4, preCopyCount);
        Assert.Equal(4, postCopyCount);
        Assert.Equal(1, copySkipCount);
    }

    [Fact]
    public void CanCreateCopyGraphOptionsWithDefaultValues()
    {
        var options = new CopyGraphOptions()
        {
            Concurrency = 0,
            MaxMetadataBytes = 0
        };
        Assert.Equal(10, options.Concurrency);
        Assert.Equal(4 * 1024 * 1024, options.MaxMetadataBytes);
    }

    [Fact]
    public async Task CopyAsync_SrcRefIsNull_ThrowsError()
    {
        var cancellationToken = new CancellationToken();
        var sourceTarget = new MemoryStore();
        var destinationTarget = new MemoryStore();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sourceTarget.CopyAsync("", destinationTarget, "", cancellationToken));
    }

    [Fact]
    public async Task CopyGraphAsync_DescIsInvalid_ThrowsError()
    {
        var cancellationToken = new CancellationToken();
        var sourceTarget = new MemoryStore();
        var destinationTarget = new MemoryStore();
        var invalidDesc = new Descriptor()
        {
            MediaType = MediaType.ImageConfig,
            Digest = ""
        };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sourceTarget.CopyGraphAsync(destinationTarget, invalidDesc, cancellationToken));
    }
}
