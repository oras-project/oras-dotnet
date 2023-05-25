using Oras.Exceptions;
using System;
using System.Linq;
using System.Net.Http;

namespace Oras.Remote
{
    internal class Utils
    {
        /// <summary>
        /// defaultMaxMetadataBytes specifies the default limit on how many response
        /// bytes are allowed in the server's response to the metadata APIs.
        /// See also: Repository.MaxMetadataBytes
        /// </summary>
        const long defaultMaxMetadataBytes = 4 * 1024 * 1024; // 4 MiB

        /// <summary>
        /// ParseLink returns the URL of the response's "Link" header, if present.
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        public static string ParseLink(HttpResponseMessage resp)
        {
            var link = String.Empty;
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
            if (link.IndexOf('>') is var index && index < -1)
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
            Uri baseUri = new Uri(scheme+"://"+authority);
            Uri resolvedUri = new Uri(baseUri, link);

            return resolvedUri.AbsoluteUri;
        }

        /// <summary>
        /// LimitReader ensures that the read byte does not exceed n
        /// bytes. if n is less than or equal to zero, defaultMaxMetadataBytes is used.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static byte[] LimitReader(HttpContent content, long n)
        {
            if (n <= 0)
            {
                n = defaultMaxMetadataBytes;
            }

            var bytes = content.ReadAsByteArrayAsync().Result;

            if (bytes.Length > n)
            {
                throw new Exception($"response body exceeds the limit of {n} bytes");
            }

            return bytes;
        }

        /// <summary>
        /// NoLinkHeaderException is thrown when a link header is missing.
        /// </summary>
        public class NoLinkHeaderException : Exception
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
