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
using Xunit;

namespace OrasProject.Oras.Tests.Remote;

public class ReferenceTest
{
    [Theory]
    [InlineData("docker.io/repo", "registry-1.docker.io", "repo", "", "")]
    [InlineData("example.com/repo", "example.com", "repo", "", "")]
    [InlineData("example.com/repo:tag", "example.com", "repo", "tag", "")]
    [InlineData("example.com/repo@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b",
        "example.com",
        "repo",
        "",
        "sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b")]
    [InlineData("example.com/repo:tag@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b",
        "example.com",
        "repo",
        "",
        "sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b")]
    public void Reference_IsValid(string referenceString,
                                    string expectedHost,
                                    string expectedRepository,
                                    string expectedTag,
                                    string expectedDigest)
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
    [InlineData("/test")]
    [InlineData("test")]
    [InlineData("test:test")]
    [InlineData("-InvalidHost.com")]
    [InlineData("invalid host.com/repo:tag")]
    [InlineData("foo@example.com/repo:tag")]
    [InlineData("example.com/")]
    [InlineData("example.com/repo:")]
    [InlineData("example.com/InvalidRepo")]
    [InlineData("example.com/InvalidRepo:tag")]
    [InlineData("example.com/InvalidRepo@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b")]
    [InlineData("example.com/repo:tag$$")]
    [InlineData("example.com/repo@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b:tag")]
    [InlineData("example.com/repo@sha256:6c3c624b58dbbc$$")]
    public void Reference_NotValid(string referenceString)
    {
        Assert.Throws<InvalidReferenceException>(() => Reference.Parse(referenceString));
    }

    [Fact]
    public void Reference_InvalidPropertyAssignment()
    {
        var reference = Reference.Parse("example.com/repo:tag");
        Assert.Throws<InvalidReferenceException>(() => reference.Registry = "invalid registry");
        Assert.Throws<InvalidReferenceException>(() => reference.Repository = "invalid repo");
        Assert.Throws<InvalidReferenceException>(() => reference.ContentReference = "invalid tag");
    }

    [Fact]
    public void Reference_ValidPropertyAssignment()
    {
        var reference = Reference.Parse("foo.com/bar:baz");

        // null out properties  
        reference.Repository = null;
        reference.ContentReference = null;

        // Assign valid properties
        reference.Registry = "example.com";
        reference.Repository = "repo";
        reference.ContentReference = "sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b";

        // Confirm cloning
        var reference2 = Reference.Parse("example.com/repo@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b");
        var reference3 = new Reference(reference2);
        Assert.Equal(reference.Host, reference3.Host);
        Assert.Equal(reference.Repository, reference3.Repository);
        Assert.Equal(reference.Registry, reference3.Registry);
        Assert.Equal(reference.ContentReference, reference3.ContentReference);
    }
}
