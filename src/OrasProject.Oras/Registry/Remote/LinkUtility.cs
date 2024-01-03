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
using System.Linq;
using System.Net.Http;

namespace OrasProject.Oras.Registry.Remote;

internal class LinkUtility
{
    /// <summary>
    /// ParseLink returns the URL of the response's "Link" header, if present.
    /// </summary>
    /// <param name="resp"></param>
    /// <returns></returns>
    internal static string ParseLink(HttpResponseMessage resp)
    {
        string link;
        if (resp.Headers.TryGetValues("Link", out var values))
        {
            link = values.FirstOrDefault();
        }
        else
        {
            throw new NoLinkHeaderException();
        }

        if (link[0] != '<')
        {
            throw new Exception($"invalid next link {link}: missing '<");
        }
        if (link.IndexOf('>') is var index && index == -1)
        {
            throw new Exception($"invalid next link {link}: missing '>'");
        }
        else
        {
            link = link[1..index];
        }

        if (!Uri.IsWellFormedUriString(link, UriKind.RelativeOrAbsolute))
        {
            throw new Exception($"invalid next link {link}");
        }

        var scheme = resp.RequestMessage.RequestUri.Scheme;
        var authority = resp.RequestMessage.RequestUri.Authority;
        Uri baseUri = new Uri(scheme + "://" + authority);
        Uri resolvedUri = new Uri(baseUri, link);

        return resolvedUri.AbsoluteUri;
    }


    /// <summary>
    /// NoLinkHeaderException is thrown when a link header is missing.
    /// </summary>
    internal class NoLinkHeaderException : Exception
    {
        public NoLinkHeaderException()
        {
        }

        public NoLinkHeaderException(string message)
            : base(message)
        {
        }

        public NoLinkHeaderException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
