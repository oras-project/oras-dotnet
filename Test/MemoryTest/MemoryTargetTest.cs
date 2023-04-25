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

        [Fact]
        public async Task MemoryTarget_CanStoreData_True()
        {
            var content = Encoding.UTF8.GetBytes("Hello World");
            var buffer =  new byte[32];
            SHA256.HashData(content, buffer);
            var hash = $"{nameof(SHA256)}:{Convert.ToHexString(buffer)}";
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
            try
            {
               await memoryTarget.PushAsync(descriptor, stream, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Assert.Fail(ex.Message);
            }
            return;
        }
    }
}
