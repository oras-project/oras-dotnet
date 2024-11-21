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
    public async Task InvalidDigestException()
    {
        await Assert.ThrowsAsync<InvalidDigestException>(() => throw new InvalidDigestException());
        await Assert.ThrowsAsync<InvalidDigestException>(() => throw new InvalidDigestException("Invalid digest"));
        await Assert.ThrowsAsync<InvalidDigestException>(() => throw new InvalidDigestException("Invalid digest", null));
    }

    [Fact]
    public async Task InvalidReferenceException()
    {
        await Assert.ThrowsAsync<InvalidReferenceException>(() => throw new InvalidReferenceException());
        await Assert.ThrowsAsync<InvalidReferenceException>(() => throw new InvalidReferenceException("Invalid reference"));
        await Assert.ThrowsAsync<InvalidReferenceException>(() => throw new InvalidReferenceException("Invalid reference", null));
    }

    [Fact]
    public async Task NotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => throw new NotFoundException());
        await Assert.ThrowsAsync<NotFoundException>(() => throw new NotFoundException("Not found"));
        await Assert.ThrowsAsync<NotFoundException>(() => throw new NotFoundException("Not found", null));
    }
    
    [Fact]
    public async Task NoReferrerUpdateException()
    {
        await Assert.ThrowsAsync<NoReferrerUpdateException>(() => throw new NoReferrerUpdateException());
        await Assert.ThrowsAsync<NoReferrerUpdateException>(() => throw new NoReferrerUpdateException("No referrer update"));
        await Assert.ThrowsAsync<NoReferrerUpdateException>(() => throw new NoReferrerUpdateException("No referrer update", null));
    }
    
    [Fact]
    public async Task ReferrersSupportLevelAlreadySetException()
    {
        await Assert.ThrowsAsync<ReferrersSupportLevelAlreadySetException>(() => throw new ReferrersSupportLevelAlreadySetException());
        await Assert.ThrowsAsync<ReferrersSupportLevelAlreadySetException>(() => throw new ReferrersSupportLevelAlreadySetException("Referrers support level has already been set"));
        await Assert.ThrowsAsync<ReferrersSupportLevelAlreadySetException>(() => throw new ReferrersSupportLevelAlreadySetException("Referrers support level has already been set", null));
    }
}
