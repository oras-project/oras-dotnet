using Oras.Exceptions;
using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Oras.Content
{
    internal static class DigestUtility
    {

        /// <summary>
        /// digestRegexp checks the digest.
        /// </summary>
        private const string digestRegexp = @"[a-z0-9]+(?:[.+_-][a-z0-9]+)*:[a-zA-Z0-9=_-]+";

        /// <summary>
        /// ParseDigest verifies the digest header and throws an exception if it is invalid.
        /// </summary>
        /// <param name="digest"></param>
        internal static string ParseDigest(string digest)
        {
            if (!Regex.IsMatch(digest, digestRegexp))
            {
                throw new InvalidDigestException($"Invalid digest: {digest}");
            }

            return digest;
        }

        /// <summary>
        /// CalculateSHA256DigestFromBytes generates a SHA256 digest from a byte array.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        internal static string CalculateSHA256DigestFromBytes(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content);
            var output = $"{nameof(SHA256)}:{BitConverter.ToString(hash).Replace("-", "")}";
            return output.ToLower();
        }
    }
}
