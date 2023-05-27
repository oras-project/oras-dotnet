using System;
using System.Linq;
using System.Net.Http;

namespace Oras.Remote
{
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
        /// ObtainUrl returns the link with a scheme and authority.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="plainHttp"></param>
        /// <returns></returns>
        internal static string ObtainUrl(string url, bool plainHttp)
        {
            if (plainHttp)
            {
                if (!url.Contains("http"))
                {
                    url = "http://" + url;
                }
            }
            else
            {
                if (!url.Contains("https"))
                {
                    url = "https://" + url;
                }
            }

            return url;
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
}
