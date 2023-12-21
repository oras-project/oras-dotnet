using System;

namespace Oras.Exceptions
{
    /// <summary>
    /// SizeExceedsLimitException is thrown when a size exceeds the limit.
    /// </summary>
    public class SizeExceedsLimitException : Exception
    {
        public SizeExceedsLimitException()
        {
        }

        public SizeExceedsLimitException(string message)
            : base(message)
        {
        }

        public SizeExceedsLimitException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
