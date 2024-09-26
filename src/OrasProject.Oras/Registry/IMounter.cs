using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Registry;

/// <summary>
/// Mounter allows cross-repository blob mounts.
/// </summary>
public interface IMounter
{
    /// <summary>
    /// Mount makes the blob with the given descriptor in fromRepo
    /// available in the repository signified by the receiver.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="contentReference"></param>
    /// <param name="getContents"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task MountAsync(Descriptor descriptor, string contentReference, Func<CancellationToken, Task<Stream>>? getContents, CancellationToken cancellationToken);
}
