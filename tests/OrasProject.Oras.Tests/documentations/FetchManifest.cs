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
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using System.Net;
using Xunit;
using static OrasProject.Oras.Content.Digest;
using static OrasProject.Oras.Tests.Remote.Util.Util;

public class FetchManifest
{

    [Fact]
    public async Task FetchManifestWithConfigAsync()
    {
        var manifest = """{"layers":[]}"""u8.ToArray();
        var manifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(manifest),
            Size = manifest.Length
        };
        var reference = "foobar";

        HttpResponseMessage MockHandlerMockHandler(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method != HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }
            if (req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{manifestDesc.Digest}" || req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{reference}")
            {
                if (req.Headers.TryGetValues("Accept", out IEnumerable<string>? values) && !values.Contains(MediaType.ImageManifest))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                res.Content = new ByteArrayContent(manifest);
                res.Content.Headers.Add("Content-Type", [MediaType.ImageManifest]);
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = CustomClient(MockHandlerMockHandler),
            PlainHttp = true,
        });
        var cancellationToken = new CancellationToken();

        var dataRef = await repo.FetchAsync(reference, cancellationToken);
        var dataDigest = await repo.FetchAsync(manifestDesc.Digest, cancellationToken);
    }
}