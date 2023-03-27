using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
<<<<<<< HEAD
    internal class MissingReferenceException : Exception
    {
        public MissingReferenceException() : base("missing reference") { }
=======
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
>>>>>>> interface
    }
}
