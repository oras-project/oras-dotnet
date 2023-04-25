using Oras.Interfaces;
using Oras.Memory;
using Oras.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace OrasTest.MemoryTest
{
    public class MemoryTargetTest
    {
        [Fact]
        public void MemoryTarget_ImplementsStorage_True()
        {
            {
                var target = new MemoryTarget();
                Assert.IsAssignableFrom<IStorage>(target);
            }
        }
        /// <summary>
        /// This method tests if a MemoryTarget object can store data
        /// </summary>
        /// <returns></returns>
        [Fact]

        public async Task MemoryTarget_CanStoreData_True()
        {
            // Convert the "Hello World" string to a byte array
            var content = Encoding.UTF8.GetBytes("Hello World");

            // Create a buffer to hold the SHA256 hash
            var buffer = new byte[32];

            // Calculate the SHA256 hash of the content and store it in the buffer
            SHA256.HashData(content, buffer);

            // Convert the buffer to a hexadecimal string and prefix it with the name of the hashing algorithm
            var hash = $"{nameof(SHA256)}:{Convert.ToHexString(buffer)}";

            // Create a new Descriptor object to describe the content
            var descriptor = new Descriptor
            {
                MediaType = "test",
                Digest = hash,
                Size = content.Length
            };

            // Create a reference for the content
            var reference = "foobar";

            // Create a new MemoryTarget object
            var memoryTarget = new MemoryTarget();

            // Create a cancellation token
            var cancellationToken = new CancellationToken();

            // Create a MemoryStream object from the content byte array
            var stream = new MemoryStream(content);

            // Push the content to the MemoryTarget object
            await memoryTarget.PushAsync(descriptor, stream, cancellationToken);

            // Tag the content with the reference
            await memoryTarget.TagAsync(descriptor, reference, cancellationToken);

            // Resolve the reference to get the descriptor
            var gotDescriptor = await memoryTarget.ResolveAsync(reference, cancellationToken);

            // Assert that the descriptor is equal to the original descriptor
            Assert.Equal(descriptor, gotDescriptor);

            // Assert that the content exists in the MemoryTarget object
            Assert.True(await memoryTarget.ExistsAsync(descriptor, cancellationToken));

            // Fetch the content from the MemoryTarget object
            var readContent = await memoryTarget.FetchAsync(descriptor, cancellationToken);

            // Copy the fetched content to a new MemoryStream object
            using var memoryStream = new MemoryStream();
            readContent.CopyTo(memoryStream);

            // Convert the fetched content to a byte array
            var readContentBytes = memoryStream.ToArray();

            // Assert that the length of the fetched content is equal to the length of the original content
            Assert.Equal(content.Length, readContent.Length);
        }
    }
}
