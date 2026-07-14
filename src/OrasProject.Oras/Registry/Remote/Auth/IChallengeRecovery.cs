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
/// Attempts to recover from an HTTP 401 whose challenge the standard authentication flow could not
/// use — for example a registry that omits the <c>WWW-Authenticate</c> challenge when a stale token
/// is presented, or one whose challenge is followed to its token endpoint only for that endpoint to
/// reject the token request with a 401 (a challenge derived from a stale cached token that the
/// registry cannot honor). In both cases a credential-free request to the same URL yields a usable
/// challenge.
/// </summary>
/// <remarks>
/// <para>
/// The client consults a recovery handler whenever standard resolution of a 401 fails to produce a
/// usable challenge — for <em>every</em> such 401, regardless of whether the request carried a cached
/// token or is replayable. The handler is expected to <b>self-gate</b>: inspect
/// <see cref="FailedChallenge.AttachedCachedToken"/> and <see cref="FailedChallenge.CanReplay"/> and
/// return <c>null</c> when recovery does not apply. (The built-in
/// <see cref="ChallengeRecoveries.ColdProbe"/> recovers only when both are <c>true</c>.) Genuine
/// credential failures, and any non-401 token-endpoint failure, are surfaced as exceptions
/// <em>before</em> any handler is consulted, so recovery cannot mask a real authentication error. The
/// one token-endpoint outcome offered to recovery is a <b>401</b> from following the challenge's realm
/// — the stale-token failure this seam exists to tolerate; if recovery declines, that exact exception
/// is rethrown unchanged.
/// </para>
/// <para>
/// Return a replacement response for the client to continue from — typically the result of
/// <see cref="FailedChallenge.ProbeWithoutAuthorizationAsync"/>. If that response is itself a fresh
/// 401, the client re-runs it through the standard flow once (so a recovered challenge is
/// fetched/cached normally); if it is a success, it is returned as-is. Return <c>null</c> to give up,
/// in which case the client falls back to its default behavior for the original 401.
/// </para>
/// <para>See <see cref="ChallengeRecoveries.ColdProbe"/> for the built-in cold-probe recovery.</para>
/// </remarks>
public interface IChallengeRecovery
{
    /// <summary>
    /// Attempts to recover from the failed 401 challenge described by <paramref name="context"/>,
    /// returning a replacement response for the client to continue from, or <c>null</c> to give up.
    /// </summary>
    /// <param name="context">The failed 401 exchange, plus a credential-free probe primitive.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A replacement response to continue from, or <c>null</c> to give up.</returns>
    Task<HttpResponseMessage?> RecoverAsync(
        FailedChallenge context,
        CancellationToken cancellationToken = default);
}
