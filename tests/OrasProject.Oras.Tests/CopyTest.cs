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
using Index = OrasProject.Oras.Oci.Index;

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
    public async Task CanCopyGraph_FullCopy()
    {
        var src = new MemoryStore();
        var dst = new MemoryStore();

        // Generate test content
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

        void GenerateManifest(Descriptor config, params Descriptor[] layers)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers.ToList()
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        void GenerateIndex(params Descriptor[] manifests)
        {
            var index = new Index
            {
                Manifests = manifests.ToList()
            };
            var indexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(index));
            AppendBlob(MediaType.ImageIndex, indexBytes);
        }

        // Append blobs and generate manifests and indices
        var getBytes = (string data) => Encoding.UTF8.GetBytes(data);
        AppendBlob(MediaType.ImageConfig, getBytes("config")); // Blob 0
        AppendBlob(MediaType.ImageLayer, getBytes("foo"));     // Blob 1
        AppendBlob(MediaType.ImageLayer, getBytes("bar"));     // Blob 2
        AppendBlob(MediaType.ImageLayer, getBytes("hello"));   // Blob 3
        GenerateManifest(descs[0], descs[1], descs[2]);                 // Blob 4
        GenerateManifest(descs[0], descs[3]);                                      // Blob 5
        GenerateManifest(descs[0], descs[1], descs[2], descs[3]);       // Blob 6
        GenerateIndex(descs[4], descs[5]);                           // Blob 7
        GenerateIndex(descs[6]);                                                   // Blob 8
        GenerateIndex();                                                           // Blob 9
        GenerateIndex(descs[7], descs[8], descs[9]);                 // Blob 10

        var root = descs[^1]; // The last descriptor as the root

        // Push blobs to the source memory store
        for (int i = 0; i < blobs.Count; i++)
        {
            await src.PushAsync(descs[i], new MemoryStream(blobs[i]), CancellationToken.None);
        }

        // Set up tracking storage wrappers for verification
        var srcTracker = new StorageTracker(src);
        var dstTracker = new StorageTracker(dst);

        // Perform the copy graph operation
        var copyOptions = new CopyGraphOptions();
        await srcTracker.CopyGraphAsync(dstTracker, root, copyOptions, CancellationToken.None);

        // Verify contents in the destination
        foreach (var (desc, blob) in descs.Zip(blobs, Tuple.Create))
        {
            Assert.True(await dst.ExistsAsync(desc, CancellationToken.None), $"Blob {desc.Digest} should exist in destination.");
            var fetchedContent = await dst.FetchAsync(desc, CancellationToken.None);
            using var memoryStream = new MemoryStream();
            await fetchedContent.CopyToAsync(memoryStream);
            Assert.Equal(blob, memoryStream.ToArray());
        }

        // Verify API counts
        // REMARKS: FetchCount should equal to blobs.Count
        // but since there's no caching implemented, it is not
        Assert.Equal(18, srcTracker.FetchCount);
        Assert.Equal(0, srcTracker.PushCount);
        Assert.Equal(0, srcTracker.ExistsCount);
        Assert.Equal(0, dstTracker.FetchCount);
        Assert.Equal(blobs.Count, dstTracker.PushCount);

        // REMARKS: ExistsCount should equal to blobs.Count
        // but since there's no caching implemented, it is not
        Assert.Equal(16, dstTracker.ExistsCount);
    }

    [Fact]
    public async Task CanCopyExistingRoot()
    {
        var src = new MemoryStore();
        var dst = new MemoryStore();

        // Generate test content
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

        void GenerateManifest(Descriptor config, params Descriptor[] layers)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers.ToList()
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        // Add blobs and generate manifest
        AppendBlob(MediaType.ImageConfig, Encoding.UTF8.GetBytes("config")); // Blob 0
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("foo"));     // Blob 1
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("bar"));     // Blob 2
        GenerateManifest(descs[0], descs[1], descs[2]);                      // Blob 3

        // Push blobs to the source store
        foreach (var (blob, desc) in blobs.Zip(descs))
        {
            await src.PushAsync(desc, new MemoryStream(blob), CancellationToken.None);
        }

        var root = descs[3];
        var refTag = "foobar";
        var newTag = "newtag";

        // Tag root node in source
        await src.TagAsync(root, refTag, CancellationToken.None);

        // Prepare copy options with OnCopySkippedAsync
        var skippedCount = 0;
        var skippedAsyncCount = 0;
        var copyOptions = new CopyOptions();

        copyOptions.CopySkipped += _ => skippedCount++;

        copyOptions.CopySkippedAsync += _ =>
        {
            skippedAsyncCount++;
            return Task.CompletedTask;
        };

        // Copy with the source tag
        var gotDesc = await src.CopyAsync(refTag, dst, "", copyOptions, CancellationToken.None);
        Assert.Equal(root, gotDesc);

        // Copy with a new tag
        gotDesc = await src.CopyAsync(refTag, dst, newTag, copyOptions, CancellationToken.None);
        Assert.Equal(root, gotDesc);

        // Verify contents in the destination
        foreach (var desc in descs)
        {
            var exists = await dst.ExistsAsync(desc, CancellationToken.None);
            Assert.True(exists, $"Destination should contain descriptor {desc.Digest}");
        }

        // Verify the source tag in destination
        gotDesc = await dst.ResolveAsync(refTag, CancellationToken.None);
        Assert.Equal(root, gotDesc);

        // Verify the new tag in destination
        gotDesc = await dst.ResolveAsync(newTag, CancellationToken.None);
        Assert.Equal(root, gotDesc);

        // Verify the OnCopySkippedAsync invocation count
        Assert.Equal(1, skippedCount);
        Assert.Equal(1, skippedAsyncCount);
    }

    [Fact]
    public async Task CanCopyWithOptions()
    {
        var src = new MemoryStore();

        // Generate test content
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            descs.Add(new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            });
        }

        void AppendManifest(string arc, string os, string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            descs.Add(new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length,
                Platform = new Platform
                {
                    Architecture = arc,
                    Os = os
                }
            });
        }

        void GenerateManifest(string arc, string os, Descriptor config, params Descriptor[] layers)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers.ToList()
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendManifest(arc, os, MediaType.ImageManifest, manifestBytes);
        }

        void GenerateIndex(params Descriptor[] manifests)
        {
            var index = new Index
            {
                Manifests = manifests.ToList()
            };
            var indexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(index));
            AppendBlob(MediaType.ImageIndex, indexBytes);
        }

        // Create blobs and manifests
        AppendBlob(MediaType.ImageConfig, Encoding.UTF8.GetBytes("config"));           // Blob 0
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("foo"));               // Blob 1
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("bar"));               // Blob 2
        GenerateManifest("test-arc-1", "test-os-1", descs[0], descs[1], descs[2]);      // Blob 3
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("hello"));             // Blob 4
        GenerateManifest("test-arc-2", "test-os-2", descs[0], descs[4]);               // Blob 5
        GenerateIndex(descs[3], descs[5]);                                             // Blob 6

        var cancellationToken = CancellationToken.None;

        // Push blobs to source store
        for (int i = 0; i < blobs.Count; i++)
        {
            await src.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);
        }

        var root = descs[6];
        var refTag = "foobar";
        await src.TagAsync(root, refTag, cancellationToken);

        // Test copy with platform filter and hooks
        var dst = new MemoryStore();
        var preCopyCount = 0;
        var postCopyCount = 0;
        var preCopyAsyncCount = 0;
        var postCopyAsyncCount = 0;
        var copyOptions = new CopyOptions();

        copyOptions.PreCopy += _ => preCopyCount++;
        copyOptions.PreCopyAsync += d =>
        {
            preCopyAsyncCount++;
            return Task.CompletedTask;
        };

        copyOptions.PostCopy += _ => postCopyCount++;
        copyOptions.PostCopyAsync += d =>
        {
            postCopyAsyncCount++;
            return Task.CompletedTask;
        };

        var expectedDesc = descs[6];
        var gotDesc = await src.CopyAsync(refTag, dst, "", copyOptions, cancellationToken);
        Assert.Equal(expectedDesc, gotDesc);

        // Verify platform-specific contents
        var expectedDescs = new[] { descs[0], descs[4], descs[5] };
        foreach (var desc in expectedDescs)
        {
            Assert.True(await dst.ExistsAsync(desc, cancellationToken));
        }

        // Verify API counts
        Assert.Equal(7, preCopyCount);
        Assert.Equal(7, preCopyAsyncCount);
        Assert.Equal(7, postCopyCount);
        Assert.Equal(7, postCopyAsyncCount);
    }


    private class StorageTracker : ITarget
    {
        private readonly ITarget _storage;

        public int FetchCount { get; private set; }
        public int PushCount { get; private set; }
        public int ExistsCount { get; private set; }

        public IList<string> Fetched { get; } = [];

        public StorageTracker(ITarget storage)
        {
            _storage = storage;
        }

        public async Task<bool> ExistsAsync(Descriptor desc, CancellationToken cancellationToken)
        {
            ExistsCount++;
            return await _storage.ExistsAsync(desc, cancellationToken);
        }

        public async Task<Stream> FetchAsync(Descriptor desc, CancellationToken cancellationToken)
        {
            FetchCount++;
            Fetched.Add(desc.Digest);
            return await _storage.FetchAsync(desc, cancellationToken);
        }

        public async Task PushAsync(Descriptor desc, Stream content, CancellationToken cancellationToken)
        {
            PushCount++;
            await _storage.PushAsync(desc, content, cancellationToken);
        }

        public async Task TagAsync(Descriptor desc, string reference, CancellationToken cancellationToken)
        {
            await _storage.TagAsync(desc, reference, cancellationToken);
        }

        public Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken)
        {
            return _storage.ResolveAsync(reference, cancellationToken);
        }
    }
}
