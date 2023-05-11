using Oras.Exceptions;
using System;
using System.Linq;
using System.Net.Http;

namespace Oras.Remote
{
    internal class Utils
    {
        /// <summary>
        /// ParseLink returns the URL of the response's "Link" header, if present.
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        public static string ParseLink(HttpResponseMessage resp)
        {
            var link = resp.Headers.GetValues("Link").FirstOrDefault();
            if (String.IsNullOrEmpty(link))
            {
                throw new NoLinkHeaderException();
            }

            if (link[0] != '<')
            {
                throw new Exception($"invalid next link {link}: missing '<");
            }
            if (link.IndexOf('>') is var index && index < -1)
            {
                throw new Exception($"invalid next link {link}: missing '>'");
            }
            else
            {
                link = link[1..index];
            }

            if (!Uri.IsWellFormedUriString(link, UriKind.Absolute))
            {
                throw new Exception($"invalid next link {link}: not an absolute URL");
            }

            return link;
        }
    }
}
