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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Default implementation of <see cref="IRealmValidator"/> that validates
/// realm URLs using scheme, host, and trusted-host rules.
/// </summary>
/// <remarks>
/// <para>Validation rules (evaluated in order):</para>
/// <list type="number">
/// <item>Reject non-HTTP(S) schemes.</item>
/// <item>Reject HTTP unless <see cref="AllowInsecureHttp"/> is true.</item>
/// <item>Reject realm URLs containing userinfo.</item>
/// <item>Allow if realm host matches the registry host (same host).</item>
/// <item>Allow if realm host is in <see cref="TrustedRealmHosts"/>.</item>
/// <item>Default deny.</item>
/// </list>
/// </remarks>
public sealed class DefaultRealmValidator : IRealmValidator
{
    /// <summary>
    /// When <c>true</c>, allows realm URLs with the <c>http</c> scheme
    /// (for dev/testing only). Default: <c>false</c>.
    /// </summary>
    public bool AllowInsecureHttp { get; init; }

    /// <summary>
    /// Explicit set of trusted realm hostnames (case-insensitive).
    /// Realms matching these hosts are always allowed regardless of
    /// other rules.
    /// </summary>
    /// <remarks>
    /// Pre-populated with well-known registries whose auth realm
    /// host differs from the registry host:
    /// <list type="bullet">
    /// <item><c>auth.docker.io</c> — Docker Hub
    /// (registry: registry-1.docker.io)</item>
    /// <item><c>gitlab.com</c> — GitLab Container Registry
    /// (registry: registry.gitlab.com)</item>
    /// </list>
    /// </remarks>
    public ISet<string> TrustedRealmHosts { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "auth.docker.io",
            "gitlab.com"
        };

    /// <inheritdoc/>
    public Task<bool> IsRealmAllowedAsync(
        Uri registryUri,
        Uri realmUri,
        CancellationToken cancellationToken = default)
    {
        // 1. Reject non-HTTP(S) schemes.
        if (!IsHttpScheme(realmUri))
        {
            return Task.FromResult(false);
        }

        // 2. Reject HTTP unless explicitly allowed.
        if (!AllowInsecureHttp
            && string.Equals(
                realmUri.Scheme, "http",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        // 3. Reject userinfo in realm URL.
        if (!string.IsNullOrEmpty(realmUri.UserInfo))
        {
            return Task.FromResult(false);
        }

        // 4. Same host check (case-insensitive, port-aware).
        if (IsSameHost(registryUri, realmUri))
        {
            return Task.FromResult(true);
        }

        // 5. Trusted realm hosts.
        var realmHost = NormalizeHost(realmUri.Host);
        if (TrustedRealmHosts.Contains(realmHost))
        {
            return Task.FromResult(true);
        }

        // 6. Default deny.
        return Task.FromResult(false);
    }

    /// <summary>
    /// Returns <c>true</c> if the URI uses http or https.
    /// </summary>
    private static bool IsHttpScheme(Uri uri)
    {
        return string.Equals(
                   uri.Scheme, "https",
                   StringComparison.OrdinalIgnoreCase)
               || string.Equals(
                   uri.Scheme, "http",
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether two URIs refer to the same host and
    /// effective port.
    /// </summary>
    private static bool IsSameHost(Uri registryUri, Uri realmUri)
    {
        var regHost = NormalizeHost(registryUri.Host);
        var realmHost = NormalizeHost(realmUri.Host);

        if (!string.Equals(
                regHost, realmHost,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Compare effective ports. GetEffectivePort returns the
        // explicit port or the default for the scheme.
        var regPort = GetEffectivePort(registryUri);
        var realmPort = GetEffectivePort(realmUri);
        return regPort == realmPort;
    }

    /// <summary>
    /// Normalizes a hostname by lowercasing and stripping trailing
    /// dots.
    /// </summary>
    private static string NormalizeHost(string host)
    {
        return host.TrimEnd('.').ToLowerInvariant();
    }

    /// <summary>
    /// Returns the port for the URI. For http/https URIs,
    /// <see cref="Uri.Port"/> always returns the effective port
    /// (explicit or scheme-default).
    /// </summary>
    private static int GetEffectivePort(Uri uri) => uri.Port;
}
