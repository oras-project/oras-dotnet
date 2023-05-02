using System;
using System.Threading.Tasks;

namespace Oras.Interfaces
{
    /// <summary>
    /// ITagLister lists tags by the tag service.
    /// </summary>
    internal interface ITagLister
    {
        /// <summary>
        /// Tags lists the tags available in the repository.
        /// Since the returned tag list may be paginated by the underlying
        /// implementation, a function should be passed in to process the paginated
        /// tag list.
        /// `last` argument is the `last` parameter when invoking the tags API.
        /// If `last` is NOT empty, the entries in the response start after the
        /// tag specified by `last`. Otherwise, the response starts from the top
        /// of the Tags list.
        /// Note: When implemented by a remote registry, the tags API is called.
        /// However, not all registries supports pagination or conforms the
        /// specification.
        /// References:
        /// - https://github.com/opencontainers/distribution-spec/blob/v1.1.0-rc1/spec.md#content-discovery
        /// - https://docs.docker.com/registry/spec/api/#tags
        /// See also `Tags()` in this package.
        /// </summary>
        /// <param name="last"></param>
        /// <param name="fn"></param>
        /// <returns></returns>
        Task TagsAsync(string last, Func<string[], Task> fn);
    }
}
