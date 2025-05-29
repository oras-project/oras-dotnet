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
