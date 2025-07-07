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
using Xunit;
using System.Net;
using OrasProject.Oras.Oci;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using static OrasProject.Oras.Content.Digest;

public class PushManifest
{
    [Fact]
    public async Task PushManifestWithConfigAsync()
    {
        var (_, expectedManifestBytes) = RandomManifest();
        var expectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(expectedManifestBytes),
            Size = expectedManifestBytes.Length
        };

        byte[]? receivedManifest = null;

        async Task<HttpResponseMessage> MockHttpRequestHandlerAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage
            {
                RequestMessage = req
            };
            if (req.Method == HttpMethod.Put &&
                req.RequestUri?.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}")
            {
                if (req.Headers.TryGetValues("Content-Type", out IEnumerable<string>? values))
                {
                    if (req.RequestUri.AbsolutePath == $"/v2/test/manifests/{expectedManifestDesc.Digest}" &&
                         !values.Contains(MediaType.ImageManifest))
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                }

                if (req.Content?.Headers?.ContentLength != null)
                {
                    var buf = new byte[req.Content.Headers.ContentLength.Value];
                    (await req.Content.ReadAsByteArrayAsync(cancellationToken)).CopyTo(buf, 0);
                    receivedManifest = buf;
                }

                res.StatusCode = HttpStatusCode.Created;
                return res;
            }
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = CustomClient(MockHttpRequestHandlerAsync),
            PlainHttp = true,
        });

        var cancellationToken = new CancellationToken();
        await repo.PushAsync(expectedManifestDesc, new MemoryStream(expectedManifestBytes), cancellationToken);
    }
}