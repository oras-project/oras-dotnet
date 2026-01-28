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

using System.Collections.Generic;

namespace OrasProject.Oras.Content;

/// <summary>
/// Options for resolving references.
/// This class is designed for use across resolve APIs including <see cref="IResolvable"/>.
/// </summary>
/// <remarks>
/// Not all options apply to all implementations. HTTP-based implementations
/// (e.g., <see cref="Registry.Remote.BlobStore"/>, <see cref="Registry.Remote.ManifestStore"/>)
/// support all options. Local or in-memory implementations may ignore
/// options that are not applicable to their transport.
/// </remarks>
public class ResolveOptions
{
    /// <summary>
    /// Custom HTTP headers to include in the resolve request.
    /// </summary>
    /// <remarks>
    /// This property is only honored by HTTP-based registry implementations.
    /// Non-HTTP implementations (e.g., local OCI layout stores) will ignore this property.
    /// </remarks>
    public IDictionary<string, IEnumerable<string>>? Headers { get; init; }
}
