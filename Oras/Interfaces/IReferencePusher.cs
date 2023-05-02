using Oras.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces
{
    /// <summary>
    /// IReferencePusher provides advanced push with the tag service.
    /// </summary>
    internal interface IReferencePusher
    {
        /// <summary>
        /// PushReferenceAsync pushes the manifest with a reference tag.
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="descriptor"></param>
        /// <param name="content"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task PushReferenceAsync(string reference, Descriptor descriptor, Stream content, CancellationToken cancellationToken = default);
    }
}
