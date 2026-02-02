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
using OrasProject.Oras.Content.File;
using OrasProject.Oras.Content.File.Exceptions;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OrasProject.Oras.Tests.Content.File;

public class FileStoreTest : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _tempDirs = new();

    public FileStoreTest()
    {
        _tempDir = CreateTempDir();
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oras_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    /// <summary>
    /// Tests that FileStore implements ITarget correctly.
    /// </summary>
    [Fact]
    public void FileStore_ImplementsCorrectInterfaces()
    {
        using var store = new Store(_tempDir);

        Assert.True(store is ITarget);
        Assert.True(store is IPredecessorFindable);
        Assert.True(store is IDisposable);
        Assert.True(store is IAsyncDisposable);
    }

    /// <summary>
    /// Tests basic push and fetch operations.
    /// </summary>
    [Fact]
    public async Task FileStore_PushAndFetch_Success()
    {
        using var store = new Store(_tempDir);
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";
        var desc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        // Test push
        await store.PushAsync(desc, new MemoryStream(content));

        // Test exists
        Assert.True(await store.ExistsAsync(desc));

        // Test fetch
        using var fetchedStream = await store.FetchAsync(desc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests relative root with successful operations.
    /// </summary>
    [Fact]
    public async Task FileStore_RelativeRoot_Success()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            using var store = new Store(".");

            var content = Encoding.UTF8.GetBytes("hello world");
            var name = "test.txt";
            var mediaType = "test";
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(content),
                Size = content.Length,
                Annotations = new Dictionary<string, string>
                {
                    [Annotations.Title] = name
                }
            };

            // Test push
            await store.PushAsync(desc, new MemoryStream(content));

            // Test exists
            Assert.True(await store.ExistsAsync(desc));

            // Test fetch
            using var fetchedStream = await store.FetchAsync(desc);
            using var ms = new MemoryStream();
            await fetchedStream.CopyToAsync(ms);
            Assert.Equal(content, ms.ToArray());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    /// <summary>
    /// Tests that store operations fail after close.
    /// </summary>
    [Fact]
    public async Task FileStore_Close_OperationsFailAfterClose()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";
        var desc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };
        var reference = "foobar";

        var store = new Store(_tempDir);

        // Test push before close
        await store.PushAsync(desc, new MemoryStream(content));
        Assert.True(await store.ExistsAsync(desc));

        // Close the store
        store.Dispose();

        // Closing twice should not throw
        store.Dispose();

        // Test operations after close
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.AddAsync(name, mediaType, ""));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.PushAsync(desc, new MemoryStream(content)));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.ExistsAsync(desc));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.TagAsync(desc, reference));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.ResolveAsync(reference));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.FetchAsync(desc));
        await Assert.ThrowsAsync<FileStoreClosedException>(() => store.GetPredecessorsAsync(desc));
    }

    /// <summary>
    /// Tests pushing a file.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "test.txt"
            }
        };

        using var store = new Store(_tempDir);

        // Test push
        await store.PushAsync(desc, new MemoryStream(content));

        // Test exists
        Assert.True(await store.ExistsAsync(desc));

        // Test fetch
        using var fetchedStream = await store.FetchAsync(desc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests pushing content without a name (to fallback storage).
    /// </summary>
    [Fact]
    public async Task FileStore_Push_NoName()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length
        };

        using var store = new Store(_tempDir);

        // Test push
        await store.PushAsync(desc, new MemoryStream(content));

        // Test exists
        Assert.True(await store.ExistsAsync(desc));

        // Test fetch
        using var fetchedStream = await store.FetchAsync(desc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests pushing content without a name exceeding fallback limit.
    /// </summary>
    [Fact]
    public async Task FileStore_Push_NoName_ExceedLimit()
    {
        var blob = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(blob),
            Size = blob.Length
        };

        using var store = new Store(_tempDir, 1); // 1 byte limit

        await Assert.ThrowsAsync<SizeLimitExceededException>(() =>
            store.PushAsync(desc, new MemoryStream(blob)));
    }

    /// <summary>
    /// Tests that pushing content with wrong size fails.
    /// </summary>
    [Fact]
    public async Task FileStore_Push_NoName_SizeNotMatch()
    {
        var blob = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(blob),
            Size = 1
        };

        using var store = new Store(_tempDir, 1);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.PushAsync(desc, new MemoryStream(blob)));
    }

    /// <summary>
    /// Tests that file not found throws NotFoundException.
    /// </summary>
    [Fact]
    public async Task FileStore_File_NotFound()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "test.txt"
            }
        };

        using var store = new Store(_tempDir);

        Assert.False(await store.ExistsAsync(desc));
        await Assert.ThrowsAsync<NotFoundException>(() => store.FetchAsync(desc));
    }

    /// <summary>
    /// Tests that pushing content with wrong digest fails.
    /// </summary>
    [Fact]
    public async Task FileStore_File_ContentBadPush()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "test.txt"
            }
        };

        using var store = new Store(_tempDir);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.PushAsync(desc, new MemoryStream(Encoding.UTF8.GetBytes("foobar"))));
    }

    /// <summary>
    /// Tests adding a file to the store.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Add()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";
        var expectedDesc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        var path = Path.Combine(_tempDir, name);
        await System.IO.File.WriteAllBytesAsync(path, content);

        using var store = new Store(_tempDir);

        // Test add
        var gotDesc = await store.AddAsync(name, mediaType, path);
        Assert.Equal(expectedDesc.MediaType, gotDesc.MediaType);
        Assert.Equal(expectedDesc.Digest, gotDesc.Digest);
        Assert.Equal(expectedDesc.Size, gotDesc.Size);
        Assert.Equal(name, gotDesc.Annotations?[Annotations.Title]);

        // Test exists
        Assert.True(await store.ExistsAsync(gotDesc));

        // Test fetch
        using var fetchedStream = await store.FetchAsync(gotDesc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests adding a file with duplicate name fails.
    /// </summary>
    [Fact]
    public async Task FileStore_File_SameContent_DuplicateName()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";

        var path = Path.Combine(_tempDir, name);
        await System.IO.File.WriteAllBytesAsync(path, content);

        using var store = new Store(_tempDir);

        // Test add
        var gotDesc = await store.AddAsync(name, mediaType, path);
        Assert.True(await store.ExistsAsync(gotDesc));

        // Test duplicate name
        await Assert.ThrowsAsync<DuplicateNameException>(() =>
            store.AddAsync(name, mediaType, path));
    }

    /// <summary>
    /// Tests adding files with same content but different names.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Add_SameContent()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");

        var name1 = "test_1.txt";
        var name2 = "test_2.txt";

        var path1 = Path.Combine(_tempDir, name1);
        var path2 = Path.Combine(_tempDir, name2);
        await System.IO.File.WriteAllBytesAsync(path1, content);
        await System.IO.File.WriteAllBytesAsync(path2, content);

        using var store = new Store(_tempDir);

        // Test add both
        var gotDesc1 = await store.AddAsync(name1, mediaType, path1);
        var gotDesc2 = await store.AddAsync(name2, mediaType, path2);

        Assert.Equal(gotDesc1.Digest, gotDesc2.Digest);
        Assert.NotEqual(gotDesc1.Annotations?[Annotations.Title], gotDesc2.Annotations?[Annotations.Title]);

        // Test exists
        Assert.True(await store.ExistsAsync(gotDesc1));
        Assert.True(await store.ExistsAsync(gotDesc2));
    }

    /// <summary>
    /// Tests adding with missing name fails.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Add_MissingName()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";

        var path = Path.Combine(_tempDir, name);
        await System.IO.File.WriteAllBytesAsync(path, content);

        using var store = new Store(_tempDir);

        await Assert.ThrowsAsync<MissingNameException>(() =>
            store.AddAsync("", mediaType, path));
    }

    /// <summary>
    /// Tests pushing files with same content but different names.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_SameContent()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");

        var name1 = "test_1.txt";
        var desc1 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name1
            }
        };

        var name2 = "test_2.txt";
        var desc2 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name2
            }
        };

        using var store = new Store(_tempDir);

        // Test push
        await store.PushAsync(desc1, new MemoryStream(content));
        await store.PushAsync(desc2, new MemoryStream(content));

        // Test exists
        Assert.True(await store.ExistsAsync(desc1));
        Assert.True(await store.ExistsAsync(desc2));
    }

    /// <summary>
    /// Tests pushing with duplicate name fails.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_DuplicateName()
    {
        var mediaType = "test";
        var name = "test.txt";
        var content1 = Encoding.UTF8.GetBytes("hello world");
        var content2 = Encoding.UTF8.GetBytes("goodbye world");

        var desc1 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content1),
            Size = content1.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        var desc2 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content2),
            Size = content2.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        using var store = new Store(_tempDir);

        // First push should succeed
        await store.PushAsync(desc1, new MemoryStream(content1));
        Assert.True(await store.ExistsAsync(desc1));

        // Second push with same name should fail
        await Assert.ThrowsAsync<DuplicateNameException>(() =>
            store.PushAsync(desc2, new MemoryStream(content2)));
    }

    /// <summary>
    /// Tests overwrite behavior.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_Overwrite()
    {
        var mediaType = "test";
        var name = "test.txt";
        var oldContent = Encoding.UTF8.GetBytes("hello world");
        var newContent = Encoding.UTF8.GetBytes("goodbye world");

        // Create existing file
        var path = Path.Combine(_tempDir, name);
        await System.IO.File.WriteAllBytesAsync(path, oldContent);

        var desc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(newContent),
            Size = newContent.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        using var store = new Store(_tempDir);

        // Push should overwrite
        await store.PushAsync(desc, new MemoryStream(newContent));
        Assert.True(await store.ExistsAsync(desc));

        // Test fetch
        using var fetchedStream = await store.FetchAsync(desc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(newContent, ms.ToArray());
    }

    /// <summary>
    /// Tests DisableOverwrite option.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_DisableOverwrite()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";

        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        // Create existing file
        var path = Path.Combine(_tempDir, name);
        await System.IO.File.WriteAllBytesAsync(path, content);

        using var store = new Store(_tempDir)
        {
            DisableOverwrite = true
        };

        await Assert.ThrowsAsync<OverwriteDisallowedException>(() =>
            store.PushAsync(desc, new MemoryStream(content)));
    }

    /// <summary>
    /// Tests IgnoreNoName option.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_IgnoreNoName()
    {
        var config = Encoding.UTF8.GetBytes("{}");
        var configDesc = new Descriptor
        {
            MediaType = "config",
            Digest = Digest.ComputeSha256(config),
            Size = config.Length
        };

        var manifest = new Manifest
        {
            MediaType = MediaType.ImageManifest,
            Config = configDesc,
            Layers = []
        };
        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(manifestJson),
            Size = manifestJson.Length
        };

        using var store = new Store(_tempDir)
        {
            IgnoreNoName = true
        };

        // Push an OCI manifest without name
        await store.PushAsync(manifestDesc, new MemoryStream(manifestJson));

        // Verify the manifest is not saved
        Assert.False(await store.ExistsAsync(manifestDesc));
    }

    /// <summary>
    /// Tests path traversal is disallowed by default.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_DisallowPathTraversal()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "../test.txt";

        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        using var store = new Store(_tempDir);

        await Assert.ThrowsAsync<PathTraversalDisallowedException>(() =>
            store.PushAsync(desc, new MemoryStream(content)));
    }

    /// <summary>
    /// Tests path traversal when allowed.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_PathTraversal()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "../test.txt";

        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        // Create a subdirectory to work from
        var subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        using var store = new Store(subDir)
        {
            AllowPathTraversalOnWrite = true
        };

        // Should succeed when path traversal is allowed
        await store.PushAsync(desc, new MemoryStream(content));
        Assert.True(await store.ExistsAsync(desc));
    }

    /// <summary>
    /// Tests tag and resolve operations.
    /// </summary>
    [Fact]
    public async Task FileStore_TagAndResolve()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var name = "test.txt";
        var mediaType = "test";
        var desc = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };
        var reference = "foobar";

        using var store = new Store(_tempDir);

        // Push
        await store.PushAsync(desc, new MemoryStream(content));

        // Tag
        await store.TagAsync(desc, reference);

        // Resolve
        var gotDesc = await store.ResolveAsync(reference);
        Assert.Equal(desc.Digest, gotDesc.Digest);
        Assert.Equal(desc.MediaType, gotDesc.MediaType);
        Assert.Equal(desc.Size, gotDesc.Size);
    }

    /// <summary>
    /// Tests resolve for non-existent tag.
    /// </summary>
    [Fact]
    public async Task FileStore_TagNotFound()
    {
        var reference = "foobar";

        using var store = new Store(_tempDir);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            store.ResolveAsync(reference));
    }

    /// <summary>
    /// Tests tagging unknown content fails.
    /// </summary>
    [Fact]
    public async Task FileStore_TagUnknownContent()
    {
        var content = Encoding.UTF8.GetBytes("{\"layers\":[]}");
        var desc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length
        };
        var reference = "foobar";

        using var store = new Store(_tempDir);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            store.TagAsync(desc, reference));
    }

    /// <summary>
    /// Tests repeat tagging.
    /// </summary>
    [Fact]
    public async Task FileStore_RepeatTag()
    {
        using var store = new Store(_tempDir);
        var reference = "foobar";

        // First tag
        var content1 = Encoding.UTF8.GetBytes("hello world");
        var desc1 = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content1),
            Size = content1.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = Digest.ComputeSha256(content1) + ".blob"
            }
        };

        await store.PushAsync(desc1, new MemoryStream(content1));
        await store.TagAsync(desc1, reference);

        var gotDesc = await store.ResolveAsync(reference);
        Assert.Equal(desc1.Digest, gotDesc.Digest);

        // Second tag (replace)
        var content2 = Encoding.UTF8.GetBytes("foo");
        var desc2 = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content2),
            Size = content2.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = Digest.ComputeSha256(content2) + ".blob"
            }
        };

        await store.PushAsync(desc2, new MemoryStream(content2));
        await store.TagAsync(desc2, reference);

        gotDesc = await store.ResolveAsync(reference);
        Assert.Equal(desc2.Digest, gotDesc.Digest);
    }

    /// <summary>
    /// Tests predecessors functionality.
    /// </summary>
    [Fact]
    public async Task FileStore_Predecessors()
    {
        using var store = new Store(_tempDir);

        // Generate test content
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var dgst = Digest.ComputeSha256(blob);
            descs.Add(new Descriptor
            {
                MediaType = mediaType,
                Digest = dgst,
                Size = blob.Length,
                Annotations = new Dictionary<string, string>
                {
                    [Annotations.Title] = dgst.Split(':')[1][..12] + ".blob"
                }
            });
        }

        void GenerateManifest(Descriptor config, params Descriptor[] layers)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers.ToList()
            };
            var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
            AppendBlob(MediaType.ImageManifest, manifestJson);
        }

        AppendBlob(MediaType.ImageConfig, Encoding.UTF8.GetBytes("config")); // Blob 0
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("foo"));     // Blob 1
        AppendBlob(MediaType.ImageLayer, Encoding.UTF8.GetBytes("bar"));     // Blob 2
        GenerateManifest(descs[0], descs[1], descs[2]);                      // Blob 3

        // Push all content
        for (var i = 0; i < blobs.Count; i++)
        {
            await store.PushAsync(descs[i], new MemoryStream(blobs[i]));
        }

        // Verify predecessors
        // Blob 0 (config) should have manifest as predecessor
        var preds0 = (await store.GetPredecessorsAsync(descs[0])).ToList();
        Assert.Single(preds0);
        Assert.Equal(descs[3].Digest, preds0[0].Digest);

        // Blob 1 (layer) should have manifest as predecessor
        var preds1 = (await store.GetPredecessorsAsync(descs[1])).ToList();
        Assert.Single(preds1);
        Assert.Equal(descs[3].Digest, preds1[0].Digest);

        // Blob 2 (layer) should have manifest as predecessor
        var preds2 = (await store.GetPredecessorsAsync(descs[2])).ToList();
        Assert.Single(preds2);
        Assert.Equal(descs[3].Digest, preds2[0].Digest);

        // Blob 3 (manifest) should have no predecessors
        var preds3 = await store.GetPredecessorsAsync(descs[3]);
        Assert.Empty(preds3);
    }

    /// <summary>
    /// Tests ForceCAS option prevents restoration of duplicates.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_ForceCAS()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");

        var desc1 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "blob1"
            }
        };

        var desc2 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "blob2"
            }
        };

        var config = Encoding.UTF8.GetBytes("{}");
        var configDesc = new Descriptor
        {
            MediaType = MediaType.ImageConfig,
            Digest = Digest.ComputeSha256(config),
            Size = config.Length
        };

        var manifest = new Manifest
        {
            MediaType = MediaType.ImageManifest,
            Config = configDesc,
            Layers = [desc1, desc2]
        };
        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(manifestJson),
            Size = manifestJson.Length
        };

        using var store = new Store(_tempDir)
        {
            ForceCAS = true
        };

        // Push blob1
        await store.PushAsync(desc1, new MemoryStream(content));

        // Push manifest
        await store.PushAsync(manifestDesc, new MemoryStream(manifestJson));

        // Verify blob2 does NOT exist (ForceCAS prevents restoration)
        Assert.False(await store.ExistsAsync(desc2));
    }

    /// <summary>
    /// Tests restore duplicates functionality.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_RestoreDuplicates()
    {
        var mediaType = "test";
        var content = Encoding.UTF8.GetBytes("hello world");

        var desc1 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "blob1"
            }
        };

        var desc2 = new Descriptor
        {
            MediaType = mediaType,
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "blob2"
            }
        };

        var config = Encoding.UTF8.GetBytes("{}");
        var configDesc = new Descriptor
        {
            MediaType = MediaType.ImageConfig,
            Digest = Digest.ComputeSha256(config),
            Size = config.Length
        };

        var manifest = new Manifest
        {
            MediaType = MediaType.ImageManifest,
            Config = configDesc,
            Layers = [desc1, desc2]
        };
        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = Digest.ComputeSha256(manifestJson),
            Size = manifestJson.Length
        };

        using var store = new Store(_tempDir);

        // Push blob1
        await store.PushAsync(desc1, new MemoryStream(content));

        // Push manifest
        await store.PushAsync(manifestDesc, new MemoryStream(manifestJson));

        // Verify blob2 is restored
        Assert.True(await store.ExistsAsync(desc2));

        // Fetch and verify
        using var fetchedStream = await store.FetchAsync(desc2);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests resolveWritePath with various path traversal scenarios.
    /// </summary>
    [Theory]
    [InlineData("test.txt", false, false)]                // Good relative path
    [InlineData("../test.txt", false, true)]              // Bad path traversal
    [InlineData("../test.txt", true, false)]              // Path traversal allowed (will write to temp dir)
    [InlineData("..", false, true)]                       // Bad directory path
    // Note: ".." with allowTraversal=true is not tested as it would try to write to the parent of temp dir
    public async Task FileStore_resolveWritePath_PathTraversal(string name, bool allowTraversal, bool shouldThrow)
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = name
            }
        };

        using var store = new Store(_tempDir)
        {
            AllowPathTraversalOnWrite = allowTraversal
        };

        if (shouldThrow)
        {
            await Assert.ThrowsAsync<PathTraversalDisallowedException>(() =>
                store.PushAsync(desc, new MemoryStream(content)));
        }
        else
        {
            // Should not throw
            await store.PushAsync(desc, new MemoryStream(content));
            Assert.True(await store.ExistsAsync(desc));
        }
    }

    /// <summary>
    /// Tests concurrent push operations.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Push_Concurrent()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "test.txt"
            }
        };

        using var store = new Store(_tempDir);

        var concurrency = 64;
        var tasks = new Task[concurrency];

        for (var i = 0; i < concurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    await store.PushAsync(desc, new MemoryStream(content));
                }
                catch (DuplicateNameException)
                {
                    // Expected for concurrent pushes with same name
                }
            });
        }

        await Task.WhenAll(tasks);

        Assert.True(await store.ExistsAsync(desc));

        using var fetchedStream = await store.FetchAsync(desc);
        using var ms = new MemoryStream();
        await fetchedStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Tests concurrent fetch operations.
    /// </summary>
    [Fact]
    public async Task FileStore_File_Fetch_Concurrent()
    {
        var content = Encoding.UTF8.GetBytes("hello world");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length,
            Annotations = new Dictionary<string, string>
            {
                [Annotations.Title] = "test.txt"
            }
        };

        using var store = new Store(_tempDir);
        await store.PushAsync(desc, new MemoryStream(content));

        var concurrency = 64;
        var tasks = new Task[concurrency];

        for (var i = 0; i < concurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var fetchedStream = await store.FetchAsync(desc);
                using var ms = new MemoryStream();
                await fetchedStream.CopyToAsync(ms);
                Assert.Equal(content, ms.ToArray());
            });
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Tests empty reference validation.
    /// </summary>
    [Fact]
    public async Task FileStore_EmptyReference()
    {
        using var store = new Store(_tempDir);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.ResolveAsync(""));

        var content = Encoding.UTF8.GetBytes("test");
        var desc = new Descriptor
        {
            MediaType = "test",
            Digest = Digest.ComputeSha256(content),
            Size = content.Length
        };

        await store.PushAsync(desc, new MemoryStream(content));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.TagAsync(desc, ""));
    }
}
