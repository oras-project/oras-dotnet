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

using OrasProject.Oras.Content.File.Exceptions;
using Xunit;

namespace OrasProject.Oras.Tests.Content.File.Exceptions;

public class ExceptionTest
{
    [Fact]
    public async Task FileStoreClosedException()
    {
        await Assert.ThrowsAsync<FileStoreClosedException>(
            () => throw new FileStoreClosedException());
        await Assert.ThrowsAsync<FileStoreClosedException>(
            () => throw new FileStoreClosedException(
                "Store already closed"));
        await Assert.ThrowsAsync<FileStoreClosedException>(
            () => throw new FileStoreClosedException(
                "Store already closed", null));
    }

    [Fact]
    public async Task DuplicateNameException()
    {
        await Assert.ThrowsAsync<DuplicateNameException>(
            () => throw new DuplicateNameException());
        await Assert.ThrowsAsync<DuplicateNameException>(
            () => throw new DuplicateNameException(
                "Duplicate name"));
        await Assert.ThrowsAsync<DuplicateNameException>(
            () => throw new DuplicateNameException(
                "Duplicate name", null));
    }

    [Fact]
    public async Task MissingNameException()
    {
        await Assert.ThrowsAsync<MissingNameException>(
            () => throw new MissingNameException());
        await Assert.ThrowsAsync<MissingNameException>(
            () => throw new MissingNameException(
                "Missing name"));
        await Assert.ThrowsAsync<MissingNameException>(
            () => throw new MissingNameException(
                "Missing name", null));
    }

    [Fact]
    public async Task OverwriteDisallowedException()
    {
        await Assert.ThrowsAsync<
            OverwriteDisallowedException>(
            () => throw new
                OverwriteDisallowedException());
        await Assert.ThrowsAsync<
            OverwriteDisallowedException>(
            () => throw new
                OverwriteDisallowedException(
                    "Overwrite disallowed"));
        await Assert.ThrowsAsync<
            OverwriteDisallowedException>(
            () => throw new
                OverwriteDisallowedException(
                    "Overwrite disallowed", null));
    }

    [Fact]
    public async Task PathTraversalDisallowedException()
    {
        await Assert.ThrowsAsync<
            PathTraversalDisallowedException>(
            () => throw new
                PathTraversalDisallowedException());
        await Assert.ThrowsAsync<
            PathTraversalDisallowedException>(
            () => throw new
                PathTraversalDisallowedException(
                    "Path traversal disallowed"));
        await Assert.ThrowsAsync<
            PathTraversalDisallowedException>(
            () => throw new
                PathTraversalDisallowedException(
                    "Path traversal disallowed",
                    null));
    }
}
