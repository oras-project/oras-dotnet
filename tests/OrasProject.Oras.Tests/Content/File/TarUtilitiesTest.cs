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

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OrasProject.Oras.Content.File;
using Xunit;

namespace OrasProject.Oras.Tests.Content.File;

public class TarUtilitiesTest : IDisposable
{
    private readonly string _tempDir;

    public TarUtilitiesTest()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"oras-tar-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task TarDirectoryAsync_BasicFiles()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(srcDir, "file1.txt"), "hello");
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(srcDir, "file2.txt"), "world");

        // Act
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "myprefix", output,
            reproducible: false);
        output.Position = 0;

        // Assert
        var entries = await ReadAllTarEntriesAsync(output);
        Assert.Contains(entries,
            e => e.Name == "myprefix/"
                && e.EntryType == TarEntryType.Directory);
        Assert.Contains(entries,
            e => e.Name == "myprefix/file1.txt"
                && e.EntryType == TarEntryType.RegularFile);
        Assert.Contains(entries,
            e => e.Name == "myprefix/file2.txt"
                && e.EntryType == TarEntryType.RegularFile);
    }

    [Fact]
    public async Task TarDirectoryAsync_WithSubdirectories()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        var subDir = Path.Combine(srcDir, "sub");
        Directory.CreateDirectory(subDir);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(srcDir, "root.txt"), "root");
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(subDir, "nested.txt"), "nested");

        // Act
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "pkg", output, reproducible: false);
        output.Position = 0;

        // Assert
        var entries = await ReadAllTarEntriesAsync(output);
        Assert.Contains(entries,
            e => e.Name == "pkg/"
                && e.EntryType == TarEntryType.Directory);
        Assert.Contains(entries,
            e => e.Name == "pkg/sub/"
                && e.EntryType == TarEntryType.Directory);
        Assert.Contains(entries,
            e => e.Name == "pkg/root.txt"
                && e.EntryType == TarEntryType.RegularFile);
        Assert.Contains(entries,
            e => e.Name == "pkg/sub/nested.txt"
                && e.EntryType == TarEntryType.RegularFile);
    }

    [Fact]
    public async Task TarDirectoryAsync_Reproducible_SetsEpoch()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(srcDir, "data.txt"), "content");

        // Act
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "prefix", output,
            reproducible: true);
        output.Position = 0;

        // Assert — all entries should have Unix epoch
        var entries = await ReadAllTarEntriesAsync(output);
        Assert.All(entries, e =>
            Assert.Equal(
                DateTimeOffset.UnixEpoch,
                e.ModificationTime));
    }

    [Fact]
    public async Task TarDirectoryAsync_NonReproducible_HasRecentTime()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(srcDir, "data.txt"), "content");

        // Act
        var beforeTar = DateTimeOffset.UtcNow;
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "prefix", output,
            reproducible: false);
        var afterTar = DateTimeOffset.UtcNow;
        output.Position = 0;

        // Assert — entries should have a recent time
        var entries = await ReadAllTarEntriesAsync(output);
        var earliest = beforeTar.AddMinutes(-5);
        var latest = afterTar.AddMinutes(5);
        Assert.All(entries, e =>
            Assert.InRange(
                e.ModificationTime,
                earliest,
                latest));
    }

    [Fact]
    public async Task TarDirectoryAsync_EmptyDirectory()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(srcDir);

        // Act
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "root", output,
            reproducible: true);
        output.Position = 0;

        // Assert — only root directory entry
        var entries = await ReadAllTarEntriesAsync(output);
        Assert.Single(entries);
        Assert.Equal("root/", entries[0].Name);
        Assert.Equal(
            TarEntryType.Directory, entries[0].EntryType);
    }

    [Fact]
    public async Task TarDirectoryAsync_FileContent_IsPreserved()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        var expected = "hello, tar!";
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(srcDir, "test.txt"), expected);

        // Act
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "p", output, reproducible: false);
        output.Position = 0;

        // Assert
        using var reader = new TarReader(output);
        TarEntry? entry;
        while ((entry = await reader.GetNextEntryAsync()) !=
            null)
        {
            if (entry.Name == "p/test.txt"
                && entry.DataStream != null)
            {
                using var sr = new StreamReader(
                    entry.DataStream);
                var actual = await sr.ReadToEndAsync();
                Assert.Equal(expected, actual);
                return;
            }
        }
        Assert.Fail("Expected entry p/test.txt not found");
    }

    [Fact]
    public async Task TarDirectoryAsync_SetsUidGidToZero()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(srcDir, "f.txt"), "x");

        // Act
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "test", output,
            reproducible: false);
        output.Position = 0;

        // Assert — all entries should have uid=0, gid=0
        var entries = await ReadAllTarEntriesAsync(output);
        Assert.All(entries, e =>
        {
            var pax = Assert.IsType<PaxTarEntry>(e);
            Assert.Equal(0, pax.Uid);
            Assert.Equal(0, pax.Gid);
        });
    }

    [Fact]
    public async Task TarDirectoryAsync_Reproducible_DeterministicOrder()
    {
        // Arrange — create files in non-alphabetical order
        var srcDir = Path.Combine(_tempDir, "src");
        var subB = Path.Combine(srcDir, "beta");
        var subA = Path.Combine(srcDir, "alpha");
        Directory.CreateDirectory(subB);
        Directory.CreateDirectory(subA);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(srcDir, "z.txt"), "z");
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(srcDir, "a.txt"), "a");
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(subA, "inner.txt"), "i");

        // Act
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "pkg", output,
            reproducible: true);
        output.Position = 0;

        // Assert — entries are in deterministic order
        var entries = await ReadAllTarEntriesAsync(output);
        var names = entries.Select(e => e.Name).ToList();

        // Depth-first walk: root → sorted subdirs
        // (recursing into each) → sorted files.
        Assert.Equal(
            new List<string>
            {
                "pkg/",
                "pkg/alpha/",
                "pkg/alpha/inner.txt",
                "pkg/beta/",
                "pkg/a.txt",
                "pkg/z.txt"
            },
            names);
    }

    [Fact]
    public async Task TarDirectoryAsync_PrefixWithoutSlash()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);

        // Act — prefix without trailing slash
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "notrail", output,
            reproducible: true);
        output.Position = 0;

        // Assert — root entry should still end with /
        var entries = await ReadAllTarEntriesAsync(output);
        Assert.Contains(entries,
            e => e.Name == "notrail/"
                && e.EntryType == TarEntryType.Directory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/absolute")]
    [InlineData("../escape")]
    [InlineData("a/..")]
    [InlineData("a/../b")]
    [InlineData("a/./b")]
    [InlineData("a//b")]
    public async Task TarDirectoryAsync_InvalidPrefix_Throws(
        string prefix)
    {
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);

        using var output = new MemoryStream();
        await Assert.ThrowsAsync<ArgumentException>(
            () => TarUtilities.TarDirectoryAsync(
                srcDir, prefix, output,
                reproducible: false));
    }

    [Fact]
    public async Task TarDirectoryAsync_MissingSource_Throws()
    {
        var missing = Path.Combine(
            _tempDir, "does-not-exist");

        using var output = new MemoryStream();
        await Assert.ThrowsAsync<
            DirectoryNotFoundException>(
            () => TarUtilities.TarDirectoryAsync(
                missing, "pkg", output,
                reproducible: false));

        // Stream should be empty — no partial archive
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void CreateDirectoryEntry_AppendsSlash()
    {
        // Arrange
        var dir = Path.Combine(_tempDir, "testdir");
        Directory.CreateDirectory(dir);

        // Act
        var entry = TarUtilities.CreateDirectoryEntry(
            "mydir", dir, reproducible: false);

        // Assert
        Assert.Equal("mydir/", entry.Name);
        Assert.Equal(TarEntryType.Directory, entry.EntryType);
    }

    [Fact]
    public void CreateDirectoryEntry_PreservesTrailingSlash()
    {
        // Arrange
        var dir = Path.Combine(_tempDir, "testdir");
        Directory.CreateDirectory(dir);

        // Act
        var entry = TarUtilities.CreateDirectoryEntry(
            "mydir/", dir, reproducible: false);

        // Assert
        Assert.Equal("mydir/", entry.Name);
    }

    [Fact]
    public void CreateDirectoryEntry_Reproducible_SetsEpoch()
    {
        // Arrange
        var dir = Path.Combine(_tempDir, "testdir");
        Directory.CreateDirectory(dir);

        // Act
        var entry = TarUtilities.CreateDirectoryEntry(
            "mydir", dir, reproducible: true);

        // Assert
        Assert.Equal(
            DateTimeOffset.UnixEpoch,
            entry.ModificationTime);
        Assert.Equal(0, entry.Uid);
        Assert.Equal(0, entry.Gid);
    }

    [Fact]
    public void GetUnixFileMode_Directory_Returns755()
    {
        // Arrange
        var dir = Path.Combine(_tempDir, "testdir");
        Directory.CreateDirectory(dir);
        var dirInfo = new DirectoryInfo(dir);

        // Act
        var mode = TarUtilities.GetUnixFileMode(dirInfo);

        // Assert — rwxr-xr-x = 755
        var expected = UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherExecute;
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void GetUnixFileMode_File_Returns644()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.txt");
        System.IO.File.WriteAllText(filePath, "data");
        var fileInfo = new FileInfo(filePath);

        // Act
        var mode = TarUtilities.GetUnixFileMode(fileInfo);

        // Assert — rw-r--r-- = 644
        var expected = UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.GroupRead
            | UnixFileMode.OtherRead;
        Assert.Equal(expected, mode);
    }

    [Fact]
    public async Task TarDirectoryAsync_Symlink_EmitsEntry()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        var targetFile = Path.Combine(srcDir, "real.txt");
        await System.IO.File.WriteAllTextAsync(
            targetFile, "data");
        var linkPath = Path.Combine(srcDir, "link.txt");
        try
        {
            System.IO.File.CreateSymbolicLink(
                linkPath, "real.txt");
        }
        catch (Exception ex)
            when (ex is UnauthorizedAccessException
                or PlatformNotSupportedException
                or IOException)
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "Symlinks not supported on this" +
                $" platform: {ex.Message}");
        }

        // Act
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "sym", output,
            reproducible: true);
        output.Position = 0;

        // Assert
        var entries = await ReadAllTarEntriesAsync(output);
        var symEntry = Assert.Single(entries,
            e => e.EntryType == TarEntryType.SymbolicLink);
        Assert.Equal("sym/link.txt", symEntry.Name);
        Assert.Equal("real.txt", symEntry.LinkName);
        var pax = Assert.IsType<PaxTarEntry>(symEntry);
        Assert.Equal(0, pax.Uid);
        Assert.Equal(0, pax.Gid);
        Assert.Equal(
            DateTimeOffset.UnixEpoch,
            pax.ModificationTime);
    }

    [Fact]
    public async Task
        TarDirectoryAsync_DirSymlink_EmitsEntryNoRecurse()
    {
        // Arrange — create a real subdirectory with a file,
        // then create a directory symlink pointing to it.
        var srcDir = Path.Combine(_tempDir, "src");
        var realDir = Path.Combine(srcDir, "real");
        Directory.CreateDirectory(realDir);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(realDir, "inner.txt"), "data");
        var linkDir = Path.Combine(srcDir, "linked");
        try
        {
            Directory.CreateSymbolicLink(
                linkDir, realDir);
        }
        catch (Exception ex)
            when (ex is UnauthorizedAccessException
                or PlatformNotSupportedException
                or IOException)
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "Symlinks not supported on this" +
                $" platform: {ex.Message}");
        }

        // Act
        using var output = new MemoryStream();
        await TarUtilities.TarDirectoryAsync(
            srcDir, "ds", output,
            reproducible: true);
        output.Position = 0;

        // Assert
        var entries = await ReadAllTarEntriesAsync(output);
        var names = entries
            .Select(e => e.Name).ToList();

        // The directory symlink should be emitted as a
        // SymbolicLink entry (not a Directory entry), and
        // no files from inside the target should appear.
        var symEntry = Assert.Single(entries,
            e => e.EntryType
                == TarEntryType.SymbolicLink);
        Assert.Equal("ds/linked", symEntry.Name);
        Assert.Equal(
            NormalizeSeparators(realDir),
            symEntry.LinkName);

        // inner.txt should appear only under ds/real/,
        // not under ds/linked/.
        Assert.Single(names,
            n => n == "ds/real/inner.txt");
        Assert.DoesNotContain(
            "ds/linked/inner.txt", names);
    }

    /// <summary>
    /// Normalizes path separators for test assertions,
    /// mirroring the production NormalizeSeparators logic.
    /// </summary>
    private static string NormalizeSeparators(string path)
    {
        return Path.DirectorySeparatorChar != '/'
            ? path.Replace(
                Path.DirectorySeparatorChar, '/')
            : path;
    }

    /// <summary>
    /// Reads all tar entries from a stream into a list.
    /// </summary>
    private static async Task<List<TarEntry>>
        ReadAllTarEntriesAsync(Stream stream)
    {
        var entries = new List<TarEntry>();
        using var reader = new TarReader(
            stream, leaveOpen: true);
        TarEntry? entry;
        while ((entry = await reader.GetNextEntryAsync()) !=
            null)
        {
            entries.Add(entry);
        }
        return entries;
    }
}
