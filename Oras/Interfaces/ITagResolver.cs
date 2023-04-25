﻿using Oras.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Oras.Interfaces
{
    /// <summary>
    /// ITagResolver provides reference tag indexing services.
    /// </summary>
    public interface ITagResolver : IResolver
    {
        /// <summary>
        /// TagAsync tags the descriptor with the reference.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="reference"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default);
    }
}
