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

namespace OrasProject.Oras.Examples.Azure;

public static class AzureContainerRegistryAuthentication
{
    // This example demonstrates how to use the ICredentialProvider interface to
    // authenticate with Azure Container Registry, and perform a copy operation
    // between two ACR repositories.
    // For production use: Implement proper exception handling, cancellation, and dependency injection.
    public static async Task AuthenticateWithAzureContainerRegistry()
    {
        var httpClient = new HttpClient();

        // Create a repository instance for the source repository.
        var srcCredentialProvider = new AzureCredentialProvider("mysourceregistry.azurecr.io");
        var srcRepository = new Repository(new RepositoryOptions
        {
            Reference = Reference.Parse("mysourceregistry.azurecr.io/myimage"),
            Client = new Client(httpClient, credentialProvider: srcCredentialProvider),
        });

        // Create a repository instance for the destination repository.
        var dstCredentialProvider = new AzureCredentialProvider("mydestinationregistry.azurecr.io");
        var dstRepository = new Repository(new RepositoryOptions
        {
            Reference = Reference.Parse("mydestinationregistry.azurecr.io/myimage"),
            Client = new Client(httpClient, credentialProvider: dstCredentialProvider)
        });

        // Copy the artifact tagged by v1.1.0 from the source repository to the destination
        // repository, and tag it with v1.
        var srcReference = "v1.1.0";
        var dstReference = "v1";
        await srcRepository.CopyAsync(srcReference, dstRepository, dstReference);
    }
}
#endregion
