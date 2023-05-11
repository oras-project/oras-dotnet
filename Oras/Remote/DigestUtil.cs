using Oras.Exceptions;
using System.Text.RegularExpressions;

namespace Oras.Remote
{
    internal class DigestUtil
    {
        /// <summary>
        /// ParseDigest verifies the digest header and throws an exception if it is invalid.
        /// </summary>
        /// <param name="digest"></param>
        /// <exception cref="InvalidReferenceException"></exception>
        public static string Parse(string digest)
        {
            if (!Regex.IsMatch(digest, ReferenceObj.digestRegexp))
            {
                throw new InvalidReferenceException($"invalid reference format: {digest}");
            }

            return digest;
        }
    }
}
