using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Interfaces.Registry
{
    /// <summary>
    /// ITagLister lists tags by the tag service.
    /// </summary>
    public interface ITagLister
    {
        /// <summary>
        /// TagsAsync lists the tags available in the repository.
        /// Since the returned tag list may be paginated by the underlying
        /// implementation, a function should be passed in to process the paginated
        /// tag list.
        /// Note: When implemented by a remote registry, the tags API is called.
        /// However, not all registries supports pagination or conforms the
        /// specification.
        /// References:
        /// - https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md
        /// - https://docs.docker.com/registry/spec/api/#tags
        /// </summary>
        /// <param name="last"> The `last` parameter when invoking the tags API. If `last` is NOT empty, the entries in the response start after the tag specified by `last`. Otherwise, the response starts from the top of the Tags list.</param>
        /// <param name="fn"> The function to process the paginated tag list</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task TagsAsync(string last, Action<string[]> fn, CancellationToken cancellationToken = default);
    }
}
