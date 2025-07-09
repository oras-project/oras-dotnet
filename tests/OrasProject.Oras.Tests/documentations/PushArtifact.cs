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

using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras;
using Moq;


public class PushArtifact
{
    public async Task PushArtifactWithConfigAsync()
    {
        // This example demonstrates how to push an artifact to a remote repository.

        // Create a HttpClient instance to be used for making HTTP requests.
        var httpClient = new HttpClient();
        var mockCredentialProvider = new Mock<ICredentialProvider>();
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = new Client(httpClient, mockCredentialProvider.Object),
        });

        var cancellationToken = new CancellationToken();
        var artifactType = "doc/example";
        // Pack the artifact with the specified type and push it to the repository.
        await Packer.PackManifestAsync(repo, Packer.ManifestVersion.Version1_1, artifactType, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
