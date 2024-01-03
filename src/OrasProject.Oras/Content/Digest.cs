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

using OrasProject.Oras.Exceptions;
using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OrasProject.Oras.Content;

internal static class Digest
{
    private const string digestRegexPattern = @"[a-z0-9]+(?:[.+_-][a-z0-9]+)*:[a-zA-Z0-9=_-]+";
    private static readonly Regex digestRegex = new Regex(digestRegexPattern, RegexOptions.Compiled);

    /// <summary>
    /// Verifies the digest header and throws an exception if it is invalid.
    /// </summary>
    /// <param name="digest"></param>
    internal static string Validate(string? digest)
    {
        if (string.IsNullOrEmpty(digest) || !digestRegex.IsMatch(digest))
        {
            throw new InvalidDigestException($"Invalid digest: {digest}");
        }
        return digest;
    }

    /// <summary>
    /// Generates a SHA-256 digest from a byte array.
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    internal static string ComputeSHA256(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(content);
        var output = $"sha256:{BitConverter.ToString(hash).Replace("-", "")}";
        return output.ToLower();
    }
}
