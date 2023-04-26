using System;

namespace Oras.Exceptions
{
    /// <summary>
    /// InvalidReferenceException is thrown when the reference is invlid
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
