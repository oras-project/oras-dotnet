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

using OrasProject.Oras.Content.File;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using Xunit;

namespace OrasProject.Oras.Tests.Content.File;

public class StoreTest
{
    /// <summary>
    /// Tests that a Store can be created with a working directory and closed successfully,
    /// either by calling Close() directly or via Dispose().
    /// </summary>
    [Fact]
    public void CanCreateAndCloseStore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Test explicit Close()
            var store = new Store(tempDir);
            store.Close();

            // Test Dispose() via using statement
            using (var store2 = new Store(tempDir))
            {
                // Store will be disposed automatically
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that a file can be added to the store, tagged, resolved, and checked for existence.
    /// </summary>
    [Fact]
    public async Task AddTagResolveAndExists_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var tempFilePath = Path.Combine(tempDir, "test.txt");
            await System.IO.File.WriteAllTextAsync(tempFilePath, "test content");

            using var store = new Store(tempDir);

            var descriptor = await store.AddAsync("test-artifact", string.Empty, tempFilePath);

            await store.TagAsync(descriptor, "latest");

            var resolved = await store.ResolveAsync("latest");

            Assert.Equal(descriptor.Digest, resolved.Digest);

            var existsForOriginal = await store.ExistsAsync(descriptor);
            var existsForResolved = await store.ExistsAsync(resolved);

            Assert.True(existsForOriginal);
            Assert.True(existsForResolved);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that after closing the store, AddAsync, TagAsync, ResolveAsync, and ExistsAsync all throw StoreClosedException.
    /// </summary>
    [Fact]
    public async Task ClosedStore_ThrowException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var tempFilePath = Path.Combine(tempDir, "closed.txt");
            await System.IO.File.WriteAllTextAsync(tempFilePath, "content");

            using var store = new Store(tempDir);
            store.Close();

            // calling Close on a closed store should not throw
            store.Close();

            // other operations should thrown StoreClosedException
            await Assert.ThrowsAsync<StoreClosedException>(() => store.AddAsync("name", string.Empty, tempFilePath));
            await Assert.ThrowsAsync<StoreClosedException>(() => store.TagAsync(Descriptor.Empty, "latest"));
            await Assert.ThrowsAsync<StoreClosedException>(() => store.ResolveAsync("latest"));
            await Assert.ThrowsAsync<StoreClosedException>(() => store.ExistsAsync(Descriptor.Empty));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
