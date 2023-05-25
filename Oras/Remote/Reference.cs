using Oras.Exceptions;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Oras.Content;
using static Oras.Content.DigestUtility;

namespace Oras.Remote
{
    public class RemoteReference
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
        /// Reference is the reference of the object in the repository. This field
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
        /// - https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#pulling-manifests
        /// </summary>
        public static string repositoryRegexp = @"^[a-z0-9]+(?:(?:[._]|__|[-]*)[a-z0-9]+)*(?:/[a-z0-9]+(?:(?:[._]|__|[-]*)[a-z0-9]+)*)*$";

        /// <summary>
        /// tagRegexp checks the tag name.
        /// The docker and OCI spec have the same regular expression.
        /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#pulling-manifests
        /// </summary>
        public static string tagRegexp = @"^[\w][\w.-]{0,127}$";

        /// <summary>
        /// digestRegexp checks the digest.
        /// </summary>
        public static string digestRegexp = @"[a-z0-9]+(?:[.+_-][a-z0-9]+)*:[a-zA-Z0-9=_-]+";
        public RemoteReference ParseReference(string artifact)
        {
            var parts = artifact.Split('/', 2);
            if (parts.Length == 1)
            {
                throw new InvalidReferenceException($"missing repository");
            }
            (var registry, var path) = (parts[0], parts[1]);
            bool isTag = false;
            string repository;
            string reference = String.Empty;

            if (path.IndexOf('@') is var index && index != -1)
            {
                // digest found; Valid From A (if not B)
                isTag = false;
                repository = path[..index];
                reference = path[(index + 1)..];

                if (repository.IndexOf(':') is var indexOfColon && indexOfColon != -1)
                {
                    // tag found ( and now dropped without validation ) since the
                    // digest already present; Valid Form B
                    repository = repository[..indexOfColon];
                }
            }
            else if (path.IndexOf(':') is var indexOfColon && indexOfColon != -1)
            {
                // tag found; Valid Form C
                isTag = true;
                repository = path[..indexOfColon];
                reference = path[(indexOfColon + 1)..];
            }
            else
            {
                // empty `reference`; Valid Form D
                repository = path;
            }
            var remoteReference = new RemoteReference
            {
                Registry = registry,
                Repository = repository,
                Reference = reference
            };

            remoteReference.ValidateRegistry();
            remoteReference.ValidateRepository();

            if (reference.Length == 0)
            {
                return remoteReference;
            }

            if (isTag)
            {
                remoteReference.ValidateReferenceAsTag();
            }
            else
            {
                remoteReference.ValidateReferenceAsDigest();
            }
            return remoteReference;
        }

        /// <summary>
        /// ValidateReferenceAsDigest validates the reference as a digest.
        /// </summary>
        public void ValidateReferenceAsDigest()
        {
            if (!Regex.IsMatch(Reference, digestRegexp))
            {
                throw new InvalidReferenceException($"invalid reference format: {Reference}");
            }
        }


        /// <summary>
        /// ValidateRepository checks if the repository name is valid.
        /// </summary>
        /// <returns></returns>
        public void ValidateRepository()
        {
            if (!Regex.IsMatch(Repository, repositoryRegexp))
            {
                throw new InvalidReferenceException("Invalid Respository");
            }
        }

        /// <summary>
        /// ValidateRegistry checks if the registry path is valid.
        /// </summary>
        /// <returns></returns>
        public void ValidateRegistry()
        {
            var url = "dummy://" + this.Registry;
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) || new Uri(url).Authority != Registry)
            {
                throw new InvalidReferenceException("Invalid Registry");
            };
            
        }

        public void ValidateReferenceAsTag()
        {
            if (!Regex.IsMatch(Reference, tagRegexp))
            {
                throw new InvalidReferenceException("Invalid Tag");
            }
        }

        /// <summary>
        ///  ValidateReference where the reference is first tried as an ampty string, then
        ///  as a digest, and if that fails, as a tag.
        /// </summary>
        public void ValidateReference()
        {
            if (string.IsNullOrEmpty(Reference))
            {
                return;
            }
            if (Reference.IndexOf(':') != -1)
            {
                ValidateReferenceAsDigest();
                return;
            }
            else
            {
                ValidateReferenceAsTag();
            }
        }

        /// <summary>
        /// Host returns the host name of the registry
        /// </summary>
        /// <returns></returns>
        public string Host()
        {
            if (Registry == "docker.io")
            {
                return "registry-1.docker.io";
            }
            return Registry;
        }

        /// <summary>
        /// Digest returns the reference as a Digest
        /// </summary>
        /// <returns></returns>
        public string Digest()
        {
            ValidateReferenceAsDigest();
            return Reference;
        }

        /// <summary>
        /// VerifyContentDigest verifies the content digest of the artifact.
        /// </summary>
        /// <param name="resp"></param>
        /// <param name="refDigest"></param>
        /// <exception cref="Exception"></exception>
        internal static void VerifyContentDigest(HttpResponseMessage resp, string refDigest)
        {
            var digestStr = resp.Content.Headers.GetValues("Docker-Content-Digest").FirstOrDefault();
            if (String.IsNullOrEmpty(digestStr))
            {
                return;
            }

            string contentDigest;
            try
            {
                contentDigest = ParseDigest(digestStr);
            }
            catch (Exception)
            {
                throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: invalid response header: `Docker-Content-Digest: {digestStr}`");
            }
            if (contentDigest != refDigest)
            {
                throw new Exception($"{resp.RequestMessage.Method} {resp.RequestMessage.RequestUri}: invalid response; digest mismatch in Docker-Content-Digest: received {contentDigest} when expecting {refDigest}");
            }
        }

        /// <summary>
        /// ParseDigest parses the digest from the string.
        /// </summary>
        /// <param name="digestStr"></param>
        /// <returns></returns>
        public static string ParseDigest(string digestStr)
        {
            return ParseDigest(digestStr);
        }
    }
}
