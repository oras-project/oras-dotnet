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
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace OrasProject.Oras.Examples.ExternalTokenBroker;

/// <summary>
/// Demonstrates using <see cref="IAccessTokenProvider"/> with an
/// external token broker to pull an artifact without raw credentials
/// entering the process.
/// </summary>
public static class ExternalTokenBrokerAuthentication
{
    /// <summary>
    /// Pulls an artifact using an external broker for token acquisition.
    /// </summary>
    public static async Task PullWithExternalBrokerAsync(
        CancellationToken cancellationToken = default)
    {
        // Configure the broker HTTP client.
        // In production this might have mTLS, API keys, etc.
        using var brokerClient = new HttpClient
        {
            BaseAddress = new Uri("https://broker.internal.example.com")
        };

        // Create the access-token provider backed by the broker.
        var tokenProvider = new ExternalBrokerTokenProvider(
            brokerClient, "/api/v1/token");

        // Build the ORAS auth client with the provider.
        // No ICredentialProvider is needed — the broker handles
        // credential exchange externally.
        using var httpClient = new HttpClient();
        var authClient = new Client(httpClient)
        {
            AccessTokenProvider = tokenProvider
        };

        // Create a repository and pull the artifact.
        var repository = new Repository(new RepositoryOptions
        {
            Reference = Reference.Parse(
                "myregistry.example.com/myrepo:v1"),
            Client = authClient,
        });

        var descriptor = await repository.ResolveAsync(
            "v1", cancellationToken);

        // Use descriptor.Digest, descriptor.Size, etc.
        Console.WriteLine(
            $"Resolved: {descriptor.Digest} ({descriptor.Size} bytes)");
    }
}
#endregion
