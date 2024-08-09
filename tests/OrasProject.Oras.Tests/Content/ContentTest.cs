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
using OrasProject.Oras.Exceptions;
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
        var calculateHelloWorldDigest = Digest.ComputeSHA256(content);
        Assert.Equal(helloWorldDigest, calculateHelloWorldDigest);
    }

    /// <summary>
    /// This method tests if the digest validation passes for registered algorithms
    /// </summary>
    [Theory]
    [InlineData("sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b")]
    [InlineData("sha512:401b09eab3c013d4ca54922bb802bec8fd5318192b0a75f201d8b372742c513925d98f76b340d9e59a4efdc45db9f5c640a21831b3d08be")]
    public void Validate_ReturnsDigest_ForRegisteredAlgorithms(string validDigest)
    {
        var result = Digest.Validate(validDigest);
        Assert.Equal(validDigest, result);
    }

    /// <summary>
    /// This method tests if the digest validation throws an exception for unregistered or unsupported algorithms
    /// </summary>
    [Theory]
    [InlineData("md5:098f6bcd4621d373cade4e832627b4f6")] // MD5, unregistered digest
    [InlineData("sha1:3b8b5a6b79f6d1114a7b7e95b3e3bc74dd1b6a2a")] // SHA-1, unregistered digest
    [InlineData("multihash+base58:QmRZxt2b1FVZPNqd8hsiykDL3TdBDeTSPX9Kv46HmX4Gx8")] // Multihash, unregistered digest
    public void Validate_ThrowsException_ForUnregisteredAlgorithms(string invalidDigest)
    {
        Assert.Throws<InvalidDigestException>(() => Digest.Validate(invalidDigest));
    }

    /// <summary>
    /// This method tests if the digest validation throws an exception for various invalid digest formats
    /// </summary>
    [Theory]
    [InlineData("sha256:")] // Missing encoded portion
    [InlineData("sha256+b64u!LCa0a2j_xo_5m0U8HTBBNBNCLXBkg7-g-YpeiGJm564")] // Invalid character in encoded portion
    public void Validate_ThrowsException_ForInvalidDigestFormats(string invalidDigest)
    {
        Assert.Throws<InvalidDigestException>(() => Digest.Validate(invalidDigest));
    }
}
