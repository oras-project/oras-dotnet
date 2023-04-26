using Oras.Interfaces;
using Oras.Memory;
using Oras.Models;
using Oras.Content;
using System.Diagnostics;
using System.Security.Cryptography;
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

        public async Task MemoryTarget_CanStoreData_True()
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
    }
}
