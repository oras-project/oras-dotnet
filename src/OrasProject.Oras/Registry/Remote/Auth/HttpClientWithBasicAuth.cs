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
using System.Net.Http.Headers;
using System.Text;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// HttpClientWithBasicAuth adds the Basic Auth Scheme to the Authorization Header
/// </summary>
public class HttpClientWithBasicAuth : HttpClient
{
    public HttpClientWithBasicAuth(string username, string password) => Initialize(username, password);

    public HttpClientWithBasicAuth(string username, string password, HttpMessageHandler handler) : base(handler)
        => Initialize(username, password);

    private void Initialize(string username, string password)
    {
        this.AddUserAgent();
        DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
    }
}
