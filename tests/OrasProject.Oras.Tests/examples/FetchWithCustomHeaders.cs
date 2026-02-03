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

public static class FetchWithCustomHeaders
{
    // This example demonstrates how to fetch an artifact with custom HTTP headers.
    // Custom headers can be used for tracing, correlation IDs, or additional authentication.
    // For production use: Implement proper exception handling, cancellation, and dependency injection.
    public static async Task FetchWithCustomHeadersAsync()
    {
        const string registry = "localhost:5000"; // change to your target registry
        const string repository = "myrepo/test"; // change to your target repository
        const string reference = "latest"; // the tag or digest of the artifact to fetch

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

        // Create FetchOptions with custom headers.
        // These headers will be included in all HTTP requests made during the fetch operation.
        var fetchOptions = new FetchOptions
        {
            Headers = new Dictionary<string, IEnumerable<string>>
            {
                // Add a correlation ID for distributed tracing
                { "X-Correlation-Id", new[] { Guid.NewGuid().ToString() } },
                // Add custom metadata headers
                { "X-Custom-Client", new[] { "oras-dotnet-example" } }
            }
        };

        // Fetch manifest content with custom headers
        // Note: ReadAllAsync buffers the entire content in memory. This is appropriate for
        // manifests which are typically small (< 1MB). For large blobs, use streaming instead.
        var (manifestDescriptor, manifestStream) = await repo.FetchAsync(reference, fetchOptions);
        byte[] manifestBytes;
        using (manifestStream)
        {
            manifestBytes = await manifestStream.ReadAllAsync(manifestDescriptor);
        }

        // Create ResolveOptions with the same custom headers for resolve operations.
        var resolveOptions = new ResolveOptions
        {
            Headers = new Dictionary<string, IEnumerable<string>>
            {
                { "X-Correlation-Id", new[] { Guid.NewGuid().ToString() } }
            }
        };

        // Resolve a reference to get its descriptor without fetching content
        var descriptor = await repo.Manifests.ResolveAsync(reference, resolveOptions);

        if (manifestDescriptor.MediaType == MediaType.ImageManifest)
        {
            // Parse the manifest JSON and fetch each layer with custom headers
            var imageManifest = JsonSerializer.Deserialize<Manifest>(manifestBytes)
                ?? throw new JsonException("Failed to deserialize manifest");
            foreach (var layer in imageManifest.Layers)
            {
                // Fetch blob with custom headers
                var (layerDescriptor, layerStream) = await repo.Blobs.FetchAsync(
                    layer.Digest,
                    fetchOptions);

                // Stream to file to avoid loading large blobs into memory
                await using (layerStream)
                {
                    var fileName = $"layer-{layerDescriptor.Digest.Replace(":", "-")}.bin";
                    await using var fileStream = File.Create(fileName);
                    await layerStream.CopyToAsync(fileStream);
                }
            }
        }
    }
}
#endregion
