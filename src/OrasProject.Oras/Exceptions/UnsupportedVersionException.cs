using System;

namespace OrasProject.Oras.Exceptions
{
    public class UnsupportedVersionException : Exception
    {
        /// <summary>
        /// UnsupportedVersionException is thrown when a version is not supported
        /// </summary>
        public UnsupportedVersionException()
        {
        }

        public UnsupportedVersionException(string message)
            : base(message)
        {
        }

        public UnsupportedVersionException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
