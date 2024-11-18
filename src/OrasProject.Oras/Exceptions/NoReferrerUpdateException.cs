using System;

namespace OrasProject.Oras.Exceptions;

public class NoReferrerUpdateException : Exception
{
    public NoReferrerUpdateException()
    {
    }
    
    public NoReferrerUpdateException(string message)
        : base(message)
    {
    }

    public NoReferrerUpdateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
