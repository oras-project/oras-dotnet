using Oras.Exceptions;
using Oras.Remote;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Oras.Utils
{
    internal class DigestUtility
    {
        /// <summary>
        /// Parse verifies the digest header and throws an exception if it is invalid.
        /// </summary>
        /// <param name="digest"></param>
        public static string Parse(string digest)
        {
            if (!Regex.IsMatch(digest, RemoteReference.digestRegexp))
            {
                throw new InvalidReferenceException($"invalid reference format: {digest}");
            }

            return digest;
        }

        /// <summary>
        /// FromBytes generates a digest from a byte.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static string FromBytes(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content);
            var digest = $"sha256:{Convert.ToBase64String(hash)}";
            return digest;
        }
    }
}
