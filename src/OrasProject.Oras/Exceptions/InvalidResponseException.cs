using System;
using System.IO;

namespace OrasProject.Oras.Exceptions;

public class InvalidResponseException : FormatException
{
    public InvalidResponseException()
    {
    }

    public InvalidResponseException(string? message)
        : base(message)
    {
    }

    public InvalidResponseException(string? message, Exception? inner)
        : base(message, inner)
    {
    }
}
