using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PNGUIN
{
    /// <summary>
    /// The following class is a C# implementation of Mark Adler's puff.c https://github.com/madler/zlib/blob/master/contrib/puff/puff.c.
    /// </summary>
    public static class Puff
    {
        /// <summary>
        /// Maximum bits in a code.
        /// </summary>
        const int MAXBITS = 15;

        /// <summary>
        /// Maximum number of literal/length codes.
        /// </summary>
        const int MAXLCODES = 286;

        /// <summary>
        /// Maximum number of distance codes.
        /// </summary>
        const int MAXDCODES = 30;

        /// <summary>
        /// Maximum codes lengths to read.
        /// </summary>
        const int MAXCODES = MAXLCODES + MAXDCODES;

        /// <summary>
        /// Number of fixed literal/length codes (0-287).
        /// </summary>
        const int FIXLCODES = 288;

        public static void Reverse(BitArray array)
        {
            int length = array.Length;
            int mid = (length / 2);

            for (int i = 0; i < mid; i++)
            {
                bool bit = array[i];
                array[i] = array[length - i - 1];
                array[length - i - 1] = bit;
            }
        }

        public struct Huffman
        {
            /// <summary>
            /// count; [key (index)] = code length in bits; [value] = frequency;
            /// </summary>
            public short[] count;

            /// <summary>
            /// symbol; [key (index)] = symbol; [value] = code length;
            /// </summary>
            public short[] symbol;

            public Huffman(short[] count, short[] symbol)
            {
                this.count = count;
                this.symbol = symbol;
            }
        }

        private static int Decode(BitStream bitStream, Huffman h)
        {
            int code = 0;
            int first = 0;
            int index = 0;

            for (int i = 1; i <= MAXBITS; i++)
            {
                code |= bitStream.GetNextBit(); // get next bit from the stream;
                int count = h.count[i];
                if (code - count < first)
                {
                    //Console.WriteLine($"decoded value = {h.symbol[index + (code - first)]}");
                    return h.symbol[index + (code - first)];
                }
                //Console.WriteLine("get another bit");
                index += count;
                first += count;
                first <<= 1;
                code <<= 1;
            }
            return -10; // ran out of codes.
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="h"> reference to huffman code decoding table </param>
        /// <param name="length"> an array that maps code lengths onto their symbols </param>
        /// <param name="n"> total number of codes </param>
        /// <returns></returns>
        private static int Construct(ref Huffman h, short[] length, int n)
        {
            for (int i = 0; i <= MAXBITS; i++)
            {
                h.count[i] = 0;
            }
            for (int i = 0; i < n; i++)
            {
                h.count[length[i]]++;
            }
            if (h.count[0] == n)
            {
                throw new Exception("no codes!");
            }

            /* check for an over-subscribed or incomplete set of lengths */
            /* number of possible codes left of current length */
            int left = 1; /* one possible code of zero length */

            for (int i = 1; i <= MAXBITS; i++)
            {
                left <<= 1; /* one more bit, double codes left */
                left -= h.count[i]; /* deduct count from possible codes */
                if (left < 0)
                {
                    throw new Exception("oversubscribed");
                }
            }

            short[] offsets = new short[MAXBITS + 1];

            offsets[1] = 0;

            for (int i = 1; i < MAXBITS; i++)
            {
                offsets[i + 1] = (short)(offsets[i] + h.count[i]);
            }

            for (short i = 0; i < n; i++)
            {
                if (length[i] != 0)
                {
                    h.symbol[offsets[length[i]]++] = i;
                }
            }

            return left;

        }

        public static int ParseFixed(BitStream bitStream, bool lastBlock)
        {
            // lengths; [key (index)] = symbol; [value] = code length;
            short[] lengths = new short[FIXLCODES];

            for (int i = 0; i < 144; i++)
            {
                lengths[i] = 8;
            }
            for (int i = 144; i < 256; i++)
            {
                lengths[i] = 9;
            }
            for (int i = 256; i < 280; i++)
            {
                lengths[i] = 7;
            }
            for (int i = 280; i < FIXLCODES; i++)
            {
                lengths[i] = 8;
            }

            var lencode = new Huffman(new short[MAXBITS + 1], new short[FIXLCODES]);
            Construct(ref lencode, lengths, FIXLCODES);

            short[] distances = new short[MAXDCODES];

            for (int i = 0; i < MAXDCODES; i++)
            {
                distances[i] = 5;
            }

            var distcode = new Huffman(new short[MAXBITS + 1], new short[MAXDCODES]);
            Construct(ref distcode, distances, MAXDCODES);

            return Codes(bitStream, lencode, distcode, lastBlock);

        }

        static readonly short[] order =      /* permutation of code length codes */
        {16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15};

        public static int ParseDynamic(BitStream bitStream, bool lastBlock)
        {
            short[] lengths = new short[MAXCODES];

            int nlen = bitStream.GetNextBitsReverse(5) + 257;
            int ndist = bitStream.GetNextBitsReverse(5) + 1;
            int ncode = bitStream.GetNextBitsReverse(4) + 4;

            if (nlen > MAXLCODES || ndist > MAXDCODES)
            {
                throw new Exception("bad counts");
            }

            for (int i = 0; i < ncode; i++)
            {
                lengths[order[i]] = (short)bitStream.GetNextBitsReverse(3);
            }
            for (int i = ncode; i < 19; i++)
            {
                lengths[order[i]] = 0;
            }

            //for (int i = 0; i < lengths.Length; i++)
            //{
            //    Console.WriteLine($"symbol {i} has a code length of {lengths[i]}");
            //}

            var lencode = new Huffman(new short[MAXBITS + 1], new short[MAXLCODES]);
            var distcode = new Huffman(new short[MAXBITS + 1], new short[MAXDCODES]);

            var err = Construct(ref lencode, lengths, 19);
            if (err != 0)
            {
                throw new Exception("complete code set required here; code lengths codes incomplete.");
            }

            //for (int i = 0; i < lencode.count.Length; i++)
            //{
            //    Console.WriteLine($"{lencode.count[i]} symbols of length {i}");
            //}

            var index = 0;
            while (index < nlen + ndist)
            {
                var symbol = Decode(bitStream, lencode);
                var len = 0;

                if (symbol < 0)
                {
                    throw new ArgumentOutOfRangeException("invalid symbol");
                }
                if (symbol < 16)
                {
                    lengths[index++] = (short)symbol;
                }
                else
                {
                    if (symbol == 16)
                    {
                        if (index == 0)
                        {
                            throw new ArgumentOutOfRangeException("no preceeding length to reference!");
                        }
                        len = lengths[index - 1];
                        symbol = 3 + bitStream.GetNextBitsReverse(2);
                    }
                    else if (symbol == 17)
                    {
                        symbol = 3 + bitStream.GetNextBitsReverse(3);
                    }
                    else
                    {
                        symbol = 11 + bitStream.GetNextBitsReverse(7);
                    }
                    if (index + symbol > nlen + ndist)
                    {
                        throw new ArgumentOutOfRangeException("too many lengths!");
                    }
                    while (symbol-- > 0)
                    {
                        lengths[index++] = (short)len;
                    }
                }
            }

            if (lengths[256] == 0)
            {
                throw new Exception("no end of block code");
            }

            err = Construct(ref lencode, lengths, nlen);
            if (err != 0 & (err < 0 || nlen != lencode.count[0] + lencode.count[1]))
            {
                throw new ArgumentException("incomplete code ok only for single length 1 code.");
            }

            var _lengths = new short[MAXDCODES];
            Array.Copy(lengths, nlen, _lengths, 0, MAXDCODES);

            err = Construct(ref distcode, _lengths, ndist);
            if (err != 0 & (err < 0 || nlen != distcode.count[0] + distcode.count[1]))
            {
                throw new ArgumentException("incomplete code ok only for single length 1 code.");
            }

            return Codes(bitStream, lencode, distcode, lastBlock);

        }


        /// <summary>
        /// Size base for length codes 257-285.
        /// </summary>
        static readonly short[] lens = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258 };

        /// <summary>
        /// Extra bits for length codes 257-285.
        /// </summary>
        static readonly short[] lext = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };

        /// <summary>
        /// Offset base for distance codes 0-29.
        /// </summary>
        static readonly short[] dists = { 1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577 };

        /// <summary>
        /// Extra bits for distance codes 0-29.
        /// </summary>
        static readonly short[] dext = { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };

        private static int Codes(BitStream bitStream, Huffman lencode, Huffman distcode, bool lastBlock)
        {
            int symbol;

            do
            {
                symbol = Decode(bitStream, lencode);
                if (symbol < 0)
                {
                    throw new ArgumentOutOfRangeException("invalid symbol.");
                }
                if (symbol < 256)
                {
                    bitStream.OutBuffer.Add((byte)symbol);
                    //bitStream.OutBufferWithHeader.Add((byte)symbol);
                    //if (bitStream.OutBuffer.Count() % 65535 == 0)
                    //{
                    //    Console.WriteLine("blocked");
                    //    var blocks = bitStream.OutBuffer.Count() / 65535;
                    //    bitStream.OutBufferWithHeader.InsertRange((65535 + 5) * (blocks - 1), BlockHeader(65535, false));
                    //}
                }
                else if (symbol > 256)
                {
                    symbol -= 257;
                    if (symbol >= 29)
                    {
                        throw new Exception("Invalid fixed code.");
                    }
                    var len = lens[symbol] + bitStream.GetNextBitsReverse(lext[symbol]);

                    symbol = Decode(bitStream, distcode);
                    if (symbol < 0)
                    {
                        throw new ArgumentOutOfRangeException("invalid symbol.");
                    }
                    var dist = dists[symbol] + bitStream.GetNextBitsReverse(dext[symbol]);

                    // copy length bytes from distance bytes back.
                    for (int i = 0; i < len; i++)
                    {
                        bitStream.OutBuffer.Add(bitStream.OutBuffer[bitStream.OutBuffer.Count() - dist]);
                        //bitStream.OutBufferWithHeader.Add(bitStream.OutBuffer[bitStream.OutBuffer.Count() - dist]);
                        //if (bitStream.OutBuffer.Count() % 65535 == 0)
                        //{
                        //    Console.WriteLine("blocked");
                        //    var blocks = bitStream.OutBuffer.Count() / 65535;
                        //    bitStream.OutBufferWithHeader.InsertRange((65535 + 5) * (blocks - 1), BlockHeader(65535, false));
                        //}
                    }
                }


            } while (symbol != 256);

            //if (lastBlock)
            //{
            //    var blocks = bitStream.OutBuffer.Count() / 65535;
            //    bitStream.OutBufferWithHeader.InsertRange((65535 + 5) * (blocks - 1), BlockHeader((ushort)(bitStream.OutBuffer.Count() % 65535), true));
            //}

            return 0;

        }

        static List<byte> BlockHeader(ushort dataLength, bool last)
        {
            byte[] blockHeader = new byte[5];

            blockHeader[0] = (byte)(last ? 1 : 0);

            byte[] len = BitConverter.GetBytes(dataLength);
            Array.Copy(len, 0, blockHeader, 1, sizeof(short));

            byte[] nen = BitConverter.GetBytes(~dataLength);
            Array.Copy(nen, 0, blockHeader, 3, sizeof(short));

            return blockHeader.ToList();
        }
    }
}
