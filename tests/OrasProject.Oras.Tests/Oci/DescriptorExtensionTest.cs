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
using OrasProject.Oras.Oci;
using Xunit;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;
namespace OrasProject.Oras.Tests.Oci
{
    public class DescriptorExtensionTest
    {
        [Fact]
        public void LimitSize_ShouldThrowException_WhenSizeExceedsLimit()
        {
            var desc = RandomDescriptor();
            desc.Size = 150;
            long limitSize = 100;

            var exception = Assert.Throws<SizeLimitExceededException>(() => desc.LimitSize(limitSize));
            Assert.Equal("content size 150 exceeds MaxMetadataBytes 100", exception.Message);
        }

        [Fact]
        public void LimitSize_ShouldNotThrowException_WhenSizeIsWithinLimit()
        {
            var desc = RandomDescriptor();
            desc.Size = 50;
            long limitSize = 100;

            var exception = Record.Exception(() => desc.LimitSize(limitSize));
            Assert.Null(exception); // No exception should be thrown
        }
    }
}
