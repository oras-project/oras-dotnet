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
    
    public static Descriptor RandomDescriptor(string mediaType = MediaType.ImageManifest, string artifactType = "")
    {
        var randomBytes = RandomBytes();
        return new Descriptor
            { MediaType = mediaType, Digest = Digest.ComputeSha256(randomBytes), Size = randomBytes.Length, ArtifactType = artifactType };
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

    public static Index RandomIndex(IList<Descriptor>? manifests = null)
    {
        if (manifests == null)
        {
            manifests = new List<Descriptor>
            {
                RandomDescriptor(),
                RandomDescriptor(),
                RandomDescriptor(),
            };
        }
        return new Index()
        {
            Manifests = manifests,
            MediaType = MediaType.ImageIndex,
        };
    }
    
    
}
