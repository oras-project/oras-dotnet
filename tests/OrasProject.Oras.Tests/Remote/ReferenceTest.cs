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
using OrasProject.Oras.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace OrasProject.Oras.Tests.Remote;

public class ReferenceTest
{

    [Theory]
    [InlineData("example.com/repo:tag", "example.com", "repo", "tag", "")]
    [InlineData("example.com/repo@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b", "example.com", "repo", "", "sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b")]
    public void Reference_IsValid(string referenceString, string expectedHost, string expectedRepository, string expectedTag, string expectedDigest)
    {
        var reference = Reference.Parse(referenceString);
        Assert.Equal(expectedHost, reference.Host);
        Assert.Equal(expectedRepository, reference.Repository);
        if (expectedDigest == "") Assert.Throws<InvalidReferenceException>(() => reference.Digest);
        else Assert.Equal(expectedDigest, reference.Digest);
        if (expectedTag == "") Assert.Throws<InvalidReferenceException>(() => reference.Tag);
        else Assert.Equal(expectedTag, reference.Tag);
    }


    [Theory]
    [InlineData("")]
    [InlineData("test")]
    [InlineData("test:test")]
    public void Reference_NotValid(string referenceString)
    {
        // Assert InvalidReferenceException
        Assert.Throws<InvalidReferenceException>(() => Reference.Parse(referenceString));
    }
}
