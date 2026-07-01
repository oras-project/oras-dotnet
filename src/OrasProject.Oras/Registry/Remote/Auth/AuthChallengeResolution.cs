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

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// The authorization an <see cref="IAuthChallengeHandler"/> produces to retry a request
/// that received an HTTP 401 (Unauthorized) response.
/// </summary>
/// <remarks>
/// A <c>null</c> result from
/// <see cref="IAuthChallengeHandler.ResolveAuthorizationAsync"/> means "give up" — the
/// client returns the original 401 response unchanged. A non-null result instructs the
/// client to cache the token (when <see cref="Cache"/> is <c>true</c>) and retry the
/// original request with the produced <c>Authorization</c> header.
/// </remarks>
public sealed class AuthChallengeResolution
{
    /// <summary>
    /// The authentication scheme of the produced authorization. Only
    /// <see cref="Challenge.Scheme.Basic"/> and <see cref="Challenge.Scheme.Bearer"/>
    /// are valid; any other value is treated as Bearer.
    /// </summary>
    public required Challenge.Scheme Scheme { get; init; }

    /// <summary>
    /// The credential parameter placed in the <c>Authorization</c> header: a base64
    /// <c>username:password</c> string for Basic, or the bearer token for Bearer.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// The scope key under which the client should cache <see cref="Token"/>. For Bearer
    /// this is the space-joined merged scope set; for Basic it is the empty string.
    /// Ignored when <see cref="Cache"/> is <c>false</c>.
    /// </summary>
    public string CacheScopeKey { get; init; } = string.Empty;

    /// <summary>
    /// Whether the client should cache <see cref="Token"/> for reuse on subsequent
    /// requests. Defaults to <c>true</c>.
    /// </summary>
    public bool Cache { get; init; } = true;
}
