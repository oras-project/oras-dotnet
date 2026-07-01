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
/// is presented, or returns one whose realm points at a different host.
/// </summary>
/// <remarks>
/// <para>
/// The client consults a recovery handler only after standard resolution of the 401 has failed to
/// produce a usable challenge, and only for a request that carried a cached token
/// (<see cref="FailedChallenge.AttachedCachedToken"/>) and is replayable
/// (<see cref="FailedChallenge.CanReplay"/>). Credential and token-endpoint failures are surfaced as
/// exceptions and never reach a recovery handler, so recovery cannot mask a real authentication error.
/// </para>
/// <para>
/// Return a replacement response for the client to continue from — typically the result of
/// <see cref="FailedChallenge.ProbeWithoutAuthorizationAsync"/>, which the client re-runs through the
/// standard flow (so a recovered challenge is fetched/cached normally, and an already-successful
/// response is returned as-is) — or <c>null</c> to give up, in which case the client falls back to its
/// default behavior for the original 401.
/// </para>
/// <para>See <see cref="ChallengeRecoveries.ColdProbe"/> for the built-in cold-probe recovery.</para>
/// </remarks>
/// <param name="context">The failed 401 exchange, plus a credential-free probe primitive.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>A replacement response to continue from, or <c>null</c> to give up.</returns>
public delegate Task<HttpResponseMessage?> ChallengeRecoveryHandler(
    FailedChallenge context,
    CancellationToken cancellationToken);
