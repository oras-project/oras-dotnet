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
using OrasProject.Oras.Content.Exceptions;
using System.Text;
using Xunit;

namespace OrasProject.Oras.Tests.Content;

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
        var calculateHelloWorldDigest = Digest.ComputeSha256(content);
        Assert.Equal(helloWorldDigest, calculateHelloWorldDigest);
    }

    /// <summary>
    /// This method tests if the digest validation passes for registered algorithms
    /// </summary>
    [Theory]
    [InlineData("sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b")]
    [InlineData("sha512:cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e")]
    public void Validate_ReturnsDigest_ForRegisteredAlgorithms(string validDigest)
    {
        var result = Digest.Validate(validDigest);
        Assert.Equal(validDigest, result);
    }

    /// <summary>
    /// This method tests if the digest validation passes for unrecognized algorithms
    /// that match the general digest grammar, per OCI image-spec v1.1.1:
    /// "Implementations SHOULD allow digests with unrecognized algorithms to pass
    /// validation if they comply with the above grammar."
    /// </summary>
    [Theory]
    [InlineData("md5:098f6bcd4621d373cade4e832627b4f6")]
    [InlineData("sha1:3b8b5a6b79f6d1114a7b7e95b3e3bc74dd1b6a2a")]
    [InlineData("multihash+base58:QmRZxt2b1FVZPNqd8hsiykDL3TdBDeTSPX9Kv46HmX4Gx8")]
    public void Validate_ReturnsDigest_ForUnrecognizedAlgorithms(string digest)
    {
        var result = Digest.Validate(digest);
        Assert.Equal(digest, result);
    }

    /// <summary>
    /// This method tests if the digest validation throws an exception for various invalid digest formats
    /// </summary>
    [Theory]
    [InlineData("")] // empty string
    [InlineData("sha256:")] // Missing encoded portion
    [InlineData("sha256+b64u!LCa0a2j_xo_5m0U8HTBBNBNCLXBkg7-g-YpeiGJm564")] // Invalid character in encoded portion
    [InlineData(" sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b")] // Space prefixed
    public void Validate_ThrowsException_ForInvalidDigestFormats(string invalidDigest)
    {
        Assert.Throws<InvalidDigestException>(() => Digest.Validate(invalidDigest));
    }
}
