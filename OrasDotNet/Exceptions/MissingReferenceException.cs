﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Oras.Exceptions
{
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
    }
}
