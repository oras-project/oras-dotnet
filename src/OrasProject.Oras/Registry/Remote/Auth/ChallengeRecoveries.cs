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

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Built-in <see cref="IChallengeRecovery"/> strategies.
/// </summary>
public static class ChallengeRecoveries
{
    /// <summary>
    /// Recovers by re-deriving the challenge from a credential-free request to the same URL. It
    /// applies only when the failed attempt carried a cached token (a stale-token rejection) and the
    /// request is replayable; the client then re-runs the recovered response through its standard
    /// flow. This is host-agnostic — it keys off the shape of the failure, not registry names.
    /// </summary>
    public static IChallengeRecovery ColdProbe { get; } = new ColdProbeChallengeRecovery();
}

/// <summary>
/// The built-in cold-probe recovery exposed by <see cref="ChallengeRecoveries.ColdProbe"/>: re-derives
/// the challenge from a credential-free request when the failed attempt carried a cached token and the
/// request is replayable; otherwise declines (returns <c>null</c>). Host-agnostic.
/// </summary>
internal sealed class ColdProbeChallengeRecovery : IChallengeRecovery
{
    /// <inheritdoc/>
    public async Task<HttpResponseMessage?> RecoverAsync(
        FailedChallenge context,
        CancellationToken cancellationToken = default)
        => context.AttachedCachedToken && context.CanReplay
            ? await context.ProbeWithoutAuthorizationAsync(cancellationToken).ConfigureAwait(false)
            : null;
}
