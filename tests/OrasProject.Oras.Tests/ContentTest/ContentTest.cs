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

using System.Text;
using Xunit;
using static OrasProject.Oras.Content.Content;

namespace OrasProject.Oras.Tests.ContentTest
{
    public class CalculateDigest
    {
        /// <summary>
        /// This method tests if the digest is calculated properly
        /// </summary>
        [Fact]
        public void VerifiesIfDigestMatches()
        {
            var helloWorldDigest = "sha256:11d4ddc357e0822968dbfd226b6e1c2aac018d076a54da4f65e1dc8180684ac3";
            var content = Encoding.UTF8.GetBytes("helloWorld");
            var calculateHelloWorldDigest = CalculateDigest(content);
            Assert.Equal(helloWorldDigest, calculateHelloWorldDigest);
        }
    }
}
