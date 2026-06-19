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
    public static Client CreateClientWithCustomTrustedHosts()
    {
        // DefaultRealmValidator ships with auth.docker.io and
        // gitlab.com pre-configured. You can override the trusted
        // hosts to include your organization's auth endpoints.
        var validator = new DefaultRealmValidator
        {
            TrustedRealmHosts = new HashSet<string>
            {
                "auth.docker.io",
                "gitlab.com",
                "login.mycompany.com",
                "auth.internal.example.com",
            }
        };

        return new Client(new HttpClient())
        {
            RealmValidator = validator
        };
    }

    /// <summary>
    /// Uses <see cref="DefaultRealmValidator"/> with insecure HTTP
    /// allowed — useful for local development with registries running
    /// on localhost without TLS.
    /// </summary>
    public static Client CreateClientForLocalDev()
    {
        var validator = new DefaultRealmValidator
        {
            AllowInsecureHttp = true
        };

        return new Client(new HttpClient())
        {
            RealmValidator = validator
        };
    }

    /// <summary>
    /// Demonstrates a fully custom <see cref="IRealmValidator"/>
    /// that restricts realm URLs to a specific corporate domain
    /// suffix.
    /// </summary>
    public static Client CreateClientWithCustomValidator()
    {
        return new Client(new HttpClient())
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
        var allowed = host.EndsWith(_allowedSuffix)
            || host == _allowedSuffix.TrimStart('.');

        return Task.FromResult(allowed);
    }
}
#endregion
