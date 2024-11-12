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
/// InvalidDateTimeFormatException is thrown when a time format is invalid.
/// </summary>
public class InvalidDateTimeFormatException : FormatException
{
    public InvalidDateTimeFormatException()
    {
    }

    public InvalidDateTimeFormatException(string? message)
        : base(message)
    {
    }

    public InvalidDateTimeFormatException(string? message, Exception? inner)
        : base(message, inner)
    {
    }
}
