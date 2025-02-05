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
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OrasProject.Oras.Registry;

public class Reference
{
    /// <summary>
    /// Registry is the name of the registry. It is usually the domain name of the registry optionally with a port.
    /// </summary>
    public string Registry
    {
        get => _registry;
        set => _registry = ValidateRegistry(value);
    }

    /// <summary>
    /// Repository is the name of the repository.
    /// </summary>
    public string? Repository
    {
        get => _repository;
        set => _repository = value == null ? null : ValidateRepository(value);
    }

    /// <summary>
    /// Reference is the reference of the object in the repository. This field
    /// can take any one of the four valid forms (see ParseReference). In the
    /// case where it's the empty string, it necessarily implies valid form D,
    /// and where it is non-empty, then it is either a tag, or a digest
    /// (implying one of valid forms A, B, or C).
    /// </summary>
    public string? ContentReference
    {
        get => _reference;
        set
        {
            if (value == null)
            {
                _reference = value;
                _isTag = false;
                return;
            }

            if (value.Contains(':'))
            {
                _reference = ValidateReferenceAsDigest(value);
                _isTag = false;
                return;
            }

            _reference = ValidateReferenceAsTag(value);
            _isTag = true;
        }
    }

    /// <summary>
    /// Host returns the host name of the registry
    /// </summary>
    public string Host => _registry == "docker.io" ? "registry-1.docker.io" : _registry;

    /// <summary>
    /// Digest returns the reference as a Digest
    /// </summary>
    public string Digest
    {
        get
        {
            if (_reference == null)
            {
                throw new InvalidReferenceException("Null content reference");
            }
            if (_isTag)
            {
                throw new InvalidReferenceException("Not a digest");
            }
            return _reference;
        }
    }

    /// <summary>
    /// Digest returns the reference as a Tag
    /// </summary>
    public string Tag
    {
        get
        {
            if (_reference == null)
            {
                throw new InvalidReferenceException("Null content reference");
            }
            if (!_isTag)
            {
                throw new InvalidReferenceException("Not a tag");
            }
            return _reference;
        }
    }

    private string _registry;
    private string? _repository;
    private string? _reference;
    private bool _isTag;

    /// <summary>
    /// repositoryRegexp is adapted from the distribution implementation. The
    /// repository name set under OCI distribution spec is a subset of the docker
    /// repositoryRegexp is adapted from the distribution implementation. The
    /// spec. For maximum compatability, the docker spec is verified client-side.
    /// Further checks are left to the server-side.
    /// References:
    /// - https://github.com/distribution/distribution/blob/v2.7.1/reference/regexp.go#L53
    /// - https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#pulling-manifests
    /// </summary>
    private const string _repositoryRegexPattern = @"^[a-z0-9]+(?:(?:[._]|__|[-]*)[a-z0-9]+)*(?:/[a-z0-9]+(?:(?:[._]|__|[-]*)[a-z0-9]+)*)*$";
    private static readonly Regex _repositoryRegex = new Regex(_repositoryRegexPattern, RegexOptions.Compiled);

    /// <summary>
    /// tagRegexp checks the tag name.
    /// The docker and OCI spec have the same regular expression.
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#pulling-manifests
    /// </summary>
    private const string _tagRegexPattern = @"^[\w][\w.-]{0,127}$";
    private static readonly Regex _tagRegex = new Regex(_tagRegexPattern, RegexOptions.Compiled);

    public static Reference Parse(string reference)
    {
        var parts = reference.Split('/', 2);
        if (parts.Length == 1)
        {
            throw new InvalidReferenceException("Missing repository");
        }
        var registry = parts[0];
        var path = parts[1];

        var index = path.IndexOf('@');
        if (index != -1)
        {
            // digest found; Valid From A (if not B)
            var repository = path[..index];
            var contentReference = path[(index + 1)..];
            index = repository.IndexOf(':');
            if (index != -1)
            {
                // tag found ( and now dropped without validation ) since the
                // digest already present; Valid Form B
                repository = repository[..index];
            }
            var instance = new Reference(registry, repository)
            {
                _reference = ValidateReferenceAsDigest(contentReference),
                _isTag = false
            };
            return instance;
        }

        index = path.IndexOf(':');
        if (index != -1)
        {
            // tag found; Valid Form C
            var repository = path[..index];
            var contentReference = path[(index + 1)..];
            var instance = new Reference(registry, repository)
            {
                _reference = ValidateReferenceAsTag(contentReference),
                _isTag = true
            };
            return instance;
        }

        // empty `reference`; Valid Form D
        return new Reference(registry, path);
    }

    public static bool TryParse(string reference, [NotNullWhen(true)] out Reference? parsedReference)
    {
        try
        {
            parsedReference = Parse(reference);
            return true;
        }
        catch (InvalidReferenceException)
        {
            parsedReference = null;
            return false;
        }
    }

    public Reference(Reference other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }
        
        _registry = other.Registry;
        _repository = other.Repository;
        ContentReference = other.ContentReference;
    }

    public Reference(string registry) => _registry = ValidateRegistry(registry);

    public Reference(string registry, string? repository) : this(registry)
        => _repository = ValidateRepository(repository);

    public Reference(string registry, string? repository, string? reference) : this(registry, repository)
        => ContentReference = reference;

    private static string ValidateRegistry(string registry)
    {
        var url = "dummy://" + registry;
        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) || new Uri(url).Authority != registry)
        {
            throw new InvalidReferenceException("Invalid registry");
        }
        return registry;
    }

    private static string ValidateRepository(string? repository)
    {
        if (repository == null || !_repositoryRegex.IsMatch(repository))
        {
            throw new InvalidReferenceException("Invalid respository");
        }
        return repository;
    }

    private static string ValidateReferenceAsDigest(string? reference) => Content.Digest.Validate(reference);

    private static string ValidateReferenceAsTag(string? reference)
    {
        if (reference == null || !_tagRegex.IsMatch(reference))
        {
            throw new InvalidReferenceException("Invalid tag");
        }
        return reference;
    }
}
