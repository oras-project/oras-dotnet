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

public class PushImage
{
    public async Task PushImageAsync()
    {
        #region Usage
        // This example demonstrates how to push a manifest to a remote repository.

        // Create a HttpClient instance to be used for making HTTP requests.
        var httpClient = new HttpClient();
        var mockCredentialProvider = new Mock<ICredentialProvider>();
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = new Client(httpClient, mockCredentialProvider.Object),
        });

        var configBytes = new byte[] { 0x01, 0x02, 0x03 }; // Example config data
        var config = Descriptor.Create(
            configBytes,
            MediaType.ImageConfig
        );

        var layersBytes = new List<byte[]>
        {
            new byte[] { 0x04, 0x05, 0x06 }, // Example layer data
            new byte[] { 0x07, 0x08, 0x09 }  // Another layer data
        };
        var layers = new List<Descriptor>
        {
            Descriptor.Create(
                layersBytes[0],
                MediaType.ImageLayer
            ),
            Descriptor.Create(
                layersBytes[1],
                MediaType.ImageLayer
            )
        };

        // Push config and layers to the repository
        await repo.PushAsync(config, new MemoryStream(configBytes));
        for (int i = 0; i < layers.Count; i++)
        {
            await repo.PushAsync(layers[i], new MemoryStream(layersBytes[i]));

        }

        // Create a PackManifestOptions instance to specify the manifest configuration.
        var options = new PackManifestOptions
        {
            Config = config,
            Layers = layers
        };

        // Pack and push the manifest to the repository.
        var pushedDescriptor = await Packer.PackManifestAsync(
            repo,
            Packer.ManifestVersion.Version1_1,
            "",
            options);

        var tag = "tag";
        // Tag the pushed image.
        await repo.TagAsync(pushedDescriptor, tag);
        #endregion
    }
}
