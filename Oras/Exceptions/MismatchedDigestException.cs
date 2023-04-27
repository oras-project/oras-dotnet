using System;

namespace Oras.Exceptions
{
    /// <summary>
    /// MismatchedDigestException is thrown when a digest does not match the content.
    /// </summary>
    public class MismatchedDigestException : Exception
    {
        public MismatchedDigestException()
        {
        }

        public MismatchedDigestException(string message)
            : base(message)
        {
        }

        public MismatchedDigestException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
