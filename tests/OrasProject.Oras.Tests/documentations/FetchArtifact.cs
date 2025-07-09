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

using System.Text.Json;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using Moq;
using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras.Oci;

public class FetchManifest
{
    public async Task FetchArtifactWithConfigAsync()
    {
        // This example demonstrates how to fetch a manifest by tag/digest from a remote repository.

        // Create a HttpClient instance to be used for making HTTP requests.
        var httpClient = new HttpClient();

        // Create a repository instance with the target registry.
        var mockCredentialProvider = new Mock<ICredentialProvider>();
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = new Client(httpClient, mockCredentialProvider.Object),
        });


        var cancellationToken = new CancellationToken();
        var reference = "foobar";
        // Fetch by tag
        var (descriptor, content) = await repo.FetchAsync(reference, cancellationToken);

        // Fetch blob content
        if (descriptor.MediaType == MediaType.ImageManifest) {
            var imageManifest = JsonSerializer.Deserialize<Manifest>(content) ??
                                            throw new JsonException("Failed to deserialize manifest");

            foreach (var layer in imageManifest.Layers) {
                var layerBlob = await repo.FetchAsync(layer.Digest, cancellationToken).ConfigureAwait(false);
            }
        }

        // Fetch by digest
        var digest = "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        var dataByDigest = await repo.FetchAsync(digest, cancellationToken);
    }
}
