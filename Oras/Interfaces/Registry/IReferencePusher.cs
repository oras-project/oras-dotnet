using Oras.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces.Registry
{
    /// <summary>
    /// IReferencePusher provides advanced push with the tag service.
    /// </summary>
    public interface IReferencePusher
    {
        /// <summary>
        /// PushReferenceAsync pushes the manifest with a reference tag.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="content"></param>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task PushReferenceAsync(Descriptor descriptor, Stream content, string reference, CancellationToken cancellationToken = default);
    }
}
