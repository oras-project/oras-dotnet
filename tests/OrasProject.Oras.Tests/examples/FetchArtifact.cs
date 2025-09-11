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
using System.Text.Json;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Content;
using Microsoft.Extensions.Caching.Memory;

namespace OrasProject.Oras.Tests.Examples;

public static class FetchArtifact
{
    // This example demonstrates how to fetch an artifact by tag/digest from a remote repository.
    // For production use: Implement proper exception handling, cancellation, and dependency injection.
    public static async Task FetchArtifactAsync()
    {
        const string registry = "localhost:5000"; // change to your target registry
        const string repository = "myrepo/test"; // change to your target repository
        const string reference = "foobar"; // the tag or digest of the artifact to fetch, such as "latest" or "sha256:abc123...".

        // Create a HttpClient instance for making HTTP requests.
        var httpClient = new HttpClient();

        // Create a simple credential provider with static credentials.
        var credentialProvider = new SingleRegistryCredentialProvider(registry, new Credential
        {
            RefreshToken = "refresh_token" // change to your actual refresh token
        });

        // Create a memory cache for caching access tokens to improve auth performance.
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        // Create a repository instance to interact with the target repository.
        var repo = new Repository(new RepositoryOptions
        {
            Reference = Reference.Parse($"{registry}/{repository}"),
            Client = new Client(httpClient, credentialProvider, new Cache(memoryCache)),
        });

        // Fetch manifest content and read it with validation
        var (manifestDescriptor, manifestStream) = await repo.FetchAsync(reference);
        byte[] manifestBytes;
        using (manifestStream)
        {
            manifestBytes = await manifestStream.ReadAllAsync(manifestDescriptor);
        }
        if (manifestDescriptor.MediaType == MediaType.ImageManifest)
        {
            // Parse the manifest JSON and fetch each layer
            var imageManifest = JsonSerializer.Deserialize<Manifest>(manifestBytes)
                ?? throw new JsonException("Failed to deserialize manifest");
            foreach (var layer in imageManifest.Layers)
            {
                var layerData = await repo.FetchAllAsync(layer);
            }
        }
    }
}
#endregion
