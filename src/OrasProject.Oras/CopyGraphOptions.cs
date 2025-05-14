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

using System;
using System.Threading.Tasks;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras;

/// <summary>
/// CopyGraphOptions contains parameters for <see cref="Extensions.CopyGraphAsync(OrasProject.Oras.ITarget,OrasProject.Oras.ITarget,OrasProject.Oras.Oci.Descriptor,System.Threading.CancellationToken)"/>
/// </summary>
public struct CopyGraphOptions
{
    /// <summary>
    /// PreCopyAsync handles the current descriptor before it is copied.
    /// </summary>
    public event Func<Descriptor, Task>? PreCopyAsync;

    /// <summary>
    /// PreCopy handles the current descriptor before it is copied.
    /// </summary>
    public event Action<Descriptor>? PreCopy;

    /// <summary>
    /// PostCopyAsync handles the current descriptor after it is copied.
    /// </summary>
    public event Func<Descriptor, Task>? PostCopyAsync;

    /// <summary>
    /// PostCopy handles the current descriptor after it is copied.
    /// </summary>
    public event Action<Descriptor>? PostCopy;

    /// <summary>
    /// CopySkippedAsync will be called when the sub-DAG rooted by the current node
    /// is skipped.
    /// </summary>
    public event Func<Descriptor, Task>? CopySkippedAsync;

    /// <summary>
    /// CopySkipped will be called when the sub-DAG rooted by the current node
    /// is skipped.
    /// </summary>
    public event Action<Descriptor>? CopySkipped;

    internal readonly Task OnPreCopyAsync(Descriptor descriptor)
    {
        var tasks = PreCopyAsync.InvokeAsyncEvents(descriptor);
        PreCopy?.Invoke(descriptor);
        return Task.WhenAll(tasks);
    }

    internal readonly Task OnPostCopyAsync(Descriptor descriptor)
    {
        var tasks = PostCopyAsync.InvokeAsyncEvents(descriptor);
        PostCopy?.Invoke(descriptor);
        return Task.WhenAll(tasks);
    }

    internal readonly Task OnCopySkippedAsync(Descriptor descriptor)
    {
        var tasks = CopySkippedAsync.InvokeAsyncEvents(descriptor);
        CopySkipped?.Invoke(descriptor);
        return Task.WhenAll(tasks);
    }
}
