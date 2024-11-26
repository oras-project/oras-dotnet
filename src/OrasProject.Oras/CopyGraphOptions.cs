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
    /// PreCopy handles the current descriptor before it is copied.
    /// </summary>
    public event Func<Descriptor, Task>? PreCopy;

    /// <summary>
    /// PostCopy handles the current descriptor after it is copied.
    /// </summary>
    public event Func<Descriptor, Task>? PostCopy;

    /// <summary>
    /// CopySkipped will be called when the sub-DAG rooted by the current node
    /// is skipped.
    /// </summary>
    public event Func<Descriptor, Task>? CopySkipped;

    internal Task OnPreCopyAsync(Descriptor descriptor)
    {
        return PreCopy?.InvokeAsync(descriptor) ?? Task.CompletedTask;
    }

    internal Task OnPostCopyAsync(Descriptor descriptor)
    {
        return PostCopy?.InvokeAsync(descriptor) ?? Task.CompletedTask;
    }

    internal Task OnCopySkippedAsync(Descriptor descriptor)
    {
        return CopySkipped?.Invoke(descriptor) ?? Task.CompletedTask;
    }
}
