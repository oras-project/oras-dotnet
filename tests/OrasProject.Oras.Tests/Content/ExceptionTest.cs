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
using Xunit;

namespace OrasProject.Oras.Tests.Content;

public class ExceptionTest
{
    [Fact]
    public async Task InvalidDescriptorSizeException()
    {
        await Assert.ThrowsAsync<InvalidDescriptorSizeException>(() => throw new InvalidDescriptorSizeException());
        await Assert.ThrowsAsync<InvalidDescriptorSizeException>(() => throw new InvalidDescriptorSizeException("Invalid descriptor size"));
        await Assert.ThrowsAsync<InvalidDescriptorSizeException>(() => throw new InvalidDescriptorSizeException("Invalid descriptor size", null));
    }

    [Fact]
    public async Task MismatchedDigestException()
    {
        await Assert.ThrowsAsync<MismatchedDigestException>(() => throw new MismatchedDigestException());
        await Assert.ThrowsAsync<MismatchedDigestException>(() => throw new MismatchedDigestException("Mismatched digest"));
        await Assert.ThrowsAsync<MismatchedDigestException>(() => throw new MismatchedDigestException("Mismatched digest", null));
    }

    [Fact]
    public async Task MismatchedSizeException()
    {
        await Assert.ThrowsAsync<MismatchedSizeException>(() => throw new MismatchedSizeException());
        await Assert.ThrowsAsync<MismatchedSizeException>(() => throw new MismatchedSizeException("Mismatched size"));
        await Assert.ThrowsAsync<MismatchedSizeException>(() => throw new MismatchedSizeException("Mismatched size", null));
    }
}
