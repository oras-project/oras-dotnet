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
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry.Remote.Auth;
using Microsoft.Extensions.Caching.Memory;

namespace OrasProject.Oras.Tests.Examples;

public static class PushArtifact
{
    // This example demonstrates how to push an artifact to a remote repository.
    // For production use: Implement proper exception handling, cancellation, and dependency injection.
    public static async Task PushArtifactAsync()
    {
        const string registry = "localhost:5000"; // change to your target registry
        const string repository = "myrepo/test"; // change to your target repository

        // Create a HttpClient instance for making HTTP requests.
        var httpClient = new HttpClient();

        // Create a simple credential provider with static credentials.
        var credentialProvider = new SingleRegistryCredentialProvider(registry, new Credential
        {
            RefreshToken = "refresh_token"
        });

        // Create a memory cache for caching access tokens to improve auth performance.
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        // Create a repository instance to interact with the target repository.
        var repo = new Repository(new RepositoryOptions
        {
            Reference = Reference.Parse($"{registry}/{repository}"),
            Client = new Client(httpClient, credentialProvider, new Cache(memoryCache)),
        });

        // Push layers to the repository.
        var layersBytes = new List<byte[]>
        {
            new byte[] { 0x04, 0x05, 0x06 }, // example layer data
        };
        var layers = new List<Descriptor>
        {
            Descriptor.Create(layersBytes[0], MediaType.ImageLayer)
        };
        for (int i = 0; i < layers.Count; i++)
        {
            await repo.PushAsync(layers[i], new MemoryStream(layersBytes[i]));
        }

        // Pack a manifest for the artifact and push it to the repository.
        var options = new PackManifestOptions
        {
            Layers = layers
        };
        const string artifactType = "doc/example"; // choose an appropriate media type for your artifact
        var manifestDescriptor = await Packer.PackManifestAsync(repo, Packer.ManifestVersion.Version1_1, artifactType, options);

        // Tag the pushed manifest so it can be referenced by a human-readable name.
        const string tag = "tag"; // choose a tag for your artifact
        await repo.TagAsync(manifestDescriptor, tag);
    }
}
#endregion
