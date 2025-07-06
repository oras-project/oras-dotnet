
using OrasProject.Oras;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using System.Text;
using System.Text.Json;
using Xunit;

public class CopyManifest
{

    [Fact]
    public async Task CopyManifestAsync()
    {
        var blobs = new List<byte[]>();
        var descs = new List<Descriptor>();

        void AppendBlob(string mediaType, byte[] blob)
        {
            blobs.Add(blob);
            var desc = new Descriptor
            {
                MediaType = mediaType,
                Digest = Digest.ComputeSha256(blob),
                Size = blob.Length
            };
            descs.Add(desc);
        }

        void GenerateManifest(Descriptor config, List<Descriptor> layers)
        {
            var manifest = new Manifest
            {
                Config = config,
                Layers = layers
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
            AppendBlob(MediaType.ImageManifest, manifestBytes);
        }

        byte[] GetBytes(string data) => Encoding.UTF8.GetBytes(data);

        AppendBlob(MediaType.ImageConfig, GetBytes("config")); // blob 0
        AppendBlob(MediaType.ImageLayer, GetBytes("foo")); // blob 1
        AppendBlob(MediaType.ImageLayer, GetBytes("bar")); // blob 2
        GenerateManifest(descs[0], descs.GetRange(1, 2)); // blob 3

        #region Usage
        var sourceTarget = new MemoryStore();
        var destinationTarget = new MemoryStore();
        var cancellationToken = new CancellationToken();
        #endregion

        for (var i = 0; i < blobs.Count; i++)
        {
            await sourceTarget.PushAsync(descs[i], new MemoryStream(blobs[i]), cancellationToken);
        }

        var root = descs[3];

        #region Usage
        var reference = "foobar";
        #endregion
        await sourceTarget.TagAsync(root, reference, cancellationToken);

        #region Usage
        var gotDesc = await sourceTarget.CopyAsync(reference, destinationTarget, "", cancellationToken);
        #endregion
    }
}