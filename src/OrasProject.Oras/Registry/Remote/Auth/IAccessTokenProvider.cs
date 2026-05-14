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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Provides pre-resolved access tokens for registry authentication challenges.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="ICredentialProvider"/>, which returns raw credentials that the
/// <see cref="Client"/> exchanges at a token endpoint, an <see cref="IAccessTokenProvider"/>
/// returns a ready-to-use access token.  This is useful when token acquisition is handled
/// by an external service (e.g., a gRPC token service) that owns the credential exchange.
/// </para>
/// <para>
/// When both <see cref="IAccessTokenProvider"/> and <see cref="ICredentialProvider"/> are
/// configured on a <see cref="Client"/>, the access-token provider is tried first.  If it
/// returns <c>null</c>, the client falls through to credential-based authentication.
/// </para>
/// </remarks>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Resolves an access token for the given registry authentication challenge.
    /// </summary>
    /// <param name="hostname">The registry hostname (authority) that issued the challenge.</param>
    /// <param name="realm">The authentication realm URL from the WWW-Authenticate header.</param>
    /// <param name="service">The service identifier from the WWW-Authenticate header.</param>
    /// <param name="scopes">The requested access scopes.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The access token string, or <c>null</c> if this provider cannot resolve a token
    /// for the given parameters (in which case the client falls through to credential-based
    /// authentication).
    /// </returns>
    Task<string?> ResolveAccessTokenAsync(
        string hostname,
        string realm,
        string service,
        IList<string> scopes,
        CancellationToken cancellationToken = default);
}
