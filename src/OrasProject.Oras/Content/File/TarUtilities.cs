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
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content.File;

/// <summary>
/// Utilities for creating tar archives from directories.
/// </summary>
internal static class TarUtilities
{
    /// <summary>
    /// Creates a tar archive from a directory and writes it to a stream.
    /// </summary>
    /// <param name="sourceDirectory">The source directory to archive.</param>
    /// <param name="prefix">The prefix for entries in the tar archive.</param>
    /// <param name="outputStream">
    /// The output stream to write the tar archive to.
    /// </param>
    /// <param name="reproducible">
    /// Whether to remove timestamps for reproducibility.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="prefix"/> is empty, absolute, or
    /// contains traversal segments.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when <paramref name="sourceDirectory"/> does not exist.
    /// </exception>
    internal static async Task TarDirectoryAsync(
        string sourceDirectory,
        string prefix,
        Stream outputStream,
        bool reproducible,
        CancellationToken cancellationToken = default)
    {
        // Normalize and validate prefix
        prefix = prefix.Replace('\\', '/').TrimEnd('/');
        ValidatePrefix(prefix);

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Source directory not found: '{sourceDirectory}'.");
        }

        using var tarWriter = new TarWriter(outputStream, leaveOpen: true);

        // Add root directory entry
        var rootEntry = CreateDirectoryEntry(prefix, sourceDirectory, reproducible);
        await tarWriter.WriteEntryAsync(rootEntry, cancellationToken)
            .ConfigureAwait(false);

