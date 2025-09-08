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
#region Usage
using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry;
using Microsoft.Extensions.Caching.Memory;

namespace OrasProject.Oras.Tests.Examples;

public static class CopyArtifact
{
    // This example demonstrates a basic implementation of copying an artifact by tag/digest from one repository to another.
    // Cancellation tokens and exception handling are omitted for simplicity.
    public static async Task CopyArtifactAsync()
    {
        const string srcRegistry = "source.io";
        const string srcRepository = "src/test";
        const string dstRegistry = "target.io";
        const string dstRepository = "dst/test";

        // Create a HttpClient instance for making HTTP requests.
        var httpClient = new HttpClient();

        // Create simple credential providers with static credentials.
        var srcCredential = new SingleRegistryCredentialProvider(srcRegistry, new Credential
        {
            Username = "src-user",
            RefreshToken = "src-refresh-token"
        });
        var dstCredential = new SingleRegistryCredentialProvider(dstRegistry, new Credential
        {
            Username = "dst-user",
            RefreshToken = "dst-refresh-token"
        });

        // Create a memory cache for caching access tokens to improve auth performance.
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        // Create repository instances to interact with the source and destination repositories.
        var sourceRepository = new Repository(new RepositoryOptions
        {
            Reference = Reference.Parse($"{srcRegistry}/{srcRepository}"),
            Client = new Client(httpClient, srcCredential, new Cache(memoryCache)),
        });
        var destinationRepository = new Repository(new RepositoryOptions
        {
            Reference = Reference.Parse($"{dstRegistry}/{dstRepository}"),
            Client = new Client(httpClient, dstCredential, new Cache(memoryCache)),
        });

        // Copy the artifact identified by reference from the source repository to the destination
        const string reference = "tag"; // could also be a digest like "sha256:..."
        var copiedRoot = await sourceRepository.CopyAsync(reference, destinationRepository, "");
    }
}
#endregion
