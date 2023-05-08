using Oras.Exceptions;

namespace Oras.Remote
{
    public class ReferenceObj
    {
        /// <summary>
        /// Registry is the name of the registry. It is usually the domain name of the registry optionally with a port.
        /// </summary>
        public string Registry { get; set; }

        /// <summary>
        /// Repository is the name of the repository.
        /// </summary>
        public string Repository { get; set; }

        /// <summary>
        /// Ref is the reference of the object in the repository. This field
        /// can take any one of the four valid forms (see ParseReference). In the
        /// case where it's the empty string, it necessarily implies valid form D,
        /// and where it is non-empty, then it is either a tag, or a digest
        /// (implying one of valid forms A, B, or C).
        /// </summary>
        public string Reference { get; set; }

        /// <summary>
        /// repositoryRegexp is adapted from the distribution implementation. The
        /// repository name set under OCI distribution spec is a subset of the docker
        /// repositoryRegexp is adapted from the distribution implementation. The
        /// spec. For maximum compatability, the docker spec is verified client-side.
        /// Further checks are left to the server-side.
        /// References:
        /// - https://github.com/distribution/distribution/blob/v2.7.1/reference/regexp.go#L53
        /// - https://github.com/opencontainers/distribution-spec/blob/v1.1.0-rc1/spec.md#pulling-manifests
        /// </summary>
        const string repositoryRegexp = @"^[a-z0-9]+(?:(?:[._]|__|[-]*)[a-z0-9]+)*(?:/[a-z0-9]+(?:(?:[._]|__|[-]*)[a-z0-9]+)*)*$";

        /// <summary>
        /// tagRegexp checks the tag name.
        /// The docker and OCI spec have the same regular expression.
        /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.0-rc1/spec.md#pulling-manifests
        /// </summary>
        const string tagRegexp = @"^[\w][\w.-]{0,127}$";

        public ReferenceObj ParseReference(string artifact)
        {
            var parts = artifact.Split("/", 2);
            if (parts.Length == 1)
            {
                throw new InvalidReferenceException($"missing repository");
            }
            (var registry, var path) = (parts[0], parts[1]);
            bool isTag;
            string repository;
            string reference;
            var index = path.IndexOf("@");
            if (index != -1)
            {
                // digest found; Valid From A (if not B)
                isTag = false;
                repository = path.Substring(0, index);
                reference = path.Substring(index + 1);

                if (repository.IndexOf(":") is var colonIndex && colonIndex != -1)
                {
                    // Valid From B
                    throw new InvalidReferenceException($"invalid reference format: {artifact}");
                }
            }

            return default;
        }
    }
}
