using System;

namespace OrasProject.Oras.Exceptions
{
    /// <summary>
    /// AlreadyExistsException is thrown when a resource already exists.
    /// </summary>
    public class AlreadyExistsException : Exception
    {
        public AlreadyExistsException()
        {
        }

        public AlreadyExistsException(string message)
            : base(message)
        {
        }

        public AlreadyExistsException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
