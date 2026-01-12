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
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace OrasProject.Oras.Tests.Examples;

public static class GetBlobLocation
{
    // This example demonstrates how to retrieve a blob location URL from a remote registry.
    // Most OCI 1.0 compatible registries return a redirect with a blob location in the header
    // instead of returning the content directly. This API captures that location URL.
    // For production use: Implement proper exception handling, cancellation, and dependency injection.
    public static async Task GetBlobLocationAsync()
    {
        const string registry = "localhost:5000"; // change to your target registry
        const string repository = "myrepo/test"; // change to your target repository

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

        // Create a descriptor for the blob you want to get the location for
        byte[] bytes = Encoding.UTF8.GetBytes("test");
        string digest = Digest.ComputeSha256(bytes);
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageLayer,
            Size = bytes.Length,
            Digest = digest
        };

        // Get the blob location URL
        // Returns null if the registry returns content directly (no redirect)
        // Returns the redirect location URL if the registry uses redirects
        var locationUrl = await repo.Blobs.GetBlobLocationAsync(descriptor);

        if (locationUrl != null)
        {
            // Note: Be careful logging redirect URLs as they may contain temporary credentials
            // or pre-signed tokens. Consider sanitizing sensitive query parameters before logging.
            Console.WriteLine($"Blob redirect location: {locationUrl}");
            // You can now use this URL to download the blob directly from the storage backend
            // (e.g., S3, Azure Blob Storage, etc.) without going through the registry
        }
        else
        {
            Console.WriteLine("Registry returns blob content directly (no redirect)");
            // In this case, you should use the regular FetchAsync method
            var stream = await repo.Blobs.FetchAsync(descriptor);
        }
    }
}
#endregion
