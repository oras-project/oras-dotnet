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
using System.IO;

namespace OrasProject.Oras.Content.File.Exceptions;

/// <summary>
/// DuplicateFileNameException is thrown when a name
/// already exists in the FileStore.
/// </summary>
public class DuplicateFileNameException : IOException
{
    public DuplicateFileNameException()
    {
    }

    public DuplicateFileNameException(string? message)
        : base(message)
    {
    }

    public DuplicateFileNameException(
        string? message,
        Exception? inner)
        : base(message, inner)
    {
    }
}
