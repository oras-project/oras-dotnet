using System.Text;
using System.Text.Json;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using Index = OrasProject.Oras.Oci.Index;

namespace OrasProject.Oras.Tests.Remote.Util;

public class RandomDataGenerator
{
    public static int RandomInt(int min, int max)
    {
        return new Random().Next(min, max);
    }
    
    public static string RandomString()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var length = RandomInt(1, chars.Length);
        char[] stringChars = new char[length];
        for (int i = 0; i < length; ++i)
        {
            stringChars[i] = chars[RandomInt(0, chars.Length)];
        }
        return new string(stringChars);
    }
    
    public static Descriptor RandomDescriptor(string mediaType = MediaType.ImageManifest)
    {
        var randomBytes = RandomBytes();
        return new Descriptor
            { MediaType = mediaType, Digest = Digest.ComputeSHA256(randomBytes), Size = randomBytes.Length };
    }

    public static (Manifest, byte[]) RandomManifest()
    {
        var manifest = new Manifest
        {
            Layers = new List<Descriptor>(),
            Config = new Descriptor{MediaType = MediaType.ImageConfig, Digest = Guid.NewGuid().ToString("N")},
        };
        return (manifest, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest)));
    }
    
    public static (Manifest, byte[]) RandomManifestWithSubject(Descriptor? subject = null)
    {
        var manifest = new Manifest
        {
            Layers = new List<Descriptor>(),
            Config = new Descriptor{MediaType = MediaType.ImageConfig, Digest = Guid.NewGuid().ToString("N")},
        };
        if (subject == null) manifest.Subject = RandomDescriptor();
        else manifest.Subject = subject;
        return (manifest, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest)));
    }

    public static byte[] RandomBytes()
    {
        return Encoding.UTF8.GetBytes(RandomString());
    }

    public static Index RandomIndex()
    {
        return new Index()
        {
            Manifests = new List<Descriptor>
            {
                RandomDescriptor(),
                RandomDescriptor(),
            },
            MediaType = MediaType.ImageIndex,
        };
    }
    
    
}
