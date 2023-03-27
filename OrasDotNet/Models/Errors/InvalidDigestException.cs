using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
<<<<<<< HEAD
    internal class InvalidDigestException : Exception
    {
        public InvalidDigestException() : base("invalid digest") { }
=======
    public class InvalidDigestException : Exception
    {
        public InvalidDigestException()
        {
        }

        public InvalidDigestException(string message)
            : base(message)
        {
        }

        public InvalidDigestException(string message, Exception inner)
            : base(message, inner)
        {
        }
>>>>>>> interface
    }
}
