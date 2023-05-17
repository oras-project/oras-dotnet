using System;
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
                Registry = "localhost:5000"
            }, new RepositoryOption()
            {
                PlainHTTP = true
            });
            repo.PlainHTTP = true;
            var cancellationToken = new CancellationToken();
            await repo.PingAsync(cancellationToken);
            Console.WriteLine("Worked");
        }
    }
}
