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

using OrasProject.Oras.Registry.Remote.Exceptions;
using Xunit;

namespace OrasProject.Oras.Tests.Remote;

public class ExceptionTest
{
    [Fact]
    public async Task ReferrersSupportLevelAlreadySetException()
    {
        await Assert.ThrowsAsync<ReferrersStateAlreadySetException>(() => throw new ReferrersStateAlreadySetException());
        await Assert.ThrowsAsync<ReferrersStateAlreadySetException>(() => throw new ReferrersStateAlreadySetException("Referrers state has already been set"));
        await Assert.ThrowsAsync<ReferrersStateAlreadySetException>(() => throw new ReferrersStateAlreadySetException("Referrers state has already been set", null));
    }

    [Fact]
    public async Task InvalidResponseException()
    {
        await Assert.ThrowsAsync<InvalidResponseException>(() => throw new InvalidResponseException());
        await Assert.ThrowsAsync<InvalidResponseException>(() =>
            throw new InvalidResponseException("Invalid response"));
        await Assert.ThrowsAsync<InvalidResponseException>(() =>
            throw new InvalidResponseException("Invalid response", null));
    }
}
