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
#region Usage
using System.Collections.Immutable;
using OrasProject.Oras.Registry.Remote.Auth;

namespace OrasProject.Oras.Examples.RealmValidation;

/// <summary>
/// Demonstrates configuring <see cref="DefaultRealmValidator"/> and
/// implementing a custom <see cref="IRealmValidator"/>.
/// </summary>
public static class RealmValidationExamples
{
    /// <summary>
    /// Uses the built-in <see cref="DefaultRealmValidator"/> with custom
    /// trusted hosts added for an enterprise environment.
    /// </summary>
    public static Client CreateClientWithCustomTrustedHosts(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        // DefaultRealmValidator ships with NO trusted hosts: by default
        // only realms on the same host as the registry are allowed. To
        // trust a registry whose auth realm lives on a different host,
        // list those auth hosts explicitly — for example your
        // organization's auth endpoints.
        //
        // Prefer an ImmutableHashSet<string> here: the trusted-host
        // allowlist is fixed configuration, and an immutable set makes
        // that intent explicit and is safe to share across clients.
        var validator = new DefaultRealmValidator
        {
            TrustedRealmHosts = ImmutableHashSet.Create(
                "login.mycompany.com",
                "auth.internal.example.com")
        };

        return new Client(httpClient)
        {
            RealmValidator = validator
        };
    }

    /// <summary>
    /// Opts into well-known public registries whose auth realm host
    /// differs from the registry host (Docker Hub, GitLab, NVIDIA NGC).
    /// These are intentionally NOT trusted by default — the SDK is
    /// host-agnostic — so callers enable them explicitly.
    /// </summary>
    public static Client CreateClientTrustingWellKnownPublicRegistries(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        var validator = new DefaultRealmValidator
        {
            TrustedRealmHosts = ImmutableHashSet.Create(
                "auth.docker.io",    // Docker Hub (registry-1.docker.io)
                "gitlab.com",        // GitLab (registry.gitlab.com)
                "authn.nvidia.com")  // NVIDIA NGC (nvcr.io)
        };

        return new Client(httpClient)
        {
            RealmValidator = validator
        };
    }

    /// <summary>
    /// Uses <see cref="DefaultRealmValidator"/> with insecure HTTP
    /// allowed — useful for local development with registries running
    /// on localhost without TLS.
    /// </summary>
    public static Client CreateClientForLocalDev(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        var validator = new DefaultRealmValidator
        {
            AllowInsecureHttp = true
        };

        return new Client(httpClient)
        {
            RealmValidator = validator
        };
    }

    /// <summary>
    /// Demonstrates a fully custom <see cref="IRealmValidator"/>
    /// that restricts realm URLs to a specific corporate domain
    /// suffix.
    /// </summary>
    public static Client CreateClientWithCustomValidator(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        return new Client(httpClient)
        {
            RealmValidator = new CorporateDomainRealmValidator(
                ".mycompany.com")
        };
    }
}

/// <summary>
/// A custom <see cref="IRealmValidator"/> that allows realm URLs only
/// when the host ends with a given corporate domain suffix.
/// </summary>
/// <remarks>
/// This is useful in locked-down environments where all auth endpoints
/// must reside within the corporate network.
/// </remarks>
public class CorporateDomainRealmValidator : IRealmValidator
{
    private readonly string _allowedSuffix;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="allowedDomainSuffix">
    /// The required domain suffix (e.g., ".mycompany.com").
    /// </param>
    public CorporateDomainRealmValidator(string allowedDomainSuffix)
    {
        if (string.IsNullOrWhiteSpace(allowedDomainSuffix))
        {
            throw new ArgumentException(
                "Allowed domain suffix cannot be null or whitespace.",
                nameof(allowedDomainSuffix));
        }

        _allowedSuffix = allowedDomainSuffix.ToLowerInvariant();
    }

    /// <inheritdoc/>
    public Task<bool> IsRealmAllowedAsync(
        Uri registryUri,
        Uri realmUri,
        CancellationToken cancellationToken = default)
    {
        // Only allow HTTPS.
        if (!realmUri.Scheme.Equals(
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        // Allow if the realm host ends with our corporate domain.
        var host = realmUri.Host.ToLowerInvariant();
        var allowed = host.EndsWith(_allowedSuffix, StringComparison.Ordinal)
            || string.Equals(host, _allowedSuffix.TrimStart('.'), StringComparison.Ordinal);

        return Task.FromResult(allowed);
    }
}
#endregion
