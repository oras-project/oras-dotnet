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
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using Xunit;
using Xunit.Abstractions;

namespace OrasProject.Oras.Tests.Remote.Auth;

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

        async Task<HttpResponseMessage> MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken)
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
        var result = await client.FetchOauth2Token(
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

        async Task<HttpResponseMessage> MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken)
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
        var result = await client.FetchOauth2Token(
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
            client.FetchOauth2Token(
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
            client.FetchOauth2Token(
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
            client.FetchOauth2Token(
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

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken)
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
        var result = await client.FetchDistributionToken(
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

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken)
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
        var result = await client.FetchDistributionToken(
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
            client.FetchDistributionToken(
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
            client.FetchDistributionToken(
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

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken)
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
        var result = await client.FetchDistributionToken(
            realm,
            service,
            expectedScopes,
            null,
            null,
            CancellationToken.None);

        // Assert
        Assert.Equal("test_access_token", result);

        // with only username
        result = await client.FetchDistributionToken(
            realm,
            service,
            expectedScopes,
            "username",
            null,
            CancellationToken.None);

        // Assert
        Assert.Equal("test_access_token", result);

        // with only password
        result = await client.FetchDistributionToken(
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
            .Setup(helper => helper.ResolveCredentialAsync(registry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential
            {
                Username = username,
                Password = password
            });

        var client = new Client(new HttpClient(), mockCredentialProvider.Object);

        // Act
        var result = await client.FetchBasicAuth(registry, CancellationToken.None);

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
            .Setup(helper => helper.ResolveCredentialAsync(registry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential
            {
                Username = string.Empty,
                Password = password
            });

        var client = new Client(new HttpClient(), mockCredentialProvider.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthenticationException>(() =>
            client.FetchBasicAuth(registry, CancellationToken.None));
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
            .Setup(helper => helper.ResolveCredentialAsync(registry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential
            {
                Username = username,
                Password = string.Empty
            });

        var client = new Client(new HttpClient(), mockCredentialProvider.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthenticationException>(() =>
            client.FetchBasicAuth(registry, CancellationToken.None));
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

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken)
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
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");
        var cancellationToken = new CancellationToken();

        // Act
        var result = await client.SendAsync(request, cancellationToken);

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
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        var response = await client.SendAsync(request, CancellationToken.None);

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

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        var response = await client.SendAsync(request, CancellationToken.None);

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

        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var response = await client.SendAsync(request, CancellationToken.None);

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


        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var response = await client.SendAsync(request, CancellationToken.None);

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
        async Task<HttpResponseMessage> MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken)
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
            .Setup(helper => helper.ResolveCredentialAsync(host, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Credential
            {
                RefreshToken = refreshToken
            });
        var mockHandler = CustomHandler(MockHttpRequestHandler);

        var client = new Client(new HttpClient(mockHandler.Object), mockCredentialProvider.Object);
        client.CustomHeaders["foo"] = ["bar"];
        client.CustomHeaders["foo"].Add("abc");
        client.CustomHeaders["key1"] = ["value1"];
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var response = await client.SendAsync(request, CancellationToken.None);

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

        request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        response = await client.SendAsync(request, CancellationToken.None);
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

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken)
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
            .Setup(helper => helper.ResolveCredentialAsync(host, It.IsAny<CancellationToken>()))
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
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var response = await client.SendAsync(request, CancellationToken.None);

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

        request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");
        Assert.True(client.Cache.TryGetToken(host, Challenge.Scheme.Basic, "", out var token));
        Assert.Equal(basicToken, token);
        // Act
        response = await client.SendAsync(request, CancellationToken.None);
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

        HttpResponseMessage MockHttpRequestHandler(HttpRequestMessage req, CancellationToken cancellationToken)
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
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}");

        // Act
        var exception = await Assert.ThrowsAsync<AuthenticationException>(async () => await client.SendAsync(request, CancellationToken.None));

        // Assert
        Assert.Equal("Missing username or password for basic authentication.", exception.Message);
    }

    [Fact]
    public async Task ResolveCredentialAsync_NullCredentialHelper_ReturnsEmptyCredential()
    {
        // Arrange
        var client = new Client();

        // Act
        var result = await client.ResolveCredentialAsync("anyregistry", CancellationToken.None);

        // Assert
        Assert.True(result.IsEmpty());
    }

    [Fact]
    public async Task ResolveCredentialAsync_CredentialHelperConfigured_ReturnsExpectedCredential()
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
            .Setup(helper => helper.ResolveCredentialAsync("myregistry", It.IsAny<CancellationToken>()))
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
}
