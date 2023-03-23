﻿using System;
using System.Collections.Generic;
using System.Text;

namespace OrasDotNet.Models.Errors
{
    internal class InvalidDigestException : Exception
    {
        public InvalidDigestException() : base("invalid digest") { }
    }
}