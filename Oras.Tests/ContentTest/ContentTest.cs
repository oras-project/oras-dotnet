using System;
using System.Collections.Generic;
using System.Linq;
using static Oras.Content.Content;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Oras.Tests.ContentTest
{
    public class ContentTest
    {
        /// <summary>
        /// This method tests if the digest is calculated properly
        /// </summary>
        [Fact]
        public void Content_EnsureThatTheDigestIsCalculatedProperly()
        {
            var helloWorldDigest = "sha256:11d4ddc357e0822968dbfd226b6e1c2aac018d076a54da4f65e1dc8180684ac3";
            var content = Encoding.UTF8.GetBytes("helloWorld");
            var calculateHelloWorldDigest = CalculateDigest(content);
            Assert.Equal(helloWorldDigest,calculateHelloWorldDigest);
        }
    }
}
