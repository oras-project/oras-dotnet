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

using Moq;
using Moq.Protected;
using OrasProject.Oras.Oci;
using System.Diagnostics.CodeAnalysis;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace OrasProject.Oras.Tests.Remote.Util;

public class Util
{
    /// <summary>
    /// AreDescriptorsEqual compares two descriptors and returns true if they are equal.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool AreDescriptorsEqual([AllowNull] Descriptor a, [AllowNull] Descriptor b)
    {
        if (a == null && b == null)
        {
            return true;
        }
        else if (a != null && b != null)
        {
            return a.MediaType == b.MediaType &&
                    a.Digest == b.Digest &&
                    a.Size == b.Size;
        }

        return false;
    }

    /// <summary>
    /// Creates a PlainClient with a custom mock handler for testing.
    /// </summary>
    /// <param name="func">Function to handle HTTP requests and return responses.</param>
    /// <returns>A PlainClient configured with the mock handler.</returns>
    /// <remarks>
    /// The same HttpClient is used for both redirect and no-redirect scenarios in tests.
    /// The mock handler controls whether responses are redirects or direct, allowing tests
    /// to verify response handling logic without depending on actual HttpClient redirect behavior.
    /// </remarks>
    public static IClient CustomClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> func)
    {
        var moqHandler = CustomHandler(func);
        var httpClient = new HttpClient(moqHandler.Object);
        return new PlainClient(httpClient, httpClient);
    }

    /// <summary>
    /// Creates a PlainClient with a custom mock handler for testing.
    /// </summary>
    /// <param name="func">Function to handle HTTP requests and return responses.</param>
    /// <returns>A PlainClient configured with the mock handler.</returns>
    /// <remarks>
    /// The same HttpClient is used for both redirect and no-redirect scenarios in tests.
    /// The mock handler controls whether responses are redirects or direct, allowing tests
    /// to verify response handling logic without depending on actual HttpClient redirect behavior.
    /// </remarks>
    public static IClient CustomClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
    {
        var moqHandler = CustomHandler(func);
        var httpClient = new HttpClient(moqHandler.Object);
        return new PlainClient(httpClient, httpClient);
    }

    public static Mock<DelegatingHandler> CustomHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> func)
    {
        var moqHandler = new Mock<DelegatingHandler>();
        moqHandler.Protected().Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        ).ReturnsAsync(func);
        return moqHandler;
    }
    public static Mock<DelegatingHandler> CustomHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
    {
        var moqHandler = new Mock<DelegatingHandler>();
        moqHandler.Protected().Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        ).Returns(func);
        return moqHandler;
    }

    public static Descriptor ZeroDescriptor() => new()
    {
        MediaType = "",
        Digest = "",
        Size = 0
    };
}
