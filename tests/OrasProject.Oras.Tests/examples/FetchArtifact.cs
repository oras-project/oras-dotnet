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
using OrasProject.Oras.Content;

public class FetchArtifact
{
    public async Task FetchArtifactAsync()
    {
        #region Usage
        // This example demonstrates how to fetch an artifact by tag/digest from a remote repository.

        // Create a HttpClient instance to be used for making HTTP requests.
        var httpClient = new HttpClient();

        // Create a repository instance with the target registry.
        var mockCredentialProvider = new Mock<ICredentialProvider>();
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = new Client(httpClient, mockCredentialProvider.Object),
        });

        var reference = "foobar"; // The tag or digest of the artifact to fetch, such as "latest" or "sha256:abc123...".
        var (manifestDescriptor, manifestContent) = await repo.FetchAsync(reference);

        // Verify received content against received descriptor
        var manifestData = await manifestContent.ReadAllAsync(manifestDescriptor);

        // Fetch blob content
        if (manifestDescriptor.MediaType == MediaType.ImageManifest)
        {
            var imageManifest = JsonSerializer.Deserialize<Manifest>(manifestData) ??
                                            throw new JsonException("Failed to deserialize manifest");

            foreach (var layer in imageManifest.Layers)
            {
                var layerBlob = await repo.FetchAllAsync(layer);
            }
        }
        #endregion
    }
}
