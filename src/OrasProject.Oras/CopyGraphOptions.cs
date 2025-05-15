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
public class CopyGraphOptions
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



    /// <summary>
    /// Invokes the <see cref="PreCopyAsync"/> event handlers asynchronously for the specified descriptor,
    /// then invokes the <see cref="PreCopy"/> event handlers synchronously.
    /// </summary>
    /// <param name="descriptor">The <see cref="Descriptor"/> to process before copying.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation of all <see cref="PreCopyAsync"/> handlers.
    /// </returns>
    internal Task InvokePreCopyAsync(Descriptor descriptor)
    {
        var tasks = PreCopyAsync?.InvokeAsync(descriptor) ?? [Task.CompletedTask];
        PreCopy?.Invoke(descriptor);
        return Task.WhenAll(tasks);
    }


    /// <summary>
    /// Invokes the <see cref="PostCopyAsync"/> event handlers asynchronously for the specified descriptor,
    /// then invokes the <see cref="PostCopy"/> event handlers synchronously.
    /// </summary>
    /// <param name="descriptor">The <see cref="Descriptor"/> to process after copying.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation of all <see cref="PostCopyAsync"/> handlers.
    /// </returns>
    internal Task InvokePostCopyAsync(Descriptor descriptor)
    {
        var tasks = PostCopyAsync?.InvokeAsync(descriptor) ?? [Task.CompletedTask];
        PostCopy?.Invoke(descriptor);
        return Task.WhenAll(tasks);
    }


    /// <summary>
    /// Invokes the <see cref="CopySkippedAsync"/> event handlers asynchronously for the specified descriptor,
    /// then invokes the <see cref="CopySkipped"/> event handlers synchronously.
    /// </summary>
    /// <param name="descriptor">The <see cref="Descriptor"/> whose sub-DAG is being skipped.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation of all <see cref="CopySkippedAsync"/> handlers.
    /// </returns>
    internal Task InvokeCopySkippedAsync(Descriptor descriptor)
    {
        var tasks = CopySkippedAsync?.InvokeAsync(descriptor) ?? [Task.CompletedTask];
        CopySkipped?.Invoke(descriptor);
        return Task.WhenAll(tasks);
    }
}
