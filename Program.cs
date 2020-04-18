using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Collections;
using PNGUIN.Checksum;
using System.Threading;
using System.Diagnostics;

namespace PNGUIN
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = string.Empty;
            if (args.Length > 0)
            {
                path = args[0];
                Console.WriteLine(path);
            }
            if (path != string.Empty)
            {
                //Decode(path);
                var fileName = $"{Path.GetFileNameWithoutExtension(path)}_pnguin.png";
                try
                {
                    Stream dataStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var png = new PNG();
                    png.Load(dataStream);
                    dataStream.Dispose();

                    Decode(ref png);

                    Stream stream = png.WriteToStream();
                    WriteToFile(stream, png, fileName);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            Console.ReadLine();
        }

        static void InsertBlockHeader(List<byte> byteList, ushort dataLength, bool last)
        {
            byte[] blockHeader = new byte[5];

            blockHeader[0] = (byte)(last ? 1 : 0);

            byte[] len = BitConverter.GetBytes(dataLength);
            Array.Copy(len, 0, blockHeader, 1, sizeof(short));

            byte[] nen = BitConverter.GetBytes(~dataLength);
            Array.Copy(nen, 0, blockHeader, 3, sizeof(short));
            foreach (var b in blockHeader)
            {
                byteList.Add(b);
            }
        }

        static void Decode(ref PNG png)
        {
            Console.WriteLine($"\nDecoding zlib blocks...");
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            uint idatLength = 0;
            foreach (var chunk in png.IDATList)
            {
                idatLength += chunk.ChunkLength;
            }

            byte[] idatData = new byte[idatLength];

            var offset = 0;
            for (int i = 0; i < png.IDATList.Count(); i++)
            {
                Array.Copy(png.IDATList[i].ChunkData, 0, idatData, offset, (int)png.IDATList[i].ChunkLength);
                offset += (int)png.IDATList[i].ChunkLength;
            }

            Stream idatStream = new MemoryStream(idatData);
            var zlibHeader = new byte[2];
            idatStream.Read(zlibHeader, 0, 2);

            var bitBuffer = new byte[idatLength - 6]; // subtract 2 byte zlib header and 4 byte adler checksum.
            idatStream.Read(bitBuffer, 0, (int)(idatLength - 6));
            idatStream.Dispose();
            BitArray bitArray = new BitArray(bitBuffer);
            BitStream bs = new BitStream(bitArray);

            bool lastBlock;
            byte compression;

            do
            {
                lastBlock = bs.GetNextBitsReverse(1) == 1;
                compression = (byte)bs.GetNextBitsReverse(2);

                if (compression == 0)
                {
                    Console.WriteLine($"Zlib block already uncompressed...");
                    bs.GetNextBitsReverse(5);
                    ushort len = bs.GetNextShortReverse();
                    ushort nen = (ushort)~bs.GetNextShortReverse();
                    if (nen != len)
                    {
                        throw new Exception("Nen does not complement Len.");
                    }
                    for (int i = 0; i < len; i++)
                    {
                        bs.GetNextBitsReverse(8);
                    }
                }
                else if (compression == 1)
                {
                    Console.WriteLine($"Uncompressing {(lastBlock ? "last " : "")}zlib block...");
                    Puff.ParseFixed(bs, lastBlock);
                }
                else if (compression == 2)
                {
                    Console.WriteLine($"Uncompressing {(lastBlock ? "last " : "")}zlib block...");
                    Puff.ParseDynamic(bs, lastBlock);
                }
            }
            while (lastBlock == false);

            if (compression != 0)
            {
                var adler = new Adler32();
                adler.Update(bs.OutBuffer.ToArray());
                byte[] adlerbytearray = BitConverter.GetBytes((int)adler.Value);
                Array.Reverse(adlerbytearray, 0, adlerbytearray.Length);

                Console.WriteLine($"Adler32 checksum = {adlerbytearray[0]} {adlerbytearray[1]} {adlerbytearray[2]} {adlerbytearray[3]}");

                var numblocks = (bs.OutBuffer.Count()) / 65500;
                var numblocksRem = (bs.OutBuffer.Count()) % 65500;

                var splitBlocklist = new List<byte>();

                if (numblocks > 0)
                {
                    for (int j = 0; j < numblocks; j++)
                    {
                        InsertBlockHeader(splitBlocklist, 65500, false);
                        for (int i = 0; i < 65500; i++)
                        {
                            splitBlocklist.Add(bs.OutBuffer[i + j * 65500]);
                        }
                    }
                    InsertBlockHeader(splitBlocklist, (ushort)numblocksRem, true);
                    for (int i = (int)(bs.OutBuffer.Count() - numblocksRem); i < (bs.OutBuffer.Count()); i++)
                    {
                        splitBlocklist.Add(bs.OutBuffer[i]);
                    }
                }
                else
                {
                    InsertBlockHeader(splitBlocklist, (ushort)numblocksRem, true);
                    for (int i = (int)(bs.OutBuffer.Count() - numblocksRem); i < (bs.OutBuffer.Count()); i++)
                    {
                        splitBlocklist.Add(bs.OutBuffer[i]);
                    }
                }

                for (int i = 0; i < adlerbytearray.Length; i++)
                {
                    splitBlocklist.Add(adlerbytearray[i]);
                }

                byte[] newChunkData = new byte[splitBlocklist.Count() + 2];
                zlibHeader.CopyTo(newChunkData, 0);
                splitBlocklist.ToArray().CopyTo(newChunkData, 2);

                var ncdChunks = newChunkData.Length / 65500;
                var ncdRem = newChunkData.Length % 65500;

                png.IDATList.Clear();

                for (int i = 0; i < ncdChunks; i++)
                {
                    IDATChunk newIdatChunk = new IDATChunk();
                    newIdatChunk.ChunkData = newChunkData.SubArray(i * 65500, 65500);
                    png.IDATList.Add(newIdatChunk);
                }

                IDATChunk finalIdatChunk = new IDATChunk();
                finalIdatChunk.ChunkData = newChunkData.SubArray(newChunkData.Length - ncdRem, ncdRem);
                png.IDATList.Add(finalIdatChunk);

            }

            stopWatch.Stop();
            string elapsedTime = stopWatch.Elapsed.FormatTime();
            Console.WriteLine($"Total decode time = {elapsedTime}");
        }

        static void WriteToFile(Stream source, PNG png, string fileName)
        {
            using (var stream = File.Create(Directory.GetCurrentDirectory() + "\\" + fileName, 8192, FileOptions.Asynchronous))
            {
                source.Seek(0, SeekOrigin.Begin);
                source.CopyTo(stream);
                Console.WriteLine("\nFinished.");
            }
        }

    }
}
