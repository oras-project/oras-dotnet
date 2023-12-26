using System;

namespace OrasProject.Oras.Exceptions
{
    /// <summary>
    /// MissingReferenceException is thrown when a reference is missing.
    /// </summary>
    public class MissingReferenceException : Exception
    {
        public MissingReferenceException()
        {
        }

        public MissingReferenceException(string message)
            : base(message)
        {
        }

        public MissingReferenceException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
