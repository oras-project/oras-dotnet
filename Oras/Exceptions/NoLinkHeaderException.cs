using System;

namespace Oras.Exceptions
{
    /// <summary>
    /// NoLinkHeaderException is thrown when a link header is missing.
    /// </summary>
    public class NoLinkHeaderException : Exception
    {
        public NoLinkHeaderException()
        {
        }

        public NoLinkHeaderException(string message)
            : base(message)
        {
        }

        public NoLinkHeaderException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
