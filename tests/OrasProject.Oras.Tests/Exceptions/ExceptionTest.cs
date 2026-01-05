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

using OrasProject.Oras.Exceptions;
using Xunit;

namespace OrasProject.Oras.Tests.Exceptions;

public class ExceptionTest
{
    [Fact]
    public async Task AlreadyExistsException()
    {
        await Assert.ThrowsAsync<AlreadyExistsException>(() => throw new AlreadyExistsException());
        await Assert.ThrowsAsync<AlreadyExistsException>(() => throw new AlreadyExistsException("Already exists"));
        await Assert.ThrowsAsync<AlreadyExistsException>(() => throw new AlreadyExistsException("Already exists", null));
    }

    [Fact]
    public async Task NotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => throw new NotFoundException());
        await Assert.ThrowsAsync<NotFoundException>(() => throw new NotFoundException("Not found"));
        await Assert.ThrowsAsync<NotFoundException>(() => throw new NotFoundException("Not found", null));
    }

    [Fact]
    public async Task SizeLimitExceededException()
    {
        await Assert.ThrowsAsync<SizeLimitExceededException>(() => throw new SizeLimitExceededException());
        await Assert.ThrowsAsync<SizeLimitExceededException>(() => throw new SizeLimitExceededException("Size limit exceeded"));
        await Assert.ThrowsAsync<SizeLimitExceededException>(() => throw new SizeLimitExceededException("Size limit exceeded", null));
    }

    [Fact]
    public async Task MissingArtifactTypeException()
    {
        await Assert.ThrowsAsync<MissingArtifactTypeException>(() => throw new MissingArtifactTypeException());
        await Assert.ThrowsAsync<MissingArtifactTypeException>(() => throw new MissingArtifactTypeException("Missing artifact type"));
        await Assert.ThrowsAsync<MissingArtifactTypeException>(() => throw new MissingArtifactTypeException("Missing artifact type", null));
    }

    [Fact]
    public async Task InvalidMediaTypeException()
    {
        await Assert.ThrowsAsync<InvalidMediaTypeException>(() => throw new InvalidMediaTypeException());
        await Assert.ThrowsAsync<InvalidMediaTypeException>(() => throw new InvalidMediaTypeException("Invalid media type"));
        await Assert.ThrowsAsync<InvalidMediaTypeException>(() => throw new InvalidMediaTypeException("Invalid media type", null));
    }

    [Fact]
    public async Task InvalidDateTimeFormatException()
    {
        await Assert.ThrowsAsync<InvalidDateTimeFormatException>(() => throw new InvalidDateTimeFormatException());
        await Assert.ThrowsAsync<InvalidDateTimeFormatException>(() => throw new InvalidDateTimeFormatException("Invalid date time format"));
        await Assert.ThrowsAsync<InvalidDateTimeFormatException>(() => throw new InvalidDateTimeFormatException("Invalid date time format", null));
    }

    [Fact]
    public async Task DuplicateNameException()
    {
        await Assert.ThrowsAsync<DuplicateNameException>(() => throw new DuplicateNameException());
        await Assert.ThrowsAsync<DuplicateNameException>(() => throw new DuplicateNameException("Duplicate name"));
        await Assert.ThrowsAsync<DuplicateNameException>(() => throw new DuplicateNameException("Duplicate name", null));
    }

    [Fact]
    public async Task MissingNameException()
    {
        await Assert.ThrowsAsync<MissingNameException>(() => throw new MissingNameException());
        await Assert.ThrowsAsync<MissingNameException>(() => throw new MissingNameException("Missing name"));
        await Assert.ThrowsAsync<MissingNameException>(() => throw new MissingNameException("Missing name", null));
    }

    [Fact]
    public async Task MissingReferenceException()
    {
        await Assert.ThrowsAsync<MissingReferenceException>(() => throw new MissingReferenceException());
        await Assert.ThrowsAsync<MissingReferenceException>(() => throw new MissingReferenceException("Missing reference"));
        await Assert.ThrowsAsync<MissingReferenceException>(() => throw new MissingReferenceException("Missing reference", null));
    }

    [Fact]
    public async Task StoreClosedException()
    {
        await Assert.ThrowsAsync<StoreClosedException>(() => throw new StoreClosedException());
        await Assert.ThrowsAsync<StoreClosedException>(() => throw new StoreClosedException("Store closed"));
        await Assert.ThrowsAsync<StoreClosedException>(() => throw new StoreClosedException("Store closed", null));
    }
}
