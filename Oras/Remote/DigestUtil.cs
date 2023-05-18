using Oras.Exceptions;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Oras.Remote
{
    internal class DigestUtil
    {
        /// <summary>
        /// Parse verifies the digest header and throws an exception if it is invalid.
        /// </summary>
        /// <param name="digest"></param>
        public static string Parse(string digest)
        {
            if (!Regex.IsMatch(digest, ReferenceObj.digestRegexp))
            {
                throw new InvalidReferenceException($"invalid reference format: {digest}");
            }

            return digest;
        }

        /// <summary>
        /// FromBytes generates a digest from the content.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static string FromBytes(HttpContent content)
        {
            var digest = String.Empty;
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(content.ReadAsByteArrayAsync().Result);
                digest = $"sha256:{Convert.ToBase64String(hash)}";
                return digest;
            }
        }
    }
}
