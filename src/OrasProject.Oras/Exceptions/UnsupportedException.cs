using System;

namespace Oras.Exceptions
{
    /// <summary>
    /// UnsupportedException is thrown when a feature is not supported.
    /// </summary>
    public class UnsupportedException : Exception
    {
        public UnsupportedException()
        {
        }

        public UnsupportedException(string message)
            : base(message)
        {
        }

        public UnsupportedException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
