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
using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using Moq;
using Moq.Protected;
using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras.Registry.Remote.Exceptions;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using Xunit;

namespace OrasProject.Oras.Tests.Registry.Remote.Auth;

public class ClientTest
{
    private const string _userAgent = "oras-dotnet";

    [Fact]
    public void IsCredentialEmpty_AllFieldsEmpty_ReturnsTrue()
    {
        // Arrange
        var credential = new Credential
        {
            Username = string.Empty,
            Password = string.Empty,
            RefreshToken = string.Empty,
            AccessToken = string.Empty
        };

        // Act
        var result = credential.IsEmpty();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCredentialEmpty_UsernameNotEmpty_ReturnsFalse()
    {
        // Arrange
        var credential = new Credential
        {
            Username = "user",
            Password = string.Empty,
            RefreshToken = string.Empty,
            AccessToken = string.Empty
        };

        // Act
        var result = credential.IsEmpty();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCredentialEmpty_PasswordNotEmpty_ReturnsFalse()
    {
        // Arrange
        var credential = new Credential
        {
            Username = string.Empty,
            Password = "password",
            RefreshToken = string.Empty,
            AccessToken = string.Empty
        };

        // Act
        var result = credential.IsEmpty();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCredentialEmpty_RefreshTokenNotEmpty_ReturnsFalse()
    {
        // Arrange
        var credential = new Credential
        {
            Username = string.Empty,
            Password = string.Empty,
            RefreshToken = "refreshToken",
            AccessToken = string.Empty
        };

        // Act
        var result = credential.IsEmpty();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCredentialEmpty_AccessTokenNotEmpty_ReturnsFalse()
    {
        // Arrange
        var credential = new Credential
        {
            Username = string.Empty,
            Password = string.Empty,
            RefreshToken = string.Empty,
            AccessToken = "accessToken"
        };

        // Act
        var result = credential.IsEmpty();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task FetchOauth2Token_WithRefreshToken_ReturnsAccessToken()
    {
        // Arrange
        var expectedToken = "test_access_token";
        var realm = "https://example.com";
        var service = "test_service";
        var refreshToken = "refresh_token";
        string[] scopes = ["repository:repo1:push", "repository:repo2:*"];
        var credential = new Credential
        {
            RefreshToken = refreshToken
        };

        async Task<HttpResponseMessage> MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            var response = new HttpResponseMessage();
            response.RequestMessage = req;
            if (req.Method == HttpMethod.Post && req.RequestUri?.AbsoluteUri.TrimEnd('/') == realm.TrimEnd('/'))
            {
                if (req.Content?.Headers.ContentType?.MediaType != "application/x-www-form-urlencoded")
                {
                    return new HttpResponseMessage(HttpStatusCode.UnsupportedMediaType);
                }

                var formData = await req.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrEmpty(formData))
                {
                    var formValues = System.Web.HttpUtility.ParseQueryString(formData);
                    if (formValues["grant_type"] == "refresh_token"
                        && formValues["refresh_token"] == refreshToken
                        && formValues["service"] == service
                        && formValues["client_id"] == _userAgent
                        && formValues["scope"] == string.Join(" ", scopes))
                    {
                        response.StatusCode = HttpStatusCode.OK;
                        response.Content = new StringContent($"{{\"access_token\": \"{expectedToken}\"}}");
                        return response;
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var client = new Client(new HttpClient(CustomHandler(MockHttpRequestHandler).Object));

        var cancellationToken = new CancellationToken();
        // Act
        var result = await client.FetchOauth2TokenAsync(
            realm,
            service,
            scopes,
            credential,
            cancellationToken);

        // Assert
        Assert.Equal(expectedToken, result);
    }

    [Fact]
    public async Task FetchOauth2Token_WithUsernameAndPassword_ReturnsAccessToken()
    {
        // Arrange
        var expectedToken = "test_access_token";
        var realm = "https://example.com";
        var service = "test_service";
        string[] scopes = ["repository:repo1:push", "repository:repo2:*"];
        var username = "test_user";
        var password = "password";
        var credential = new Credential
        {
            Username = username,
            Password = password
        };

        async Task<HttpResponseMessage> MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            var response = new HttpResponseMessage();
            response.RequestMessage = req;
            if (req.Method == HttpMethod.Post && req.RequestUri?.AbsoluteUri.TrimEnd('/') == realm.TrimEnd('/'))
            {
                if (req.Content?.Headers.ContentType?.MediaType != "application/x-www-form-urlencoded")
                {
                    return new HttpResponseMessage(HttpStatusCode.UnsupportedMediaType);
                }

                var formData = await req.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrEmpty(formData))
                {
                    var formValues = System.Web.HttpUtility.ParseQueryString(formData);
                    if (formValues["grant_type"] == "password"
                        && formValues["username"] == username
                        && formValues["password"] == password
                        && formValues["service"] == service
                        && formValues["client_id"] == _userAgent
                        && formValues["scope"] == string.Join(" ", scopes))
                    {
                        response.StatusCode = HttpStatusCode.OK;
                        response.Content = new StringContent($"{{\"access_token\": \"{expectedToken}\"}}");
                        return response;
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var client = new Client(new HttpClient(CustomHandler(MockHttpRequestHandler).Object));
        var cancellationToken = new CancellationToken();
        // Act
        var result = await client.FetchOauth2TokenAsync(
            realm,
            service,
            scopes,
            credential,
            cancellationToken);

        // Assert
        Assert.Equal("test_access_token", result);
    }

    [Fact]
    public async Task FetchOauth2Token_MissingCredentials_ThrowsAuthenticationException()
    {
        // Arrange
        var credential = new Credential();
        var realm = "https://example.com";
        var service = "test_service";
        string[] scopes = ["repository:repo1:push", "repository:repo2:*"];
        var client = new Client(new HttpClient());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthenticationException>(() =>
            client.FetchOauth2TokenAsync(
            realm,
            service,
            scopes,
            credential,
            CancellationToken.None));
        Assert.Equal("missing username or password for bearer auth", exception.Message);
    }

    [Fact]
    public async Task FetchOauth2Token_InvalidResponse_ThrowsAuthenticationException()
    {
        // Arrange
        var realm = "https://example.com";
        var service = "test_service";
        string[] scopes = ["repository:repo1:push", "repository:repo2:*"];

        var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"access_token\":\"\"}")
            });

        var credential = new Credential
        {
            RefreshToken = "test_refresh_token"
        };

        var client = new Client(new HttpClient(mockHandler.Object));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthenticationException>(() =>
            client.FetchOauth2TokenAsync(
                realm,
                service,
                scopes,
                credential,
                CancellationToken.None));
        Assert.Equal("AccessToken is empty or missing", exception.Message);
    }

    [Fact]
    public async Task FetchOauth2Token_NonOkResponse_ThrowsException()
    {
        // Arrange
        var realm = "https://example.com";
        var service = "test_service";
        string[] scopes = ["repository:repo1:push", "repository:repo2:*"];
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\": \"invalid_request\"}")
            });

        var credential = new Credential
        {
            RefreshToken = "test_refresh_token"
        };

        var client = new Client(new HttpClient(mockHandler.Object));

        // Act & Assert
        await Assert.ThrowsAsync<ResponseException>(() =>
            client.FetchOauth2TokenAsync(
                realm,
                service,
                scopes,
                credential,
                CancellationToken.None));
    }

    [Fact]
    public async Task FetchDistributionToken_WithUsernamePassword_ReturnsAccessToken()
    {
        // Arrange
        var expectedToken = "test_access_token";
        var realm = "https://example.com";
        var service = "test_service";
        string[] expectedScopes = ["repository:repo1:pull", "repository:repo2:push"];
        var username = "test_user";
        var password = "test_password";

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Get
                && req.RequestUri?.GetLeftPart(UriPartial.Path).TrimEnd('/') == realm.TrimEnd('/'))
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
                if (queryParams["service"] == service &&
                    queryParams["scope"]?.Split(",").SequenceEqual(expectedScopes) == true)
                {
                    if (req.Headers.Authorization != null &&
                        req.Headers.Authorization.Scheme == "Basic" &&
                        req.Headers.Authorization.Parameter ==
                        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")))
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent($"{{\"access_token\": \"{expectedToken}\"}}"),
                            RequestMessage = req
                        };
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = req };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }

        var client = new Client(new HttpClient(CustomHandler(MockHttpRequestHandler).Object));

        // Act
        var result = await client.FetchDistributionTokenAsync(
            realm,
            service,
            expectedScopes,
            username,
            password,
            CancellationToken.None);

        // Assert
        Assert.Equal(expectedToken, result);
    }

    [Fact]
    public async Task FetchDistributionToken_WithUsernamePassword_ValidResponse_ReturnsToken()
    {
        // Arrange
        var expectedToken = "test_token";
        var realm = "https://example.com";
        var service = "test_service";
        string[] expectedScopes = ["repository:repo1:pull", "repository:repo2:push"];
        var username = "test_user";
        var password = "test_password";

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Get
                && req.RequestUri?.GetLeftPart(UriPartial.Path).TrimEnd('/') == realm.TrimEnd('/'))
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
                if (queryParams["service"] == service &&
                    queryParams["scope"]?.Split(",").SequenceEqual(expectedScopes) == true)
                {

                    if (req.Headers.Authorization != null &&
                        req.Headers.Authorization.Scheme == "Basic" &&
                        req.Headers.Authorization.Parameter ==
                        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}")))
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent($"{{\"access_token\": \"{expectedToken}\"}}"),
                            RequestMessage = req
                        };
                    }
                }
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = req };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }

        var client = new Client(new HttpClient(CustomHandler(MockHttpRequestHandler).Object));

        // Act
        var result = await client.FetchDistributionTokenAsync(
            realm,
            service,
            expectedScopes,
            username,
            password,
            CancellationToken.None);

        // Assert
        Assert.Equal(expectedToken, result);
    }

    [Fact]
    public async Task FetchDistributionToken_MissingToken_ThrowsAuthenticationException()
    {
        // Arrange
        var realm = "https://example.com";
        var service = "test_service";
        string[] scopes = ["repository:repo1:pull", "repository:repo2:push"];
        var username = "test_user";
        var password = "test_password";

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var client = new Client(new HttpClient(mockHandler.Object));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthenticationException>(() =>
            client.FetchDistributionTokenAsync(
                realm,
                service,
                scopes,
                username,
                password,
                CancellationToken.None));
        Assert.Equal("Both AccessToken and Token are empty or missing", exception.Message);
    }

    [Fact]
    public async Task FetchDistributionToken_NonOkResponse_ThrowsResponseException()
    {
        // Arrange
        var realm = "https://example.com";
        var service = "test_service";
        string[] scopes = ["repository:repo1:pull", "repository:repo2:push"];
        var username = "test_user";
        var password = "test_password";

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\": \"invalid_request\"}")
            });

        var client = new Client(new HttpClient(mockHandler.Object));

        // Act & Assert
        await Assert.ThrowsAsync<ResponseException>(() =>
            client.FetchDistributionTokenAsync(
                realm,
                service,
                scopes,
                username,
                password,
                CancellationToken.None));
    }

    [Fact]
    public async Task FetchDistributionToken_EmptyUsernameAndPassword_NoAuthorizationHeader()
    {
        // Arrange
        var realm = "https://example.com";
        var service = "test_service";
        string[] expectedScopes = ["repository:repo1:pull", "repository:repo2:push"];
        var expectedToken = "test_access_token";

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Get
                && req.RequestUri?.GetLeftPart(UriPartial.Path).TrimEnd('/') == realm.TrimEnd('/'))
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
                if (queryParams["service"] == service &&
                    queryParams["scope"]?.Split(",").SequenceEqual(expectedScopes) == true)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent($"{{\"access_token\": \"{expectedToken}\"}}"),
                        RequestMessage = req
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = req };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }
        var client = new Client(new HttpClient(CustomHandler(MockHttpRequestHandler).Object));

        // Act
        var result = await client.FetchDistributionTokenAsync(
            realm,
            service,
            expectedScopes,
            null,
            null,
            CancellationToken.None);

        // Assert
        Assert.Equal("test_access_token", result);

        // with only username
        result = await client.FetchDistributionTokenAsync(
            realm,
            service,
            expectedScopes,
            "username",
            null,
            CancellationToken.None);

        // Assert
        Assert.Equal("test_access_token", result);

        // with only password
        result = await client.FetchDistributionTokenAsync(
            realm,
            service,
            expectedScopes,
            null,
            "password",
            CancellationToken.None);

        // Assert
        Assert.Equal("test_access_token", result);
    }

    [Fact]
    public async Task FetchBasicAuth_ValidCredentials_ReturnsBase64EncodedToken()
    {
        // Arrange
        var registry = "https://example.com";
        var username = "test_user";
        var password = "test_password";
        var expectedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        var mockCredentialProvider = new Mock<ICredentialProvider>();
        mockCredentialProvider
            .Setup(provider => provider.ResolveCredentialAsync(registry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential
            {
                Username = username,
                Password = password
            });

        var client = new Client(new HttpClient(), mockCredentialProvider.Object);

        // Act
        var result = await client.FetchBasicAuthAsync(registry, CancellationToken.None);

        // Assert
        Assert.Equal(expectedToken, result);
    }

    [Fact]
    public async Task FetchBasicAuth_EmptyUsername_ThrowsAuthenticationException()
    {
        // Arrange
        var registry = "https://example.com";
        var password = "test_password";

        var mockCredentialProvider = new Mock<ICredentialProvider>();
        mockCredentialProvider
            .Setup(provider => provider.ResolveCredentialAsync(registry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential
            {
                Username = string.Empty,
                Password = password
            });

        var client = new Client(new HttpClient(), mockCredentialProvider.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthenticationException>(() =>
            client.FetchBasicAuthAsync(registry, CancellationToken.None));
        Assert.Equal("Missing username or password for basic authentication.", exception.Message);
    }

    [Fact]
    public async Task FetchBasicAuth_EmptyPassword_ThrowsAuthenticationException()
    {
        // Arrange
        var registry = "https://example.com";
        var username = "test_user";

        var mockCredentialProvider = new Mock<ICredentialProvider>();
        mockCredentialProvider
            .Setup(provider => provider.ResolveCredentialAsync(registry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential
            {
                Username = username,
                Password = string.Empty
            });

        var client = new Client(new HttpClient(), mockCredentialProvider.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthenticationException>(() =>
            client.FetchBasicAuthAsync(registry, CancellationToken.None));
        Assert.Equal("Missing username or password for basic authentication.", exception.Message);
    }

    [Fact]
    public async Task SendAsync_WithoutCredential_FetchDistributionToken()
    {
        // Arrange
        var host = "example.com";
        var expectedToken = "test_access_token";
        var realm = "https://auth.example.com";
        var service = "test_service";
        string[] expectedScopes = ["repository:repo1:pull", "repository:repo2:push"];

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Get
                && req.RequestUri?.GetLeftPart(UriPartial.Path).TrimEnd('/') == realm.TrimEnd('/'))
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
                if (queryParams["service"] == service &&
                    queryParams["scope"]?.Split(",").SequenceEqual(expectedScopes) == true)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent($"{{\"access_token\": \"{expectedToken}\"}}"),
                        RequestMessage = req
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = req };
            }

            if (req.Method == HttpMethod.Get && req.RequestUri?.Host.TrimEnd('/') == host.TrimEnd('/'))
            {
                if (req.Headers.Authorization == null
                    || req.Headers.Authorization.Parameter != expectedToken
                    || req.Headers.Authorization.Scheme != "Bearer")
                {
                    return new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.Unauthorized,
                        Headers =
                        {
                            WwwAuthenticate =
                            {
                                new AuthenticationHeaderValue(
                                    "Bearer",
                                    $"realm=\"{realm}\",service=\"{service}\",scope=\"{string.Join(" ", expectedScopes)}\"")
                            }
                        },
                        RequestMessage = req
                    };
                }
                return new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    RequestMessage = req
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }

        var mockHandler = CustomHandler(MockHttpRequestHandler);
        var client = new Client(new HttpClient(mockHandler.Object));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");
        var cancellationToken = new CancellationToken();

        // Act
        var result = await client.SendAsync(request, cancellationToken: cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization != null
                                                && req.Headers.Authorization.Scheme == "Bearer"
                                                && req.Headers.Authorization.Parameter == expectedToken),
            ItExpr.IsAny<CancellationToken>());

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization == null
                                                && req.RequestUri != null
                                                && req.RequestUri.Host.TrimEnd('/') == host.TrimEnd('/')),
            ItExpr.IsAny<CancellationToken>());

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization == null
                                                && req.RequestUri != null
                                                && req.RequestUri.GetLeftPart(UriPartial.Path).TrimEnd('/') == realm.TrimEnd('/')),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithAuthorizationHeader_ReturnsResponse()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var client = new Client(new HttpClient(mockHandler.Object));
        client.BaseClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test_token");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization != null),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithCustomHeader_ReturnsResponse()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var expectedToken = "test_token";
        var client = new Client(new HttpClient(mockHandler.Object));
        client.CustomHeaders["Authorization"] = [$"Bearer {expectedToken}"];
        client.CustomHeaders["foo"] = ["bar", "bar1"];

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization != null &&
                                                req.Headers.Authorization.Scheme == "Bearer" &&
                                                req.Headers.Authorization.Parameter == expectedToken &&
                                                string.Join(" ", req.Headers.GetValues("foo")).Equals("bar bar1")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithoutAuthorizationHeader_UsesCachedBasicAuth()
    {
        // Arrange
        var host = "example.com";
        var token = "test_basic_token";

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var client = new Client(new HttpClient(mockHandler.Object));
        client.Cache.SetCache(host, Challenge.Scheme.Basic, "", token);
        client.CustomHeaders["foo"] = ["bar"];
        client.CustomHeaders["foo"] = ["newBar"];

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization != null
                                                && req.Headers.Authorization.Scheme == "Basic"
                                                && req.Headers.Authorization.Parameter == token
                                                && "newBar".Equals(req.Headers.GetValues("foo").FirstOrDefault())),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithoutAuthorizationHeader_UsesCachedBearerAuth()
    {
        // Arrange
        var host = "example.com";
        var token = "test_bearer_token";
        string[] scopes = ["repository:repo1:pull,push", "repository:repo2:pull"];

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var client = new Client(new HttpClient(mockHandler.Object));
        client.Cache.SetCache(host, Challenge.Scheme.Bearer, string.Join(" ", scopes), token);
        Assert.True(Scope.TryParse(scopes[0], out var scope1));
        client.ScopeManager.SetScopeForRegistry(host, scope1);
        Assert.True(Scope.TryParse(scopes[1], out var scope2));
        client.ScopeManager.SetScopeForRegistry(host, scope2);
        client.CustomHeaders["foo"] = ["bar"];
        client.CustomHeaders["foo"] = ["newBar"];
        client.CustomHeaders["key1"] = ["value1"];

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization != null &&
                                                req.Headers.Authorization.Scheme == "Bearer"
                                                && req.Headers.Authorization.Parameter == token
                                                && "newBar".Equals(req.Headers.GetValues("foo").FirstOrDefault())
                                                && "value1".Equals(req.Headers.GetValues("key1").FirstOrDefault())),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UnauthorizedResponse_FetchesNewBearerTokenAndRetries()
    {
        // Arrange
        var host = "example.com";
        var realm = "https://auth.example.com";
        var service = "test_service";
        var refreshToken = "refresh_token";
        var expectedToken = "access_token";

        string[] scopes = ["repository:repo1:pull,*,delete", "repository:repo2:delete"];
        string[] expectedScopes = ["repository:repo1:*", "repository:repo2:delete"];
        async Task<HttpResponseMessage> MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Post && req.RequestUri?.AbsoluteUri.TrimEnd('/') == realm.TrimEnd('/'))
            {
                if (req.Content?.Headers.ContentType?.MediaType != "application/x-www-form-urlencoded")
                {
                    return new HttpResponseMessage(HttpStatusCode.UnsupportedMediaType);
                }

                var formData = await req.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrEmpty(formData))
                {
                    var formValues = System.Web.HttpUtility.ParseQueryString(formData);
                    if (formValues["grant_type"] == "refresh_token"
                        && formValues["refresh_token"] == refreshToken
                        && formValues["service"] == service
                        && formValues["client_id"] == _userAgent
                        && formValues["scope"] == string.Join(" ", expectedScopes))
                    {
                        return new HttpResponseMessage()
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent($"{{\"access_token\": \"{expectedToken}\"}}"),
                            RequestMessage = req
                        };
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (req.Method == HttpMethod.Get && req.RequestUri?.Host.TrimEnd('/') == host.TrimEnd('/'))
            {
                if (req.Headers.Authorization == null
                    || req.Headers.Authorization.Parameter != expectedToken
                    || req.Headers.Authorization.Scheme != "Bearer")
                {
                    return new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.Unauthorized,
                        Headers =
                        {
                            WwwAuthenticate =
                            {
                                new AuthenticationHeaderValue(
                                    "Bearer",
                                    $"realm=\"{realm}\",service=\"{service}\",scope=\"{string.Join(" ", scopes)}\"")
                            }
                        },
                        RequestMessage = req
                    };
                }

                if (string.Join(" ", req.Headers.GetValues("foo")).Equals("bar abc") &&
                    "value1".Equals(req.Headers.GetValues("key1").FirstOrDefault()))
                {
                    return new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.OK,
                        RequestMessage = req
                    };
                }
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var mockCredentialProvider = new Mock<ICredentialProvider>();
        mockCredentialProvider
            .Setup(provider => provider.ResolveCredentialAsync(host, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential
            {
                RefreshToken = refreshToken
            });
        var mockHandler = CustomHandler(MockHttpRequestHandler);

        var client = new Client(new HttpClient(mockHandler.Object), mockCredentialProvider.Object);
        client.CustomHeaders["foo"] = ["bar"];
        client.CustomHeaders["foo"].Add("abc");
        client.CustomHeaders["key1"] = ["value1"];
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization != null &&
                                                req.Headers.Authorization.Scheme == "Bearer"
                                                && req.Headers.Authorization.Parameter == expectedToken),
            ItExpr.IsAny<CancellationToken>());
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization == null),
            ItExpr.IsAny<CancellationToken>());

        mockHandler.Invocations.Clear(); // Clear invocations to ensure no residual state between tests

        using var request2 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        response = await client.SendAsync(request2, cancellationToken: CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization == null),
            ItExpr.IsAny<CancellationToken>());

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization != null &&
                                                req.Headers.Authorization.Scheme == "Bearer"
                                                && req.Headers.Authorization.Parameter == expectedToken),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UnauthorizedResponse_BearerChallengeWithoutService_OmitsServiceInTokenRequest()
    {
        // Arrange
        var host = "noservice.example.com";
        var realm = "https://auth.noservice.example.com";
        var refreshToken = "refresh_token";
        var expectedToken = "access_token";
        string[] scopes = ["repository:repo1:pull"];

        async Task<HttpResponseMessage> MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Post && req.RequestUri?.AbsoluteUri.TrimEnd('/') == realm.TrimEnd('/'))
            {
                if (req.Content?.Headers.ContentType?.MediaType != "application/x-www-form-urlencoded")
                {
                    return new HttpResponseMessage(HttpStatusCode.UnsupportedMediaType);
                }

                var formData = await req.Content.ReadAsStringAsync(cancellationToken);
                var formValues = System.Web.HttpUtility.ParseQueryString(formData);

                // service MUST be omitted (i.e., null) when absent in challenge
                if (formValues["grant_type"] == "refresh_token"
                    && formValues["refresh_token"] == refreshToken
                    && formValues["client_id"] == _userAgent
                    && formValues["scope"] == string.Join(" ", scopes)
                    && formValues["service"] == null)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent($"{{\"access_token\": \"{expectedToken}\"}}"),
                        RequestMessage = req
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (req.Method == HttpMethod.Get && req.RequestUri?.Host.TrimEnd('/') == host.TrimEnd('/'))
            {
                // First attempt will be unauthorized (no token yet)
                if (req.Headers.Authorization == null || req.Headers.Authorization.Parameter != expectedToken)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.Unauthorized,
                        Headers =
                        {
                            WwwAuthenticate =
                            {
                                // Challenge deliberately omits service parameter
                                new AuthenticationHeaderValue(
                                    "Bearer",
                                    $"realm=\"{realm}\",scope=\"{string.Join(" ", scopes)}\"")
                            }
                        },
                        RequestMessage = req
                    };
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    RequestMessage = req
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var mockCredentialProvider = new Mock<ICredentialProvider>();
        mockCredentialProvider
            .Setup(p => p.ResolveCredentialAsync(host, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential { RefreshToken = refreshToken });

        var client = new Client(new HttpClient(CustomHandler(MockHttpRequestHandler).Object), mockCredentialProvider.Object);
        // Populate scope manager to ensure scopes passed into token request
        Assert.True(Scope.TryParse(scopes[0], out var scope));
        client.ScopeManager.SetScopeForRegistry(host, scope);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Ensure subsequent cached call works and still no service param used
        using var request2 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");
        var second = await client.SendAsync(request2, cancellationToken: CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task SendAsync_BearerChallengeMissingRealm_ThrowsKeyNotFoundException()
    {
        // Arrange
        var host = "missingrealm.example.com";
        var service = "svc"; // Present but realm intentionally missing
        string[] scopes = ["repository:repo1:pull"];

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Get && req.RequestUri?.Host == host)
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Unauthorized,
                    Headers =
                    {
                        WwwAuthenticate =
                        {
                            // Missing realm parameter (should trigger KeyNotFoundException in client)
                            new AuthenticationHeaderValue(
                                "Bearer",
                                $"service=\"{service}\",scope=\"{string.Join(" ", scopes)}\"")
                        }
                    },
                    RequestMessage = req
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }

        var client = new Client(new HttpClient(CustomHandler(MockHttpRequestHandler).Object));
        Assert.True(Scope.TryParse(scopes[0], out var scope));
        client.ScopeManager.SetScopeForRegistry(host, scope);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act + Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(async () => await client.SendAsync(request, cancellationToken: CancellationToken.None));
        Assert.Equal("Missing 'realm' parameter in WWW-Authenticate Bearer challenge.", ex.Message);
    }

    [Fact]
    public async Task SendAsync_UnauthorizedResponse_FetchesNewBasicTokenAndRetries()
    {
        // Arrange
        var host = "example.com";
        var realm = "https://auth.example.com";
        var service = "test_service";
        var username = "username";
        var password = "password";
        var basicToken =
            Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Get
                && req.RequestUri?.Host.TrimEnd('/') == host.TrimEnd('/'))
            {
                if (req.Headers.Authorization == null
                    || req.Headers.Authorization.Parameter != basicToken
                    || req.Headers.Authorization.Scheme != "Basic")
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        RequestMessage = req,
                        StatusCode = HttpStatusCode.Unauthorized,
                        Headers =
                        {
                            WwwAuthenticate = { new AuthenticationHeaderValue(
                                "Basic",
                                $"realm=\"{realm}\",service=\"{service}\"") }
                        }
                    };
                }

                if (string.Join(" ", req.Headers.GetValues("foo")).Equals("bar abc") &&
                    "value1".Equals(req.Headers.GetValues("key1").FirstOrDefault())
                    && req.Headers.UserAgent.FirstOrDefault() != null
                    && req.Headers.UserAgent.FirstOrDefault()!.ToString().Equals("oras-dotnet"))
                {
                    return new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.OK,
                        RequestMessage = req
                    };
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }

        var mockCredentialProvider = new Mock<ICredentialProvider>();
        mockCredentialProvider
            .Setup(provider => provider.ResolveCredentialAsync(host, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential
            {
                Username = username,
                Password = password
            });
        var mockHandler = CustomHandler(MockHttpRequestHandler);
        var client = new Client(new HttpClient(mockHandler.Object), mockCredentialProvider.Object);
        client.CustomHeaders["foo"] = ["bar"];
        client.CustomHeaders["foo"].Add("abc");
        client.CustomHeaders["key1"] = ["value1"];
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var response = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization == null),
            ItExpr.IsAny<CancellationToken>());

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization != null &&
                                                req.Headers.Authorization.Scheme == "Basic"
                                                && req.Headers.Authorization.Parameter == basicToken),
            ItExpr.IsAny<CancellationToken>());

        mockHandler.Invocations.Clear(); // Clear invocations to ensure no residual state between tests

        using var request2 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");
        Assert.True(client.Cache.TryGetToken(host, Challenge.Scheme.Basic, "", out var token));
        Assert.Equal(basicToken, token);
        // Act
        response = await client.SendAsync(request2, cancellationToken: CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization != null &&
                                                req.Headers.Authorization.Scheme == "Basic"
                                                && req.Headers.Authorization.Parameter == basicToken),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithoutCredential_FetchBasicAuthThrowsAuthenticationException()
    {
        // Arrange
        var host = "example.com";
        var realm = "https://auth.example.com";
        var service = "test_service";

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Get
                && req.RequestUri?.Host.TrimEnd('/') == host.TrimEnd('/'))
            {
                if (req.Headers.Authorization == null)
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        RequestMessage = req,
                        StatusCode = HttpStatusCode.Unauthorized,
                        Headers =
                        {
                            WwwAuthenticate = { new AuthenticationHeaderValue(
                                "Basic",
                                $"realm=\"{realm}\",service=\"{service}\"") }
                        }
                    };
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }

        var mockHandler = CustomHandler(MockHttpRequestHandler);
        var client = new Client(new HttpClient(mockHandler.Object));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var exception = await Assert.ThrowsAsync<AuthenticationException>(async () => await client.SendAsync(request, cancellationToken: CancellationToken.None));

        // Assert
        Assert.Equal("Missing username or password for basic authentication.", exception.Message);
    }

    [Fact]
    public async Task ResolveCredentialAsync_NullCredentialProvider_ReturnsEmptyCredential()
    {
        // Arrange
        var client = new Client();

        // Act
        var result = await client.ResolveCredentialAsync("anyregistry", CancellationToken.None);

        // Assert
        Assert.True(result.IsEmpty());
    }

    [Fact]
    public async Task ResolveCredentialAsync_CredentialProviderConfigured_ReturnsExpectedCredential()
    {
        // Arrange
        var expected = new Credential
        {
            Username = "user",
            Password = "pass",
            RefreshToken = "refresh",
            AccessToken = "access"
        };
        var mockCredentialProvider = new Mock<ICredentialProvider>();
        mockCredentialProvider
            .Setup(provider => provider.ResolveCredentialAsync("myregistry", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var client = new Client(new HttpClient(), mockCredentialProvider.Object);

        // Act
        var result = await client.ResolveCredentialAsync("myregistry", CancellationToken.None);

        // Assert
        Assert.Equal(expected.Username, result.Username);
        Assert.Equal(expected.Password, result.Password);
        Assert.Equal(expected.RefreshToken, result.RefreshToken);
        Assert.Equal(expected.AccessToken, result.AccessToken);
    }

    [Fact]
    public async Task SendAsync_UsesAuthorityForCacheKey_Bearer()
    {
        // Arrange
        var host = "example.com";
        var realm = "https://auth.example.com";
        var service = "test_service";
        string[] scopes5000 = ["repository:repo1:pull5000"]; // distinct per port
        string[] scopes443 = ["repository:repo1:pull443"];  // distinct per port
        var token5000 = "token5000";
        var token443 = "token443";

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Get && req.RequestUri?.GetLeftPart(UriPartial.Path).TrimEnd('/') == realm.TrimEnd('/'))
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
                var scope = query["scope"] ?? string.Empty;
                var svc = query["service"] ?? string.Empty;
                if (svc == service && (scope == string.Join(" ", scopes5000) || scope == string.Join(" ", scopes443)))
                {
                    var tok = scope == string.Join(" ", scopes5000) ? token5000 : token443;
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent($"{{\"access_token\": \"{tok}\"}}"),
                        RequestMessage = req
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = req };
            }

            if (req.Method == HttpMethod.Get && req.RequestUri?.Host == host)
            {
                var expectedTok = req.RequestUri!.Port == 5000 ? token5000 : token443;
                if (req.Headers.Authorization == null || req.Headers.Authorization.Scheme != "Bearer" || req.Headers.Authorization.Parameter != expectedTok)
                {
                    var scopes = req.RequestUri.Port == 5000 ? scopes5000 : scopes443;
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.Unauthorized,
                        Headers =
                        {
                            WwwAuthenticate =
                            {
                                new AuthenticationHeaderValue(
                                    "Bearer",
                                    $"realm=\"{realm}\",service=\"{service}\",scope=\"{string.Join(" ", scopes)}\"")
                            }
                        },
                        RequestMessage = req
                    };
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    RequestMessage = req
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }

        var mockHandler = CustomHandler(MockHttpRequestHandler);
        var client = new Client(new HttpClient(mockHandler.Object));

        // Act: call port 5000
        using var request1 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}:5000");
        var r1 = await client.SendAsync(request1, cancellationToken: CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // Act: call port 443 (explicit)
        using var request2 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}:443");
        var r2 = await client.SendAsync(request2, cancellationToken: CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        // Assert: ensure an authorized call happened with token per port
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.Host == host && req.RequestUri.Port == 5000 &&
                                            req.Headers.Authorization != null && req.Headers.Authorization.Scheme == "Bearer" && req.Headers.Authorization.Parameter == token5000),
            ItExpr.IsAny<CancellationToken>());

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.Host == host && req.RequestUri.Port == 443 &&
                                            req.Headers.Authorization != null && req.Headers.Authorization.Scheme == "Bearer" && req.Headers.Authorization.Parameter == token443),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UsesAuthorityForCacheKey_Basic()
    {
        // Arrange
        var host = "example.com";
        var realm = "https://auth.example.com";
        var service = "test_service";
        var up5000 = (User: "u5000", Pass: "p5000");
        var up443 = (User: "u443", Pass: "p443");
        var tok5000 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{up5000.User}:{up5000.Pass}"));
        var tok443 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{up443.User}:{up443.Pass}"));

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken = default)
        {
            if (req.Method == HttpMethod.Get && req.RequestUri?.Host == host)
            {
                var expectedTok = req.RequestUri!.Port == 5000 ? tok5000 : tok443;
                if (req.Headers.Authorization == null || req.Headers.Authorization.Scheme != "Basic" || req.Headers.Authorization.Parameter != expectedTok)
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        RequestMessage = req,
                        Headers = { WwwAuthenticate = { new AuthenticationHeaderValue("Basic", $"realm=\"{realm}\",service=\"{service}\"") } }
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = req };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }

        var mockCredentialProvider = new Mock<ICredentialProvider>();
        mockCredentialProvider
            .Setup(p => p.ResolveCredentialAsync(It.Is<string>(s => s.Contains(":5000")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential { Username = up5000.User, Password = up5000.Pass });
        mockCredentialProvider
            .Setup(p => p.ResolveCredentialAsync(It.Is<string>(s => s.Contains(":443") || s == host), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential { Username = up443.User, Password = up443.Pass });

        var mockHandler = CustomHandler(MockHttpRequestHandler);
        var client = new Client(new HttpClient(mockHandler.Object), mockCredentialProvider.Object);

        // Act: call port 5000
        using var request1 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}:5000");
        var r1 = await client.SendAsync(request1, cancellationToken: CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // Act: call port 443 (explicit)
        using var request2 = new HttpRequestMessage(HttpMethod.Get, $"https://{host}:443");
        var r2 = await client.SendAsync(request2, cancellationToken: CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        // Assert: ensure proper basic auth token per port was used
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.Host == host && req.RequestUri.Port == 5000 &&
                                            req.Headers.Authorization != null && req.Headers.Authorization.Scheme == "Basic" && req.Headers.Authorization.Parameter == tok5000),
            ItExpr.IsAny<CancellationToken>());

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.Host == host && (req.RequestUri.Port == 443 || req.RequestUri.Port == -1) &&
                                            req.Headers.Authorization != null && req.Headers.Authorization.Scheme == "Basic" && req.Headers.Authorization.Parameter == tok443),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Cache_Setter_UsesInjectedCache_ForBearer()
    {
        // Arrange
        var host = "example.com";
        var authority = $"{host}:5000";
        string[] scopes = ["repository:repo1:pull", "repository:repo2:push"]; // any values
        var expectedKey = string.Join(" ", scopes);
        var expectedToken = "cached_bearer";

        HttpResponseMessage Mock(HttpRequestMessage req, CancellationToken ct)
        {
            if (req.Method == HttpMethod.Get && req.RequestUri?.Host == host && req.RequestUri.Port == 5000)
            {
                if (req.Headers.Authorization != null && req.Headers.Authorization.Scheme == "Bearer" && req.Headers.Authorization.Parameter == expectedToken)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = req };
                }
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = req };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }

        var handler = CustomHandler(Mock);
        var client = new Client(new HttpClient(handler.Object));
        // Inject scopes for the authority so the client builds the same key
        Assert.True(Scope.TryParse(scopes[0], out var s1));
        client.ScopeManager.SetScopeForRegistry(authority, s1);
        Assert.True(Scope.TryParse(scopes[1], out var s2));
        client.ScopeManager.SetScopeForRegistry(authority, s2);

        // Inject mocked ICache to supply pre-cached token
        var cacheMock = new Mock<ICache>(MockBehavior.Strict);
        var schemeOut = Challenge.Scheme.Bearer;
        cacheMock.Setup(m => m.TryGetScheme(authority, out schemeOut, null)).Returns(true);
        var tokenOut = expectedToken;
        cacheMock.Setup(m => m.TryGetToken(authority, Challenge.Scheme.Bearer, expectedKey, out tokenOut, null)).Returns(true);
        client.Cache = cacheMock.Object;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{authority}");

        // Act
        var resp = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        cacheMock.Verify(m => m.TryGetScheme(authority, out schemeOut, null), Times.AtLeastOnce());
        cacheMock.Verify(m => m.TryGetToken(authority, Challenge.Scheme.Bearer, expectedKey, out tokenOut, null), Times.AtLeastOnce());
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.Host == host && r.RequestUri.Port == 5000 && r.Headers.Authorization != null && r.Headers.Authorization.Scheme == "Bearer" && r.Headers.Authorization.Parameter == expectedToken),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Cache_Setter_UsesInjectedCache_ForBasic()
    {
        // Arrange
        var host = "example.com";
        var authority = host; // Uri.Authority omits default :443 for https
        var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("u:p"));

        HttpResponseMessage Mock(HttpRequestMessage req, CancellationToken ct)
        {
            if (req.Method == HttpMethod.Get && req.RequestUri?.Host == host)
            {
                if (req.Headers.Authorization != null && req.Headers.Authorization.Scheme == "Basic" && req.Headers.Authorization.Parameter == basicToken)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = req };
                }
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = req };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req };
        }

        var handler = CustomHandler(Mock);
        var client = new Client(new HttpClient(handler.Object));
        var cacheMock = new Mock<ICache>(MockBehavior.Strict);
        var schemeOut = Challenge.Scheme.Basic;
        cacheMock.Setup(m => m.TryGetScheme(authority, out schemeOut, null)).Returns(true);
        var tokenOut = basicToken;
        cacheMock.Setup(m => m.TryGetToken(authority, Challenge.Scheme.Basic, string.Empty, out tokenOut, null)).Returns(true);
        client.Cache = cacheMock.Object;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var resp = await client.SendAsync(request, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        cacheMock.Verify(m => m.TryGetScheme(authority, out schemeOut, null), Times.AtLeastOnce());
        cacheMock.Verify(m => m.TryGetToken(authority, Challenge.Scheme.Basic, string.Empty, out tokenOut, null), Times.AtLeastOnce());
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.Host == host && r.Headers.Authorization != null && r.Headers.Authorization.Scheme == "Basic" && r.Headers.Authorization.Parameter == basicToken),
            ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Tests Client with two HttpClient parameters for consistent configuration across redirect modes.
    /// </summary>
    [Fact]
    public async Task Client_WithTwoHttpClients_AppliesConfigurationToBoth()
    {
        var baseUrl = "https://example.com";
        var expectedRedirectLocation = new Uri("https://storage.example.com/blob");

        // Create mock handlers
        var mockHandlerWithRedirect = new Mock<HttpMessageHandler>();
        var mockHandlerNoRedirect = new Mock<HttpMessageHandler>();

        mockHandlerWithRedirect.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = req });
            });

        mockHandlerNoRedirect.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                if (req.RequestUri?.AbsolutePath == "/redirect")
                {
                    var response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
                    {
                        RequestMessage = req
                    };
                    response.Headers.Location = expectedRedirectLocation;
                    return Task.FromResult(response);
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = req });
            });

        // Create HttpClients with custom timeout configuration
        var httpClientWithRedirect = new HttpClient(mockHandlerWithRedirect.Object)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var httpClientNoRedirect = new HttpClient(mockHandlerNoRedirect.Object)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Create Client with both HttpClients
        var client = new Client(
            httpClient: httpClientWithRedirect,
            noRedirectHttpClient: httpClientNoRedirect,
            credentialProvider: null,
            cache: null);

        // Test standard request (follows redirects)
        using var standardRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/test");
        using (var standardResponse = await client.SendAsync(
            standardRequest,
            cancellationToken: CancellationToken.None))
        {
            Assert.Equal(HttpStatusCode.OK, standardResponse.StatusCode);
        }

        // Test no-redirect request (captures redirect location)
        using var noRedirectRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/redirect");
        using (var noRedirectResponse = await client.SendAsync(
            noRedirectRequest,
            allowAutoRedirect: false,
            cancellationToken: CancellationToken.None))
        {
            Assert.Equal(HttpStatusCode.TemporaryRedirect, noRedirectResponse.StatusCode);
            Assert.Equal(expectedRedirectLocation, noRedirectResponse.Headers.Location);
        }

        // Verify both clients were used with the custom timeout configuration
        Assert.Equal(TimeSpan.FromSeconds(30), client.BaseClient.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(30), client.NoRedirectClient.Timeout);
    }
}
