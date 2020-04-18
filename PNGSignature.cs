using System;
using System.Collections.Generic;
using System.Text;

namespace PNGUIN
{
    /// The original source of this class can be found at https://github.com/murrple-1/APNGManagement/tree/master/APNGLib.

    /// <summary>
    /// The universal 8 byte header of a PNG.
    /// </summary>
    internal static class PNGSignature
    {
        public static byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static void Compare(byte[] sig)
        {
            if (sig.Length == Signature.Length)
            {
                for (int i = 0; i < Signature.Length; i++)
                {
                    if (Signature[i] != sig[i])
                    {
                        throw new ApplicationException("PNG signature not found.");
                    }
                }
            }
            else
            {
                throw new ApplicationException("PNG signature not found.");
            }
        }
    }
}
