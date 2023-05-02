using Oras.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Oras.Interfaces
{
    /// <summary>
    /// IReferrerLister provides the Referrers API.
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.0-rc1/spec.md#listing-referrers
    /// </summary>
    internal interface IReferrerLister
    {
        Task ReferrersAsync(Descriptor desc, string artifactType, Func<Descriptor[], Task> fn, CancellationToken cancellationToken = default);
    }
}
