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
using Azure.Identity;
using Azure.Core;
using Azure.Containers.ContainerRegistry;

namespace OrasProject.Oras.Tests.Examples;

// This is an example implementation of AzureCredentialProvider, which
// can be used to authenticate with Azure Container Registry. It uses
// DefaultAzureCredential with the fully qualified type name. It implements 
// the ICredentialProvider interface.
public class AzureCredentialProvider(string host) : ICredentialProvider
{
    public string Host { get; init; } = host;
    private string _aadToken { get; set; } = string.Empty;
    private Credential _credential { get; set; } = new Credential();
    private DateTimeOffset _tokenExpiry { get; set; } = DateTimeOffset.MinValue;
    private ContainerRegistryClient _acrClient { get; set; } = new ContainerRegistryClient(new Uri($"https://{host}"));

    private async Task<string> GetAadTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_aadToken) || DateTimeOffset.UtcNow >= _tokenExpiry)
        {
            var credential = new DefaultAzureCredential();
            string[] scopes = ["https://management.azure.com/.default"];
            var token = await credential.GetTokenAsync(new TokenRequestContext(scopes), cancellationToken).ConfigureAwait(false);
            _aadToken = token.Token;
            _tokenExpiry = token.ExpiresOn;
        }
        return _aadToken;
    }

    // Implement the ICredentialProvider interface.
    public async Task<Credential> ResolveCredentialAsync(string hostname, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(hostname))
        {
            throw new ArgumentException("Hostname cannot be null or empty.", nameof(hostname));
        }

        if (hostname != Host)
        {
            throw new ArgumentException($"Hostname '{hostname}' does not match the expected host '{Host}'.", nameof(hostname));
        }

        if (!_credential.IsEmpty() && DateTimeOffset.UtcNow < _tokenExpiry)
        {
            return _credential;
        }

        var aadToken = await GetAadTokenAsync(cancellationToken).ConfigureAwait(false);
        var response = await _acrClient.ExchangeAadAccessTokenForAcrRefreshTokenAsync(hostname, null, null, aadToken, cancellationToken);
        _credential = new Credential(RefreshToken: response.Value.RefreshToken);
        return _credential;
    }
}
#endregion