        // Walk directory tree manually to avoid recursing into symlinked directories.
        await WalkDirectoryAsync(
            tarWriter, sourceDirectory, prefix, sourceDirectory,
            reproducible, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Recursively walks a directory, emitting tar entries. Symlinked directories
    /// are emitted as symlink entries without recursion to prevent cycles and
    /// unintended inclusion of files outside the source tree.
    /// </summary>
    /// <remarks>
    /// This method uses recursion bounded by actual filesystem depth. Symlinked
    /// directories are not followed, so cycles cannot occur. For typical OCI layer
    /// content the depth is well within safe stack limits.
    /// Symlink targets (both directory and file) are preserved as-is from the
    /// filesystem, which may include absolute paths. Callers or extractors must
    /// validate link targets independently to guard against symlink-based path
    /// traversal.
    /// </remarks>
    private static async Task WalkDirectoryAsync(
        TarWriter tarWriter,
        string sourceDirectory,
        string prefix,
        string currentDir,
        bool reproducible,
        CancellationToken cancellationToken)
    {
        // Collect subdirectories: materialize and sort when reproducible;
        // stream lazily otherwise.
        IEnumerable<string> subDirs;
        if (reproducible)
        {
            var dirs = Directory.GetDirectories(currentDir);
            Array.Sort(dirs, StringComparer.Ordinal);
            subDirs = dirs;
        }
        else
        {
            subDirs = Directory.EnumerateDirectories(currentDir);
        }

        foreach (var dir in subDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDirectory, dir);
            var entryName = prefix + "/" + NormalizeSeparators(relativePath);
            if (!entryName.EndsWith('/'))
            {
                entryName += '/';
            }

            var dirInfo = new DirectoryInfo(dir);
            if (dirInfo.LinkTarget != null)
            {
                // Emit symlink entry without recursing
                var symEntryName = entryName.EndsWith('/')
                    ? entryName[..^1] : entryName;

                var symEntry = new PaxTarEntry(
                    TarEntryType.SymbolicLink, symEntryName)
                {
                    LinkName = NormalizeSeparators(dirInfo.LinkTarget),
                    Uid = 0,
                    Gid = 0
                };

                if (reproducible)
                {
                    symEntry.ModificationTime = DateTimeOffset.UnixEpoch;
                }

                await tarWriter.WriteEntryAsync(symEntry, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            var entry = CreateDirectoryEntry(entryName, dir, reproducible);
            await tarWriter.WriteEntryAsync(entry, cancellationToken)
                .ConfigureAwait(false);

            // Recurse into real (non-symlink) directories
            await WalkDirectoryAsync(
                tarWriter, sourceDirectory, prefix, dir,
                reproducible, cancellationToken).ConfigureAwait(false);
        }

        // Collect files: materialize and sort when reproducible;
        // stream lazily otherwise.
        IEnumerable<string> localFiles;
        if (reproducible)
        {
            var files = Directory.GetFiles(currentDir);
            Array.Sort(files, StringComparer.Ordinal);
            localFiles = files;
        }
        else
        {
            localFiles = Directory.EnumerateFiles(currentDir);
        }

        foreach (var file in localFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var entryName = prefix + "/" + NormalizeSeparators(relativePath);

            var fileInfo = new FileInfo(file);

            if (fileInfo.LinkTarget != null)
            {
                var symEntry = new PaxTarEntry(
                    TarEntryType.SymbolicLink, entryName)
                {
                    LinkName = NormalizeSeparators(fileInfo.LinkTarget),
                    Uid = 0,
                    Gid = 0
                };

                if (reproducible)
                {
                    symEntry.ModificationTime = DateTimeOffset.UnixEpoch;
                }

                await tarWriter.WriteEntryAsync(symEntry, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var dataStream = System.IO.File.OpenRead(file);
                await using (dataStream.ConfigureAwait(false))
                {
                    var fileEntry = new PaxTarEntry(
                        TarEntryType.RegularFile, entryName)
                    {
                        DataStream = dataStream,
                        Uid = 0,
                        Gid = 0,
                        Mode = GetUnixFileMode(fileInfo)
                    };

                    if (reproducible)
                    {
                        fileEntry.ModificationTime = DateTimeOffset.UnixEpoch;
                    }

                    await tarWriter.WriteEntryAsync(fileEntry, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Normalizes OS-specific directory separators to forward slashes for tar
    /// entry paths. On Unix this is a no-op, preserving literal backslashes in
    /// filenames.
    /// </summary>
    private static string NormalizeSeparators(string path)
    {
        return Path.DirectorySeparatorChar != '/'
            ? path.Replace(Path.DirectorySeparatorChar, '/')
            : path;
    }

    /// <summary>
    /// Returns true if the path starts with a Windows-style drive prefix
    /// (e.g. "C:" or "C:/"). This check is needed because
    /// <see cref="Path.IsPathRooted"/> does not recognise drive letters on
    /// non-Windows platforms.
    /// </summary>
    private static bool HasDrivePrefix(string path)
    {
        return path.Length >= 2
            && char.IsAsciiLetter(path[0])
            && path[1] == ':';
    }

    /// <summary>
    /// Validates that a prefix is a relative, non-rooted path with no traversal,
    /// empty, or dot-only segments.
    /// </summary>
    private static void ValidatePrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            throw new ArgumentException(
                "Prefix must not be empty.", nameof(prefix));
        }

        if (Path.IsPathRooted(prefix) || HasDrivePrefix(prefix))
        {
            throw new ArgumentException(
                "Prefix must be a relative path.", nameof(prefix));
        }

        var segments = prefix.Split('/');
        foreach (var segment in segments)
        {
            if (segment is "" or "." or "..")
            {
                throw new ArgumentException(
                    "Prefix must be a relative path without traversal segments.",
                    nameof(prefix));
            }
        }
    }

    /// <summary>
    /// Creates a <see cref="PaxTarEntry"/> for a directory.
    /// </summary>
    /// <param name="entryName">The name of the tar entry.</param>
    /// <param name="directoryPath">The path of the directory on disk.</param>
    /// <param name="reproducible">
    /// Whether to use epoch timestamp for reproducibility.
    /// </param>
    /// <returns>
    /// A <see cref="PaxTarEntry"/> representing the directory.
    /// </returns>
    internal static PaxTarEntry CreateDirectoryEntry(
        string entryName, string directoryPath, bool reproducible)
    {
        if (!entryName.EndsWith('/'))
        {
            entryName += '/';
        }

        var dirInfo = new DirectoryInfo(directoryPath);
        var entry = new PaxTarEntry(TarEntryType.Directory, entryName)
        {
            Uid = 0,
            Gid = 0,
            Mode = GetUnixFileMode(dirInfo)
        };

        if (reproducible)
        {
            entry.ModificationTime = DateTimeOffset.UnixEpoch;
        }

        return entry;
    }

    /// <summary>
    /// Returns a hardcoded portable Unix file mode for a file system entry:
    /// rwxr-xr-x (755) for directories, rw-r--r-- (644) for files.
    /// </summary>
    /// <remarks>
    /// This method does not read actual filesystem permissions. It returns fixed
    /// modes suitable for cross-platform tar archives. The
    /// <paramref name="info"/> parameter is used only to distinguish directories
    /// from files via type check.
    /// </remarks>
    /// <param name="info">The file system info to determine the mode for.</param>
    /// <returns>The appropriate <see cref="UnixFileMode"/>.</returns>
    internal static UnixFileMode GetUnixFileMode(FileSystemInfo info)
    {
        if (info is DirectoryInfo)
        {
            return UnixFileMode.UserRead | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute;
        }

        return UnixFileMode.UserRead | UnixFileMode.UserWrite
            | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    }
}
