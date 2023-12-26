using System;

namespace OrasProject.Oras.Exceptions
{
    /// <summary>
    /// NotFoundException is thrown when a resource is not found.
    /// </summary>
    public class NotFoundException : Exception
    {
        public NotFoundException()
        {
        }

        public NotFoundException(string message)
            : base(message)
        {
        }

        public NotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
