using System;
using System.Collections.Generic;
using System.Text;

namespace PNGUIN.Checksum
{
    /// The original source of this class can be found at https://github.com/murrple-1/APNGManagement/tree/master/APNGLib.

    /// <summary> 
    /// Computes CRC32 checksum for a stream of data. 
    /// </summary>
    internal static class CRC
    {
        /// <summary>
        /// Initialize CRC32 to starting value.
        /// </summary>
        public const uint INITIAL_CRC = 0xFFFFFFFF;

        /// <summary>
        /// CRC memoization table.
        /// </summary>
        private static uint[] CRCTable => ConstructCRCTable();

        /// <summary>
        /// Construct the CRC memoization table.
        /// </summary>
        private static uint[] ConstructCRCTable()
        {
            var crcTable = new uint[256];

            for (uint n = 0; n < crcTable.Length; n++)
            {
                uint c = n;
                for (uint k = 0; k < 8; k++)
                {
                    if ((c & 1) != 0)
                    {
                        c = 0xedb88320 ^ (c >> 1);
                    }
                    else
                    {
                        c = c >> 1;
                    }
                }
                crcTable[n] = c;
            }
            return crcTable;
        }

        /// <summary> Generate a new CRC value. </summary>
        /// <param name="currentCRC"> The starting CRC value. </param>
        /// <param name="bytes"> The byte array used to calculate the CRC. </param>
        /// <param name="final"> If true, then no more byte to process. CRC value finalized by inverting the bits. </param>
        public static void UpdateCRC(ref uint currentCRC, byte[] bytes, bool final)
        {
            for (int n = 0; n < bytes.Length; n++)
            {
                currentCRC = CRCTable[(currentCRC ^ bytes[n]) & 0xff] ^ (currentCRC >> 8);
            }
            currentCRC = final ? ~currentCRC : currentCRC;
        }

    }
}
