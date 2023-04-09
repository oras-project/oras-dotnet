using System;
using System.Collections.Generic;
using System.Text;

namespace Oras.Exceptions
{
    /// <summary>
    /// InvalidDigestException is thrown when a digest is invalid.
    /// </summary>
    public class InvalidReferenceException : Exception
    {
        public InvalidReferenceException()
        {
        }

        public InvalidReferenceException(string message)
            : base(message)
        {
        }

        public InvalidReferenceException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
