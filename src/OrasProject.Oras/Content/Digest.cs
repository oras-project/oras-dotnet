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

using OrasProject.Oras.Content.Exceptions;
using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OrasProject.Oras.Content;

internal static partial class Digest
{
    // Registered algorithm identifiers per OCI image-spec v1.1.1.
    internal const string AlgorithmSha256 = "sha256";
    internal const string AlgorithmSha512 = "sha512";

    // Validation error constants for programmatic consumption.
    internal const string ErrDigestEmpty = "Invalid digest. Digest string is empty.";
    internal const string ErrDigestInvalidFormat = "Invalid digest format.";
    internal const string ErrSha256InvalidEncoded = "Invalid sha256 digest. Encoded portion must be exactly 64 lowercase hex characters.";
    internal const string ErrSha512InvalidEncoded = "Invalid sha512 digest. Encoded portion must be exactly 128 lowercase hex characters.";

    // General digest grammar per OCI image-spec v1.1.1:
    //   digest            ::= algorithm ":" encoded
    //   algorithm         ::= algorithm-component (algorithm-separator algorithm-component)*
    //   algorithm-component ::= [a-z0-9]+
    //   algorithm-separator ::= [+._-]
    //   encoded           ::= [a-zA-Z0-9=_-]+
    // https://github.com/opencontainers/image-spec/blob/v1.1.1/descriptor.md#digests
    [GeneratedRegex(@"^[a-z0-9]+(?:[.+_-][a-z0-9]+)*:[a-zA-Z0-9=_-]+$", RegexOptions.Compiled)]
    private static partial Regex DigestRegex();

    // SHA-256 encoded portion: exactly 64 lowercase hex characters.
    // Per OCI image-spec v1.1.1: "the encoded portion MUST match /[a-f0-9]{64}/"
    [GeneratedRegex(@"^[a-f0-9]{64}$", RegexOptions.Compiled)]
    private static partial Regex Sha256EncodedRegex();

    // SHA-512 encoded portion: exactly 128 lowercase hex characters.
    // Per OCI image-spec v1.1.1: "the encoded portion MUST match /[a-f0-9]{128}/"
    [GeneratedRegex(@"^[a-f0-9]{128}$", RegexOptions.Compiled)]
    private static partial Regex Sha512EncodedRegex();

    /// <summary>
    /// Verifies the digest string and throws an exception if it is invalid.
    /// </summary>
    /// <param name="digest">The digest string to validate.</param>
    /// <returns>The validated digest string.</returns>
    /// <exception cref="InvalidDigestException">Thrown when the digest is invalid.</exception>
    internal static string Validate(string digest)
    {
        return TryValidate(digest, out var error)
            ? digest
            : throw new InvalidDigestException(error);
    }

    /// <summary>
    /// Validates a digest string per OCI image-spec v1.1.1.
    /// Registered algorithms (sha256, sha512) are validated strictly.
    /// Unrecognized algorithms pass if they match the general grammar.
    /// </summary>
    /// <param name="digest">The digest string to validate.</param>
    /// <param name="error">When this method returns false, contains the validation error.</param>
    /// <returns>true if valid; otherwise, false.</returns>
    internal static bool TryValidate(string digest, out string error)
    {
        if (string.IsNullOrEmpty(digest))
        {
            error = ErrDigestEmpty;
            return false;
        }

        if (!DigestRegex().IsMatch(digest))
        {
            error = $"{ErrDigestInvalidFormat} The digest '{digest}' does not match the required grammar.";
            return false;
        }

        var colonIndex = digest.IndexOf(':');
        var algorithm = digest[..colonIndex];
        var encoded = digest[(colonIndex + 1)..];

        // For registered algorithms, enforce per-algorithm encoded format
        // per OCI image-spec v1.1.1 descriptor.md#registered-algorithms.
        // Unrecognized algorithms pass validation if they match the general
        // grammar per spec: "Implementations SHOULD allow digests with
        // unrecognized algorithms to pass validation if they comply with
        // the above grammar."
        if (algorithm == AlgorithmSha256)
        {
            if (!Sha256EncodedRegex().IsMatch(encoded))
            {
                error = $"{ErrSha256InvalidEncoded} Got '{digest}'.";
                return false;
            }
        }
        else if (algorithm == AlgorithmSha512)
        {
            if (!Sha512EncodedRegex().IsMatch(encoded))
            {
                error = $"{ErrSha512InvalidEncoded} Got '{digest}'.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Generates a SHA-256 digest from a byte array.
    /// </summary>
    /// <param name="content">The content to hash.</param>
    /// <returns>The digest string in the format "sha256:&lt;hex&gt;".</returns>
    internal static string ComputeSha256(byte[] content)
    {
        var hash = SHA256.HashData(content);
        var output = $"{AlgorithmSha256}:{Convert.ToHexString(hash)}";
        return output.ToLowerInvariant();
    }
}
