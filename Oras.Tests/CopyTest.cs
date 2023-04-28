using Oras.Constants;
using Oras.Memory;
using Oras.Models;
using System.Text;
using System.Text.Json;
using Xunit;
using static Oras.Content.Content;
using Index = Oras.Models.Index;

namespace Oras.Tests
{
    public class CopyTest
    {
        /// <summary>
        /// This method tests if a MemoryTarget object can be copied into another MemoryTarget object
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task MemoryTarget_CanCopyToAnotherMemoryTarget()
        {
            var sourceTarget = new MemoryTarget();
            var cancellationToken = new CancellationToken();
            var blobs = new List<byte[]>();
            var descs = new List<Descriptor>();
            var appendBlob = (string mediaType, byte[] blob) =>
            {
                blobs.Add(blob);
                var desc = new Descriptor
                {
                    MediaType = mediaType,
                    Digest = CalculateDigest(blob),
                    Size = blob.Length
                };
                descs.Add(desc);
            };
            var generateManifest = (Descriptor config, List<Descriptor> layers) =>
            {
                var manifest = new Manifest
                {
                    Config = config,
                    Layers = layers
                };
                var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
                appendBlob(OCIMediaTypes.ImageManifest, manifestBytes);
            };

            var generateIndex = (List<Descriptor> manifests) =>
            {
                var index = new Index
                {
                    Manifests = manifests
                };
                var indexBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(index));
                appendBlob(OCIMediaTypes.ImageIndex, indexBytes);
            };
            var getBytes = (string data) => Encoding.UTF8.GetBytes(data);
            appendBlob(OCIMediaTypes.ImageConfig, getBytes("config"));// blob 0
            appendBlob(OCIMediaTypes.ImageLayer, getBytes("foo"));// blob 1
            appendBlob(OCIMediaTypes.ImageLayer, getBytes("bar"));// blob 2
            generateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

            for (var i = 0; i < blobs.Count; i++)
            {
                await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);

            }
            var root = descs[3];
            var reference = "foobar";
            await sourceTarget.TagAsync(root, reference, cancellationToken);
            var destinationTarget = new MemoryTarget();
            var gotDesc = await Copy.CopyAsync(sourceTarget, reference, destinationTarget, "", cancellationToken);
            Assert.Equal(gotDesc, root);

            foreach (var des in descs)
            {
                await destinationTarget.ExistsAsync(des, cancellationToken);
            }
        }
    }
}
