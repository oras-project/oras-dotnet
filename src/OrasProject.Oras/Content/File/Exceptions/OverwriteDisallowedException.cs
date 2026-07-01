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
/// OverwriteDisallowedException is thrown when an attempt to overwrite an existing file
/// is made and overwriting is not allowed.
/// </summary>
public class OverwriteDisallowedException : IOException
{
    private const string _defaultMessage = "Overwrite disallowed";

    public OverwriteDisallowedException()
        : base(_defaultMessage)
    {
    }

    public OverwriteDisallowedException(string? message)
        : base(message ?? _defaultMessage)
    {
    }

    public OverwriteDisallowedException(string? message, Exception? inner)
        : base(message ?? _defaultMessage, inner)
    {
    }
}
