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
using OrasProject.Oras.Registry;
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
    public async Task CanMountFromSourceRepository()
    {
        var sourceTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        // Utility function to append blobs
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
        
        var generateIndex = (Descriptor config, List<Descriptor> manifests) =>
        {
            var manifest = new Index()
            {
                Manifests = manifests
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            appendBlob(MediaType.ImageIndex, manifestBytes);
        };

        // Generate blobs and manifest
        var getBytes = (string data) => Encoding.UTF8.GetBytes(data);
        appendBlob(MediaType.ImageConfig, getBytes("config"));  // Blob 0
        appendBlob(MediaType.ImageLayer, getBytes("foo"));      // Blob 1
        appendBlob(MediaType.ImageLayer, getBytes("bar"));      // Blob 2
        generateManifest(descs[0], descs[1..3]);                // Blob 3
        appendBlob(MediaType.ImageLayer, getBytes("hello"));    // Blob 4
        generateManifest(descs[0], [descs[4]]);                 // Blob 5
        generateIndex(descs[3], [descs[5]]);                    // Blob 6

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);
        }

        var root = descs[3];
        var destinationTarget = new CountingStore(new MemoryStore());

        var numMount = new AtomicCounter();
        destinationTarget.OnMount = async (descriptor,contentReference, getContents, cancellationToken) => 
        {
            numMount.Increment();
            if (contentReference != "source")
            {
                throw new Exception($"fromRepo = {contentReference}, want source");
            }

            var fetchedContent = await sourceTarget.FetchAsync(descriptor, cancellationToken);
            await destinationTarget.PushAsync(descriptor, fetchedContent, cancellationToken); // Bypass counters
        };

        var copyOptions = new CopyOptions
        {
            MountFrom = (desc) => new[] { "source" },
        };
        copyOptions.OnPreCopy += (desc) => destinationTarget.PreCopyCounter.Increment();
        copyOptions.OnPostCopy += (desc) => destinationTarget.PostCopyCounter.Increment();
        copyOptions.OnMounted += (desc,reference) => destinationTarget.OnMountedCounter.Increment();

        // Perform the CopyGraph operation
        await sourceTarget.CopyGraphAsync(destinationTarget, root, cancellationToken, copyOptions);

        // Verify the expected counts
        Assert.Equal(4, numMount.Value);
        Assert.Equal(4, destinationTarget.OnMountedCounter.Value);
        Assert.Equal(4, destinationTarget.MountFromCounter.Value);
    }

    // Custom class for tracking operation counts
    public class CountingStore : ITarget, IMounter
    {
        public MemoryStore Store { get; }
        public AtomicCounter ExistsCounter { get; } = new AtomicCounter();
        public AtomicCounter FetchCounter { get; } = new AtomicCounter();
        public AtomicCounter PushCounter { get; } = new AtomicCounter();
        public AtomicCounter OnMountedCounter { get; } = new AtomicCounter();
        public AtomicCounter PreCopyCounter { get; } = new AtomicCounter();
        public AtomicCounter PostCopyCounter { get; } = new AtomicCounter();
        public AtomicCounter MountFromCounter { get; } = new AtomicCounter();

        public Func<Descriptor, string, Func<CancellationToken, Task<Stream>>?, CancellationToken, Task> OnMount { get; set; }

        public CountingStore(MemoryStore store)
        {
            Store = store;
        }

        public async Task<bool> ExistsAsync(Descriptor desc, CancellationToken cancellationToken)
        {
            ExistsCounter.Increment();
            return await Store.ExistsAsync(desc, cancellationToken);
        }

        public async Task<Stream> FetchAsync(Descriptor desc, CancellationToken cancellationToken)
        {
            FetchCounter.Increment();
            return await Store.FetchAsync(desc, cancellationToken);
        }

        public async Task PushAsync(Descriptor desc, Stream content, CancellationToken cancellationToken)
        {
            PushCounter.Increment();
            await Store.PushAsync(desc, content, cancellationToken);
        }

        public async Task TagAsync(Descriptor desc, string reference, CancellationToken cancellationToken)
        {
            await Store.TagAsync(desc, reference, cancellationToken);
        }

        public Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken)
        {
            return Store.ResolveAsync(reference, cancellationToken);
        }

        public async Task MountAsync(Descriptor descriptor, string contentReference, Func<CancellationToken, Task<Stream>>? getContents, CancellationToken cancellationToken)
        {
            MountFromCounter.Increment();
            await OnMount.Invoke(descriptor,contentReference, getContents, cancellationToken);
        }
    }

    // Utility class for thread-safe counter increments
    public class AtomicCounter
    {
        private long _value;

        public long Value => Interlocked.Read(ref _value);

        public void Increment()
        {
            Interlocked.Increment(ref _value);
        }
    }
}
