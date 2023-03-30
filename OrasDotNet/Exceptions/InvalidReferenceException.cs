using System;
using System.Collections.Generic;
using System.Text;

namespace Oras.Exceptions
{
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
