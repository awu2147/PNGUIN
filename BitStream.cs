using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace PNGUIN
{
    /// <summary>
    /// Create a <see cref="BitStream"></see> from a <see cref="BitArray"></see> by exposing method to get bits.
    /// </summary>
    public class BitStream
    {
        /// <summary>
        /// Source of the bit stream.
        /// </summary>
        private BitArray bitArray;

        /// <summary>
        /// Current position in the bit array.
        /// </summary>
        private int position;

        /// <summary>
        /// A buffer than can be written to.
        /// </summary>
        public List<byte> OutBuffer = new List<byte>();


        /// <summary>
        /// A buffer than can be written to.
        /// </summary>
        public List<byte> OutBufferWithHeader = new List<byte>();

        public BitStream(BitArray bitArray)
        {
            this.bitArray = bitArray;
        }

        public int GetNextBit()
        {
            int bit = bitArray[position] ? 1 : 0;
            position++;
            return bit;
        }

        public int GetNextBits(int n)
        {
            var value = 0;
            for (int i = 0; i < n; i++)
            {
                int bit = bitArray[position] ? 1 : 0;
                value <<= 1;
                value |= bit;
                position++;
            }
            return value;
        }

        public int GetNextBitsReverse(int n)
        {
            var value = 0;
            for (int i = 0; i < n; i++)
            {
                int bit = bitArray[position] ? 1 : 0;
                bit <<= i;
                value |= bit;
                position++;
            }
            return value;
        }

        public ushort GetNextShortReverse()
        {
            var firstByte = (byte)GetNextBitsReverse(8);
            var secondByte = (byte)GetNextBitsReverse(8);
            var firstShort = (ushort)(secondByte << 8);
            var value = (ushort)(firstShort + firstByte);
            return value;
        }

        public void ResetPosition()
        {
            position = 0;
        }
    }
}
