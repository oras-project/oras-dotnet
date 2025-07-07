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

using OrasProject.Oras.Oci;
using OrasProject.Oras;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;
using static OrasProject.Oras.Content.Digest;
using System.Net;
using System.Text.Json;
using System.Text;
using Xunit;

public class AttachReferrer
{

    [Fact]
    public async Task AttachReferrerAsync()
    {
        #region Usage
        var artifactType = "doc/example";
        var (_, targetManifestBytes) = RandomManifest();
        var targetManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(targetManifestBytes),
            Size = targetManifestBytes.Length
        };
        var annotations = new Dictionary<string, string>
        {
            { "org.opencontainers.image.created", "2000-01-01T00:00:00Z" },
            { "eol", "2025-07-01" }
        };

        var manifest = new Manifest
        {
            SchemaVersion = 2,
            MediaType = MediaType.ImageManifest,
            ArtifactType = artifactType,
            Subject = targetManifestDesc,
            Config = Descriptor.Empty,
            Layers = [Descriptor.Empty],
            Annotations = annotations
        };
        #endregion

        var uuid = Guid.NewGuid().ToString();

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method == HttpMethod.Put &&
                req.RequestUri?.AbsolutePath.Contains($"/v2/test/manifests") == true)
            {
                res.Headers.Add("Docker-Content-Digest", [ComputeSha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest)))]);
                res.Headers.Add("OCI-Subject", "test");
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }

            if (req.Method == HttpMethod.Post && req.RequestUri?.AbsolutePath == "/v2/test/blobs/uploads/")
            {
                res.StatusCode = HttpStatusCode.Accepted;
                res.Headers.Add("Location", $"/v2/test/blobs/uploads/{uuid}");
                return res;
            }

            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == $"/v2/test/blobs/uploads/{uuid}")
            {
                res.StatusCode = HttpStatusCode.Created;
                return res;
            }

            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        #region Usage
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = CustomClient(MockHttpRequestHandler),
            PlainHttp = true,
        });


        var options = new PackManifestOptions
        {
            ManifestAnnotations = annotations,
            Subject = targetManifestDesc,
        };

        var cancellationToken = new CancellationToken();
        await Packer.PackManifestAsync(repo, Packer.ManifestVersion.Version1_1, artifactType, options, cancellationToken);
        #endregion
    }
}