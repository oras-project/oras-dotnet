using System;

namespace OrasProject.Oras.Exceptions
{
    /// <summary>
    /// InvalidDescriptorSizeException is thrown when a descriptor size is invalid.
    /// </summary>
    public class InvalidDescriptorSizeException : Exception
    {
        public InvalidDescriptorSizeException()
        {
        }

        public InvalidDescriptorSizeException(string message)
            : base(message)
        {
        }

        public InvalidDescriptorSizeException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
