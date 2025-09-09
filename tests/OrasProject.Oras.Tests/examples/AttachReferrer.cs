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
using OrasProject.Oras.Registry.Remote.Auth;
using Microsoft.Extensions.Caching.Memory;

namespace OrasProject.Oras.Tests.Examples;

public static class AttachReferrer
{
    // This example demonstrates a basic implementation of attaching a referrer manifest to an existing subject manifest.
    // Cancellation tokens and exception handling are omitted for simplicity.
    public static async Task AttachReferrerAsync()
    {
        const string registry = "localhost:5000";
        const string repository = "myrepo/test";

        // Create a HttpClient instance for making HTTP requests.
        var httpClient = new HttpClient();

        // Create a simple credential provider with static credentials.
        var credentialProvider = new SingleRegistryCredentialProvider(registry, new Credential
        {
            Username = "username",
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

        // Resolve the target reference to get its descriptor.
        const string subjectReference = "target"; // could also be a digest like "sha256:..."
        var subjectDescriptor = await repo.ResolveAsync(subjectReference);

        // Pack the manifest with the specified artifact type and annotations and push it to the repository.
        var annotations = new Dictionary<string, string>
        {
            ["org.opencontainers.image.created"] = "2000-01-01T00:00:00Z",
            ["eol"] = "2025-07-01"
        };
        var options = new PackManifestOptions
        {
            ManifestAnnotations = annotations,
            Subject = subjectDescriptor, // set subject to make this manifest a referrer
        };
        const string artifactType = "doc/example";
        await Packer.PackManifestAsync(repo, Packer.ManifestVersion.Version1_1, artifactType, options);
    }
}
#endregion
