using Oras.Content;
using Oras.Exceptions;
using Oras.Memory;
using Oras.Models;
using System.Text;
using Xunit;

namespace Oras.Tests.MemoryTest
{
    public class MemoryTargetTest
    {
        /// <summary>
        /// This method tests if a MemoryTarget object can store data
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task MemoryTarget_CanStoreData()
        {
            var content = Encoding.UTF8.GetBytes("Hello World");
            string hash = StorageUtility.CalculateHash(content);
            var descriptor = new Descriptor
            {
                MediaType = "test",
                Digest = hash,
                Size = content.Length
            };

            var reference = "foobar";
            var memoryTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var stream = new MemoryStream(content);
            await memoryTarget.PushAsync(descriptor, stream, cancellationToken);
            await memoryTarget.TagAsync(descriptor, reference, cancellationToken);
            var gotDescriptor = await memoryTarget.ResolveAsync(reference, cancellationToken);

            Assert.Equal(descriptor, gotDescriptor);
            Assert.True(await memoryTarget.ExistsAsync(descriptor, cancellationToken));

            var readContent = await memoryTarget.FetchAsync(descriptor, cancellationToken);
            using var memoryStream = new MemoryStream();
            readContent.CopyTo(memoryStream);

            // Assert that the fetched content is equal to the original content
            Assert.Equal(content, memoryStream.ToArray());
        }

        /// <summary>
        /// This method tests if a MemoryTarget object throws an exception when trying to fetch a non-existing descriptor
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task MemoryTarget_ThrowsNotFoundExceptionWhenDataIsNotAvailable()
        {
            var content = Encoding.UTF8.GetBytes("Hello World");
            string hash = StorageUtility.CalculateHash(content);
            var descriptor = new Descriptor
            {
                MediaType = "test",
                Digest = hash,
                Size = content.Length
            };

            var memoryTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var contentExists = await memoryTarget.ExistsAsync(descriptor, cancellationToken);
            Assert.False(contentExists);
            await Assert.ThrowsAsync<NotFoundException>(async () =>
             {
                 await memoryTarget.FetchAsync(descriptor, cancellationToken);
             });
        }

        /// <summary>
        /// This method tests if a MemoryTarget object throws an exception when trying to push an already existing data
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task MemoryTarget_ThrowsAlreadyExistsExceptionWhenSameDataIsPushedTwice()
        {
            var content = Encoding.UTF8.GetBytes("Hello World");
            string hash = StorageUtility.CalculateHash(content);
            var descriptor = new Descriptor
            {
                MediaType = "test",
                Digest = hash,
                Size = content.Length
            };

            var memoryTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var stream = new MemoryStream(content);
            await memoryTarget.PushAsync(descriptor, stream, cancellationToken);
            await Assert.ThrowsAsync<AlreadyExistsException>(() => memoryTarget.PushAsync(descriptor, stream, cancellationToken));
        }

        /// <summary>
        /// This method tests if a MemoryTarget object throws an exception when trying to push an artifact with a wrong descriptor
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task MemoryTarget_ThrowsAnErrorWhenABadPushOccurs()
        {
            var content = Encoding.UTF8.GetBytes("Hello World");
            var wrongContent = Encoding.UTF8.GetBytes("Hello World!");
            string hash = StorageUtility.CalculateHash(content);
            var descriptor = new Descriptor
            {
                MediaType = "test",
                Digest = hash,
                Size = content.Length
            };

            var memoryTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var stream = new MemoryStream(wrongContent);
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await memoryTarget.PushAsync(descriptor, stream, cancellationToken);
            });
        }
    }
}
