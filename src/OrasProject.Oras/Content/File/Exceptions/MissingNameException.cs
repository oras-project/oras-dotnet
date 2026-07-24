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

namespace OrasProject.Oras.Content.File.Exceptions;

/// <summary>
/// MissingNameException is thrown when a name is required but not provided.
/// </summary>
public class MissingNameException : ArgumentException
{
    private const string _defaultMessage = "Missing name";

    public MissingNameException()
        : base(_defaultMessage)
    {
    }

    public MissingNameException(string? message)
        : base(message ?? _defaultMessage)
    {
    }

    public MissingNameException(string? message, Exception? inner)
        : base(message ?? _defaultMessage, inner)
    {
    }
}
