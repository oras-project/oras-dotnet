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
using OrasProject.Oras.Oci;

namespace OrasProject.Oras;

public struct CopyGraphOptions
{
    public event Action<Descriptor> PreCopy;

    public event Action<Descriptor> PostCopy;

    public event Action<Descriptor> CopySkipped;

    internal void OnPreCopy(Descriptor descriptor)
    {
        PreCopy?.Invoke(descriptor);
    }

    internal void OnPostCopy(Descriptor descriptor)
    {
        PostCopy?.Invoke(descriptor);
    }

    internal void OnCopySkipped(Descriptor descriptor)
    {
        CopySkipped?.Invoke(descriptor);
    }
}
