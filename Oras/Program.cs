using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Oras.Remote;

namespace Oras
{
    internal class Program
    {
        public static async Task Main()
        {
            //var repo = new Repository("localhost:5000/my-artifact:v1");
            var repo = new Repository(new ReferenceObj()
            {
                Reference = "v1",
                Repository = "my-artifact",
                Registry = "localhost:5000",
                
            }, new RepositoryOption()
            {
                PlainHTTP = true,
                Client = new HttpClient()
            });
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            await repo.PingAsync(cancellationToken);
            var sha = "sha256:5461789af9cae6b426f19a4983cfd2cb8bbc4f0bd50fdaee9d3c8682c701787e";
            var sha2 = "sha256:91121276c3a3dbded7a94978dacaad6d992ba491921fa6f425d704c8ccc1a677";
            var resp = await repo.Blobs().FetchReferenceAsync(sha2, cancellationToken);
            var bytes = new byte[resp.Descriptor.Size];
            await bytes.Stream.ReadAsync(bytes, 0, (int)resp.Descriptor.Size, cancellationToken);
            // encode the byte to unicode
            var str = System.Text.Encoding.UTF8.GetString(bytes);
            Console.WriteLine(str);


        }
    }
}
