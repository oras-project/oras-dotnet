using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
