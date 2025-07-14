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
using OrasProject.Oras;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote.Auth;
using Moq;

public class AttachReferrer
{
    public async Task AttachReferrerAsync()
    {
        #region Usage
        // This example demonstrates how to attach a referrer to an existing manifest.

        // Create a HttpClient instance to be used for making HTTP requests.
        var httpClient = new HttpClient();

        // Create a repository instance with the target registry.
        var mockCredentialProvider = new Mock<ICredentialProvider>();
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = new Client(httpClient, mockCredentialProvider.Object),
        });

        var targetReference = "target";

        // Resolve the target reference to get its descriptor.
        var targetDescriptor = await repo.ResolveAsync(targetReference);

        // Add annotations to the manifest.
        var artifactType = "doc/example";
        var annotations = new Dictionary<string, string>
        {
            { "org.opencontainers.image.created", "2000-01-01T00:00:00Z" },
            { "eol", "2025-07-01" }
        };

        var options = new PackManifestOptions
        {
            ManifestAnnotations = annotations,
            Subject = targetDescriptor,
        };

        // Pack the manifest with the specified artifact type and annotations and push it to the repository.
        await Packer.PackManifestAsync(repo, Packer.ManifestVersion.Version1_1, artifactType, options);
        #endregion
    }
}
