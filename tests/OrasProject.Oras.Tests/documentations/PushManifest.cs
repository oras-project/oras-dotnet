using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry;
using OrasProject.Oras;
using Moq;
using Xunit;
using System.Net;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using static OrasProject.Oras.Content.Digest;

public class PushManifest
{
    [Fact]
    public async Task PushManifestWithConfigAsync()
    {
        #region Usage
        var (_, expectedManifestBytes) = RandomManifest();
        var expectedManifestDesc = new Descriptor
        {
            MediaType = MediaType.ImageManifest,
            Digest = ComputeSha256(expectedManifestBytes),
            Size = expectedManifestBytes.Length
        };
        #endregion

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

        #region Usage
        var repo = new Repository(new RepositoryOptions()
        {
            Reference = Reference.Parse("localhost:5000/test"),
            Client = CustomClient(MockHttpRequestHandlerAsync),
            PlainHttp = true,
        });

        var cancellationToken = new CancellationToken();
        await repo.PushAsync(expectedManifestDesc, new MemoryStream(expectedManifestBytes), cancellationToken);
        #endregion
    }
}