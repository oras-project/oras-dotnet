using OrasProject.Oras.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrasProject.Oras.Oci
{
    public static class DescriptorExtension
    {
        /// <summary>
        /// LimitSize throws SizeLimitExceededException if the size of desc exceeds the limit limitSize.
        /// </summary>
        /// <param name="desc"></param>
        /// <param name="limitSize"></param>
        /// <exception cref="SizeLimitExceededException"></exception>
        public static void LimitSize(this Descriptor desc, long limitSize)
        {
            if (desc.Size > limitSize)
            {
                throw new SizeLimitExceededException($"content size {desc.Size} exceeds MaxMetadataBytes {limitSize}");
            }
        }
    }
}
