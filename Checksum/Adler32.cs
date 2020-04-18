using System;
using System.Collections.Generic;
using System.Text;

namespace PNGUIN.Checksum
{
    /// The original source of this class can be found at https://opensource.apple.com/source/gcc/gcc-4061/libjava/java/util/zip/Adler32.java.auto.html.
    /// The CSharp conversion source can be found at https://github.com/icsharpcode/SharpZipLib/blob/master/src/ICSharpCode.SharpZipLib/Checksum/Adler32.cs.

    /// <summary>
    /// Computes Adler32 checksum for a stream of data. An Adler32 checksum is not as reliable as a CRC32 checksum, but a lot faster to compute.
    /// </summary>
    internal sealed class Adler32
    {
        /// <summary> largest prime smaller than 65536. </summary>
        private static readonly uint BASE = 65521;

        /// <summary> The Adler32 checksum so far. </summary>
        private uint checkValue;

        /// <summary> Initialise a default instance of <see cref="Adler32"></see> </summary>
        public Adler32() { Reset(); }

        /// <summary>
        /// Resets the Adler32 data checksum as if no update was ever called.
        /// </summary>
        public void Reset() { checkValue = 1; }

        /// <summary>
        /// Returns the Adler32 data checksum computed so far.
        /// </summary>
        public long Value { get { return checkValue; } }

        /// <summary>
        /// Updates the checksum with the byte b.
        /// </summary>
        /// <param name="bval">
        /// The data value to add. The high byte of the int is ignored.
        /// </param>
        public void Update(int bval)
        {
            uint s1 = checkValue & 0xFFFF;
            uint s2 = checkValue >> 16;

            s1 = (s1 + ((uint)bval & 0xFF)) % BASE;
            s2 = (s1 + s2) % BASE;

            checkValue = (s2 << 16) + s1;
        }

        /// <summary>
        /// Updates the Adler32 data checksum with the bytes taken from a block of data.
        /// </summary>
        /// <param name="buffer">Contains the data to update the checksum with.</param>
        public void Update(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            Update(new ArraySegment<byte>(buffer, 0, buffer.Length));
        }

        /// <summary>
        /// Update Adler32 data checksum based on a portion of a block of data.
        /// </summary>
        /// <param name = "segment">
        /// The chunk of data to add.
        /// </param>
        public void Update(ArraySegment<byte> segment)
        {
            uint s1 = checkValue & 0xFFFF;
            uint s2 = checkValue >> 16;
            var count = segment.Count;
            var offset = segment.Offset;
            while (count > 0)
            {
                int n = 3800;
                if (n > count)
                {
                    n = count;
                }
                count -= n;
                while (--n >= 0)
                {
                    s1 = s1 + (uint)(segment.Array[offset++] & 0xff);
                    s2 = s2 + s1;
                }
                s1 %= BASE;
                s2 %= BASE;
            }
            checkValue = (s2 << 16) | s1;
        }
    }
}
