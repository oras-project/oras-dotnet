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
/// Exception thrown when a required name is missing.
/// </summary>
public class MissingNameException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingNameException"/> class.
    /// </summary>
    public MissingNameException() : base("missing name")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingNameException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public MissingNameException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingNameException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public MissingNameException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
