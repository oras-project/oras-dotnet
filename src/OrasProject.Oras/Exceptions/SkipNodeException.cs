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

namespace OrasProject.Oras.Exceptions;

/// <summary>
/// SkipNodeException signals to stop copying a node. When thrown from PreCopy the blob must exist in the target.
/// This can be used to signal that a blob has been made available in the target repository by "Mount()" or some other technique.
/// </summary>
public class SkipNodeException : Exception
{
    public SkipNodeException() : base("Skip node")
    {
    }

    public SkipNodeException(string? message) : base(message)
    {
    }

    public SkipNodeException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}

