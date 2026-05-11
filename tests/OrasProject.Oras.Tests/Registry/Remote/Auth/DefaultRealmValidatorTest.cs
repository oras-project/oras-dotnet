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
using System.Threading.Tasks;
using OrasProject.Oras.Registry.Remote.Auth;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote.Auth;

public class DefaultRealmValidatorTest
{
    private static Uri Reg(string url) => new(url);
    private static Uri Realm(string url) => new(url);

    #region Must ALLOW

    [Fact]
    public async Task SameHostExactly_Allowed()
    {
        var validator = new DefaultRealmValidator();
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://myreg.io/v2/"),
            Realm("https://myreg.io/token")));
    }

    [Fact]
    public async Task SameHostDifferentPath_Allowed()
    {
        var validator = new DefaultRealmValidator();
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://myreg.io/v2/"),
            Realm("https://myreg.io/v2/auth")));
    }

    [Fact]
    public async Task SameHostDefaultPort_Allowed()
    {
        var validator = new DefaultRealmValidator();
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://myreg.io:443/v2/"),
            Realm("https://myreg.io/token")));
    }

    [Fact]
    public async Task TrustedHost_Allowed()
    {
        var validator = new DefaultRealmValidator
        {
            TrustedRealmHosts = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                "auth.example.com"
            }
        };
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://registry.example.com/v2/"),
            Realm("https://auth.example.com/token")));
    }

    [Fact]
    public async Task CaseInsensitiveHost_Allowed()
    {
        var validator = new DefaultRealmValidator();
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://MyReg.IO/v2/"),
            Realm("https://myreg.io/token")));
    }

    [Fact]
    public async Task HttpRealm_AllowedWhenInsecureEnabled()
    {
        var validator = new DefaultRealmValidator
        {
            AllowInsecureHttp = true
        };
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("http://localhost/v2/"),
            Realm("http://localhost/token")));
    }

    [Fact]
    public async Task IpLiteralSameHost_Allowed()
    {
        var validator = new DefaultRealmValidator();
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://192.168.1.5/v2/"),
            Realm("https://192.168.1.5/token")));
    }

    [Fact]
    public async Task SameHostExplicitSamePort_Allowed()
    {
        var validator = new DefaultRealmValidator();
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://myreg.io:8443/v2/"),
            Realm("https://myreg.io:8443/token")));
    }

    [Fact]
    public async Task TrailingDotNormalized_Allowed()
    {
        var validator = new DefaultRealmValidator();
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://myreg.io./v2/"),
            Realm("https://myreg.io/token")));
    }

    #endregion

    #region Must REJECT

    [Fact]
    public async Task DifferentDomain_Rejected()
    {
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://victim.io/v2/"),
            Realm("https://evil.com/token")));
    }

    [Fact]
    public async Task HttpScheme_RejectedByDefault()
    {
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://victim.io/v2/"),
            Realm("http://victim.io/token")));
    }

    [Fact]
    public async Task NonHttpScheme_Rejected()
    {
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://victim.io/v2/"),
            Realm("file:///etc/passwd")));
    }

    [Fact]
    public async Task UserinfoInRealm_Rejected()
    {
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://victim.io/v2/"),
            Realm("https://user:pass@victim.io/token")));
    }

    [Fact]
    public async Task SubdomainTrick_Rejected()
    {
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://victim.io/v2/"),
            Realm("https://victim.io.evil.com/token")));
    }

    [Fact]
    public async Task RegistryHostAsPathSegment_Rejected()
    {
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://victim.io/v2/"),
            Realm("https://evil.com/victim.io/token")));
    }

    [Fact]
    public async Task DifferentIpLiteral_Rejected()
    {
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://192.168.1.5/v2/"),
            Realm("https://192.168.1.6/token")));
    }

    [Fact]
    public async Task DifferentPort_Rejected()
    {
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://myreg.io:8443/v2/"),
            Realm("https://myreg.io:9443/token")));
    }

    [Fact]
    public async Task FtpScheme_Rejected()
    {
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://victim.io/v2/"),
            Realm("ftp://victim.io/token")));
    }

    #endregion

    #region Edge cases

    [Fact]
    public async Task TrustedHostCaseInsensitive_Allowed()
    {
        var validator = new DefaultRealmValidator
        {
            TrustedRealmHosts = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                "AUTH.EXAMPLE.COM"
            }
        };
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://registry.example.com/v2/"),
            Realm("https://auth.example.com/token")));
    }

    [Fact]
    public async Task DefaultPortExplicit443VsImplicit_Allowed()
    {
        var validator = new DefaultRealmValidator();
        // Explicit 443 on realm, implicit on registry
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://myreg.io/v2/"),
            Realm("https://myreg.io:443/token")));
    }

    [Fact]
    public async Task NonDefaultPort_MatchRequired()
    {
        var validator = new DefaultRealmValidator();
        // Registry on :5000, realm on default → reject
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://myreg.io:5000/v2/"),
            Realm("https://myreg.io/token")));
    }

    [Fact]
    public async Task HttpInsecure_SameHost_Allowed()
    {
        var validator = new DefaultRealmValidator
        {
            AllowInsecureHttp = true
        };
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("http://localhost:5000/v2/"),
            Realm("http://localhost:5000/token")));
    }

    [Fact]
    public async Task HttpInsecure_DifferentHost_Rejected()
    {
        var validator = new DefaultRealmValidator
        {
            AllowInsecureHttp = true
        };
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("http://localhost:5000/v2/"),
            Realm("http://evil.com/token")));
    }

    [Fact]
    public async Task HttpInsecure_SameHost_DifferentPort_Rejected()
    {
        var validator = new DefaultRealmValidator
        {
            AllowInsecureHttp = true
        };
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("http://localhost:5000/v2/"),
            Realm("http://localhost:6000/token")));
    }

    [Fact]
    public async Task UserinfoOnSameHost_Rejected()
    {
        // Userinfo must be rejected even if the host matches.
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://myreg.io/v2/"),
            Realm("https://user:pass@myreg.io/token")));
    }

    [Fact]
    public async Task HttpScheme_SameHost_RejectedByDefault()
    {
        // HTTP realm on same host is still rejected unless
        // AllowInsecureHttp is true.
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://myreg.io/v2/"),
            Realm("http://myreg.io/token")));
    }

    [Fact]
    public async Task TrustedHostNotAffectedBySchemeBlock()
    {
        // Docker Hub: auth.docker.io is in the default trusted
        // list — works without explicit TrustedRealmHosts.
        var validator = new DefaultRealmValidator();
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://registry-1.docker.io/v2/"),
            Realm("https://auth.docker.io/token")));
    }

    [Fact]
    public async Task GitLabRegistry_DefaultTrusted_Allowed()
    {
        // GitLab: gitlab.com is in the default trusted list.
        var validator = new DefaultRealmValidator();
        Assert.True(await validator.IsRealmAllowedAsync(
            Reg("https://registry.gitlab.com/v2/"),
            Realm("https://gitlab.com/jwt/auth")));
    }

    [Fact]
    public async Task DataScheme_Rejected()
    {
        var validator = new DefaultRealmValidator();
        Assert.False(await validator.IsRealmAllowedAsync(
            Reg("https://victim.io/v2/"),
            Realm("data:text/plain;base64,abc")));
    }

    #endregion
}
