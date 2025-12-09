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
            await using var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            using var memoryStream = new MemoryStream();
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
            await using var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            using var memoryStream = new MemoryStream();
            await fetchContent.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();
            Assert.Equal(blobs[i], bytes);
        }
    }

    /// <summary>
    /// Can copy a rooted directed acyclic graph (DAG) from the source Memory Target to the destination Memory Target
    /// with customized CopyOptions.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CanCopyWithCopyOptions()
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

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        AppendBlob(MediaType.ImageConfig, GetBytes("foo")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("bar")); // blob 1

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);
        }

        var root = descs[0];
        var reference = "foo";
        await sourceTarget.TagAsync(root, reference, cancellationToken);

        var destinationTarget = new MemoryStore();
        var copyOptions = new CopyOptions()
        {
            MapRoot = (_, _, _) => Task.FromResult(descs[1])
        };
        await sourceTarget.CopyAsync(reference, destinationTarget, "bar", copyOptions, cancellationToken);
        Assert.True(await destinationTarget.ExistsAsync(descs[1], cancellationToken));
        Assert.False(await destinationTarget.ExistsAsync(descs[0], cancellationToken));
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
            using var stream = new MemoryStream(blobs[i]);
            await sourceTarget.PushAsync(descs[i], stream, cancellationToken);
        }

        var root = descs[3];
        var destinationTarget = new MemoryStore();
        var copyGraphOptions = new CopyGraphOptions
        {
            Concurrency = 3,
            MaxMetadataBytes = 2 * 1024 * 1024,
            PreCopyAsync = (desc, ct) =>
            {
                Interlocked.Increment(ref preCopyCount);
                return Task.FromResult(CopyNodeDecision.Continue);
            },
            PostCopyAsync = (desc, ct) =>
            {
                Interlocked.Increment(ref postCopyCount);
                return Task.CompletedTask;
            },
            OnCopySkippedAsync = (desc, ct) =>
            {
                Interlocked.Increment(ref copySkipCount);
                return Task.CompletedTask;
            }
        };

        Assert.Equal(3, copyGraphOptions.Concurrency);
        Assert.Equal(2 * 1024 * 1024, copyGraphOptions.MaxMetadataBytes);
        await sourceTarget.CopyGraphAsync(destinationTarget, root, copyGraphOptions, cancellationToken);
        for (var i = 0; i < descs.Count; i++)
        {
            Assert.True(await destinationTarget.ExistsAsync(descs[i], cancellationToken));
            await using var fetchContent = await destinationTarget.FetchAsync(descs[i], cancellationToken);
            using var memoryStream = new MemoryStream();
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
    public void CopyGraphOptions_ConcurrencyZero_ThrowsArgumentOutOfRangeException()
    {
        var options = new CopyGraphOptions();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.Concurrency = 0);
        Assert.Equal("value", exception.ParamName);
        Assert.Contains("Concurrency must be greater than 0", exception.Message);
    }

    [Fact]
    public void CopyGraphOptions_ConcurrencyNegative_ThrowsArgumentOutOfRangeException()
    {
        var options = new CopyGraphOptions();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.Concurrency = -1);
        Assert.Equal("value", exception.ParamName);
        Assert.Contains("Concurrency must be greater than 0", exception.Message);
    }

    [Fact]
    public void CopyGraphOptions_MaxMetadataBytesZero_ThrowsArgumentOutOfRangeException()
    {
        var options = new CopyGraphOptions();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxMetadataBytes = 0);
        Assert.Equal("value", exception.ParamName);
        Assert.Contains("MaxMetadataBytes must be greater than 0", exception.Message);
    }

    [Fact]
    public void CopyGraphOptions_MaxMetadataBytesNegative_ThrowsArgumentOutOfRangeException()
    {
        var options = new CopyGraphOptions();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxMetadataBytes = -1);
        Assert.Equal("value", exception.ParamName);
        Assert.Contains("MaxMetadataBytes must be greater than 0", exception.Message);
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
