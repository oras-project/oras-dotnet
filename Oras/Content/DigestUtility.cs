using Oras.Exceptions;
using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Oras.Content
{
    internal class DigestUtility
    {

        /// <summary>
        /// digestRegexp checks the digest.
        /// </summary>
        public static string digestRegexp = @"[a-z0-9]+(?:[.+_-][a-z0-9]+)*:[a-zA-Z0-9=_-]+";

        /// <summary>
        /// Parse verifies the digest header and throws an exception if it is invalid.
        /// </summary>
        /// <param name="digest"></param>
        public static string Parse(string digest)
        {
            if (!Regex.IsMatch(digest, digestRegexp))
            {
                throw new InvalidReferenceException($"invalid reference format: {digest}");
            }

            return digest;
        }

        /// <summary>
        /// CalculateSHA256DigestFromBytes generates a digest from a byte.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static string CalculateSHA256DigestFromBytes(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content);
            var digest = $"sha256:{Convert.ToBase64String(hash)}";
            return digest;
        }
    }
}
