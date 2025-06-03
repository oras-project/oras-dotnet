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

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OrasProject.Oras.Registry.Remote.Auth.Tests
{
    public class StaticCredentialHelperTests
    {
        [Fact]
        public void Constructor_NullRegistry_ThrowsArgumentException()
        {
            // Arrange
            string registry = null;
            var credential = new Credential()
            {
                Username = "user",
                Password = "password"
            };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new StaticCredentialHelper(registry, credential));
            Assert.Equal("registry", exception.ParamName);
        }

        [Fact]
        public void Constructor_EmptyRegistry_ThrowsArgumentException()
        {
            // Arrange
            string registry = "   ";
            var credential = new Credential()
            {
                Username = "user",
                Password = "password"
            };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new StaticCredentialHelper(registry, credential));
            Assert.Equal("registry", exception.ParamName);
        }

        [Fact]
        public void Constructor_EmptyCredential_ThrowsArgumentException()
        {
            // Arrange
            string registry = "example.com";
            var credential = CredentialExtensions.EmptyCredential;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new StaticCredentialHelper(registry, credential));
            Assert.Equal("credential", exception.ParamName);
        }

        [Fact]
        public async Task Constructor_DockerIoRegistry_RedirectsToRegistry1()
        {
            // Arrange
            string registry = "docker.io";
            var credential = new Credential()
            {
                Username = "user",
                Password = "password"
            };

            // Act
            var helper = new StaticCredentialHelper(registry, credential);

            // Assert - Test through ResolveAsync
            var result = await helper.ResolveAsync("registry-1.docker.io", CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(credential, result);
        }

        [Fact]
        public async Task ResolveAsync_MatchingHostname_ReturnsCredential()
        {
            // Arrange
            string registry = "example.com";
            var credential = new Credential()
            {
                Username = "user",
                Password = "password"
            };
            var helper = new StaticCredentialHelper(registry, credential);

            // Act
            var result = await helper.ResolveAsync("example.com", CancellationToken.None);

            // Assert
            Assert.Equal(credential, result);
        }

        [Fact]
        public async Task ResolveAsync_DifferentHostname_ReturnsEmptyCredential()
        {
            // Arrange
            string registry = "example.com";
            var credential = new Credential()
            {
                Username = "user",
                Password = "password"
            };
            var helper = new StaticCredentialHelper(registry, credential);

            // Act
            var result = await helper.ResolveAsync("different.com", CancellationToken.None);

            // Assert
            Assert.Equal(CredentialExtensions.EmptyCredential, result);
        }

        [Fact]
        public async Task ResolveAsync_CaseInsensitiveHostnameMatching()
        {
            // Arrange
            string registry = "ExAmPlE.CoM";
            var credential = new Credential()
            {
                Username = "user",
                Password = "password"
            };
            var helper = new StaticCredentialHelper(registry, credential);

            // Act
            var result = await helper.ResolveAsync("example.com", CancellationToken.None);

            // Assert
            Assert.Equal(credential, result);
        }
    }
}
