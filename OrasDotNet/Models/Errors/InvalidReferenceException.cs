using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
<<<<<<< HEAD
    internal class InvalidReferenceException : Exception
    {
        public InvalidReferenceException() : base("invalid reference") { }
=======
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
>>>>>>> interface
    }
}
