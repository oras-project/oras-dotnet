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

using Xunit;

namespace OrasProject.Oras.Registry.Remote.Auth.Tests
{
    public class SingleRegistryCredentialProviderTests
    {
        [Fact]
        public void Constructor_NullRegistry_ThrowsArgumentException()
        {
            // Arrange
            string? registry = null;
            var credential = new Credential()
            {
                Username = "user",
                Password = "password"
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new SingleRegistryCredentialProvider(registry!, credential));
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
            Assert.Throws<ArgumentException>(() => new SingleRegistryCredentialProvider(registry, credential));
        }

        [Fact]
        public void Constructor_EmptyCredential_ThrowsArgumentException()
        {
            // Arrange
            string registry = "example.com";
            var credential = CredentialExtensions.EmptyCredential;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new SingleRegistryCredentialProvider(registry, credential));
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
            var helper = new SingleRegistryCredentialProvider(registry, credential);

            // Assert - Test through ResolveAsync
            var result = await helper.ResolveCredentialAsync("registry-1.docker.io", CancellationToken.None);
            Assert.Equal(credential, result);
        }

        [Fact]
        public async Task Constructor_DockerIoRegistry_RedirectsToRegistry1_CaseInsensitive()
        {
            // Arrange
            string registry = "docKeR.IO";
            var credential = new Credential()
            {
                Username = "user",
                Password = "password"
            };

            // Act
            var helper = new SingleRegistryCredentialProvider(registry, credential);

            // Assert - Test through ResolveAsync
            var result = await helper.ResolveCredentialAsync("registry-1.docker.io", CancellationToken.None);
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
            var helper = new SingleRegistryCredentialProvider(registry, credential);

            // Act
            var result = await helper.ResolveCredentialAsync("example.com", CancellationToken.None);

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
            var helper = new SingleRegistryCredentialProvider(registry, credential);

            // Act
            var result = await helper.ResolveCredentialAsync("different.com", CancellationToken.None);

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
            var helper = new SingleRegistryCredentialProvider(registry, credential);

            // Act
            var result = await helper.ResolveCredentialAsync("example.com", CancellationToken.None);

            // Assert
            Assert.Equal(credential, result);
        }

        [Fact]
        public async Task ResolveAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            string registry = "example.com";
            var credential = new Credential()
            {
                Username = "user",
                Password = "password"
            };
            var helper = new SingleRegistryCredentialProvider(registry, credential);

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel the token before calling the method

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => helper.ResolveCredentialAsync("example.com", cts.Token));
        }
    }
}
