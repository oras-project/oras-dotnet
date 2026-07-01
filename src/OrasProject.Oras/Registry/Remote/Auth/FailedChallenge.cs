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

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// The context passed to a <see cref="ChallengeRecoveryHandler"/>: an HTTP 401 whose challenge the
/// standard flow could not use, plus a credential-free probe primitive to re-derive a usable one.
/// This is plain data; it holds no client reference.
/// </summary>
public sealed class FailedChallenge
{
    private readonly Func<CancellationToken, Task<HttpResponseMessage>> _probe;

    internal FailedChallenge(
        HttpResponseMessage unauthorizedResponse,
        string host,
        bool attachedCachedToken,
        bool canReplay,
        Func<CancellationToken, Task<HttpResponseMessage>> probe)
    {
        UnauthorizedResponse = unauthorizedResponse;
        Host = host;
        AttachedCachedToken = attachedCachedToken;
        CanReplay = canReplay;
        _probe = probe;
    }

    /// <summary>The 401 response, including whatever (unusable) <c>WWW-Authenticate</c> it carried.</summary>
    public HttpResponseMessage UnauthorizedResponse { get; }

    /// <summary>The registry authority (host, with port when non-default) that issued the challenge.</summary>
    public string Host { get; }

    /// <summary>
    /// <c>true</c> when the failed attempt carried a cached token — i.e. this 401 is likely a
    /// stale-token rejection rather than a first-time challenge.
    /// </summary>
    public bool AttachedCachedToken { get; }

    /// <summary>
    /// <c>true</c> when the original request can be safely re-sent without authorization
    /// (an idempotent GET/HEAD). <see cref="ProbeWithoutAuthorizationAsync"/> requires it.
    /// </summary>
    public bool CanReplay { get; }

    /// <summary>
    /// Re-sends the original request with no <c>Authorization</c> header to elicit a fresh challenge.
    /// The returned response is owned by the caller and must be disposed (or returned to the client).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response to the credential-free request.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="CanReplay"/> is <c>false</c>.</exception>
    public Task<HttpResponseMessage> ProbeWithoutAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        if (!CanReplay)
        {
            throw new InvalidOperationException(
                "The original request cannot be safely replayed without authorization " +
                "because it is not an idempotent GET or HEAD request.");
        }

        return _probe(cancellationToken);
    }
}
