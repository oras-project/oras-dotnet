using System;

namespace OrasProject.Oras.Exceptions;

public class ReferrersSupportLevelAlreadySetException : Exception
{
    public ReferrersSupportLevelAlreadySetException()
    {
    }

    public ReferrersSupportLevelAlreadySetException(string? message)
        : base(message)
    {
    }

    public ReferrersSupportLevelAlreadySetException(string? message, Exception? inner)
        : base(message, inner)
    {
    }
}
