using Oras.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces
{
    /// <summary>
    /// IStorage represents a content-addressable storage (CAS) where contents are accessed via Descriptors.
    /// The storage is designed to handle blobs of large sizes.
    /// </summary>
    public interface IStorage : IReadOnlyStorage
    {
        /// <summary>
        /// PushAsync pushes the content, matching the expected descriptor.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="content"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task PushAsync(Descriptor expected, Stream content, CancellationToken cancellationToken = default);
    }

}
