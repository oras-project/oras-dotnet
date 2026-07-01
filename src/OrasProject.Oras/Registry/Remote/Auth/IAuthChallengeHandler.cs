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

using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Resolves the authorization used to retry a request that received an HTTP 401
/// (Unauthorized) response, given the server's <c>WWW-Authenticate</c> challenge.
/// </summary>
/// <remarks>
/// <para>
/// This is the pluggable seam for token acquisition. <see cref="Client"/> attaches any
/// cached token and sends the first request itself; when that returns 401 it delegates the
/// "challenge → authorization" decision to this handler, then performs the retry send and
/// caching using the returned <see cref="AuthChallengeResolution"/>.
/// </para>
/// <para>
/// The default implementation, <see cref="DefaultAuthChallengeHandler"/>, performs the
/// standard OCI distribution flow (parse challenge, validate realm, fetch a Basic or Bearer
/// token). Consumers can supply a custom handler to recover from non-conformant registries
/// whose token-carrying 401 is unusable — for example a missing challenge, or a challenge
/// pointing at a different host — by issuing a credential-free probe via
/// <see cref="AuthChallengeContext.SendWithoutAuthorizationAsync"/> to re-derive a usable
/// challenge.
/// </para>
/// <para>
/// Implementations must be thread-safe (a single handler may serve concurrent requests) and
/// must not retain the supplied <see cref="AuthChallengeContext"/> beyond the call.
/// </para>
/// <para>
/// Note: before invoking this handler on a Bearer challenge, the client may try a token it has
/// already cached under the challenge's (merged) scope — a cache optimization that can satisfy
/// the request without calling the handler. The handler is always invoked when that cached
/// token is absent or itself rejected.
/// </para>
/// </remarks>
/// <seealso href="https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#authorization">
/// OCI Distribution Spec — Authorization
/// </seealso>
public interface IAuthChallengeHandler
{
    /// <summary>
    /// Resolves the authorization to retry the original request with.
    /// </summary>
    /// <param name="context">
    /// The context for the unauthorized request, exposing the original request, the 401
    /// response, and capabilities to fetch tokens, validate realms, merge scopes, and probe
    /// the registry without credentials.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The authorization to retry with, or <c>null</c> to give up — in which case the client
    /// returns the original 401 response unchanged.
    /// </returns>
    Task<AuthChallengeResolution?> ResolveAuthorizationAsync(
        AuthChallengeContext context,
        CancellationToken cancellationToken = default);
}
