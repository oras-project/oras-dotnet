using System;
using System.IO;

namespace OrasProject.Oras.Content
{
    public class MismatchedSizeException : IOException
    {
        public MismatchedSizeException()
        {
        }

        public MismatchedSizeException(string? message)
            : base(message)
        {
        }

        public MismatchedSizeException(string? message, Exception? inner)
            : base(message, inner)
        {
        }
    }
}
