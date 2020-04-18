using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PNGUIN
{
    /// The original source of this class can be found at https://github.com/murrple-1/APNGManagement/tree/master/APNGLib.

    /// <summary>
    /// Represents a PNG file.
    /// </summary>
    public class PNG
    {
        public IHDRChunk IHDR { get; set; }
        public IList<IDATChunk> IDATList { get; private set; }
        public IENDChunk IEND { get; set; }

        public PLTEChunk PLTE { get; set; }
        public tRNSChunk tRNS { get; set; }
        public cHRMChunk cHRM { get; set; }
        public gAMAChunk gAMA { get; set; }
        public iCCPChunk iCCP { get; set; }
        public sBITChunk sBIT { get; set; }
        public sRGBChunk sRGB { get; set; }
        public ICollection<tEXtChunk> tEXtList { get; private set; }
        public ICollection<zTXtChunk> zTXtList { get; private set; }
        public ICollection<iTXtChunk> iTXtList { get; private set; }
        public bKGDChunk bKGD { get; set; }
        public hISTChunk hIST { get; set; }
        public pHYsChunk pHYs { get; set; }
        public sPLTChunk sPLT { get; set; }
        public tIMEChunk tIME { get; set; }

        protected ICollection<PNGChunk> chunks;

        public PNG()
        {
            IDATList = new List<IDATChunk>();
            tEXtList = new HashSet<tEXtChunk>();
            zTXtList = new HashSet<zTXtChunk>();
            iTXtList = new HashSet<iTXtChunk>();

            chunks = new List<PNGChunk>();
        }

        public uint Width { get { return IHDR.Width; } }
        public uint Height { get { return IHDR.Height; } }

        #region Writing

        public virtual Stream WriteToStream()
        {
            Validate();

            IList<byte> imageData = new List<byte>();
            foreach (IDATChunk idat in IDATList)
            {
                foreach (byte b in idat.ImageData)
                {
                    imageData.Add(b);
                }
            }

            Stream stream = new MemoryStream(8192);
            WritePNGData(stream, imageData);
            return stream;
        }

        protected void WritePNGData(Stream stream, IList<byte> imageData)
        {
            WritePNGData(stream, imageData, IHDR.Width, IHDR.Height);
        }

        protected void WritePNGData(Stream stream, IList<byte> imageData, uint width, uint height)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            Console.WriteLine("\nWriting to file...");
            WriteSignature(stream);
            IHDRChunk IHDR = new IHDRChunk();
            IHDR.ChunkData = this.IHDR.ChunkData;
            IHDR.Width = width;
            IHDR.Height = height;
            WriteChunk(stream, IHDR);
            WriteAncillaryChunks(stream);
            foreach (var IDAT in IDATList)
            {
                WriteChunk(stream, IDAT);
            }            
            WriteChunk(stream, IEND);
            stopWatch.Stop();
            string elapsedTime = stopWatch.Elapsed.FormatTime();
            Console.WriteLine($"Total write time = {elapsedTime}");
        }


        protected void WriteSignature(Stream stream)
        {
            stream.Write(PNGSignature.Signature, 0, PNGSignature.Signature.Length);
        }

        protected void WriteAncillaryChunks(Stream stream)
        {
            WriteChunk(stream, PLTE);
            WriteChunk(stream, tRNS);
            WriteChunk(stream, cHRM);
            WriteChunk(stream, gAMA);
            WriteChunk(stream, iCCP);
            WriteChunk(stream, sBIT);
            WriteChunk(stream, sRGB);
            WriteChunk(stream, bKGD);
            WriteChunk(stream, hIST);
            WriteChunk(stream, pHYs);
            WriteChunk(stream, sPLT);
            WriteChunk(stream, tIME);
            foreach (tEXtChunk text in tEXtList)
            {
                WriteChunk(stream, text);
            }
            foreach (zTXtChunk ztxt in zTXtList)
            {
                WriteChunk(stream, ztxt);
            }
            foreach (iTXtChunk itxt in iTXtList)
            {
                WriteChunk(stream, itxt);
            }
            foreach (PNGChunk chunk in chunks)
            {
                WriteChunk(stream, chunk);
            }
        }

        protected static void WriteChunk(Stream stream, PNGChunk chunk)
        {
            if (chunk != null)
            {
                Console.WriteLine($"Writing {chunk.ChunkType} chunk... ({chunk.ChunkLength} bytes)");
                byte[] chArray = chunk.Chunk;
                stream.Write(chArray, 0, chArray.Length);
            }
        }

        #endregion

        #region Reading

        public void Load(Stream stream)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            Console.WriteLine("\nReading file...");
            byte[] sig = new byte[PNGSignature.Signature.Length];
            stream.Read(sig, 0, PNGSignature.Signature.Length);
            PNGSignature.Compare(sig);

            PNGChunk chunk = GetNextChunk(stream);
            Console.WriteLine($"Reading {chunk.ChunkType} chunk... ({chunk.ChunkLength} bytes)");
            if (chunk.ChunkType != IHDRChunk.NAME)
            {
                throw new ApplicationException("First chunk is not IHDR chunk");
            }
            Handle_IHDR(chunk);

            do
            {
                chunk = GetNextChunk(stream);
                Console.WriteLine($"Reading {chunk.ChunkType} chunk... ({chunk.ChunkLength} bytes)");
                if (!HandleChunk(chunk))
                {
                    HandleDefaultChunk(chunk);
                }
            } while (chunk.ChunkType != IENDChunk.NAME);
            Validate();
            stopWatch.Stop();
            string elapsedTime = stopWatch.Elapsed.FormatTime();
            Console.WriteLine($"Total read time = {elapsedTime}");
        }

        protected PNGChunk GetNextChunk(Stream stream)
        {
            PNGChunk value = new PNGChunk();

            byte[] size = new byte[sizeof(uint)];
            stream.Read(size, 0, sizeof(uint));
            uint readLength = PNGUtils.ParseUint(size);

            byte[] type = new byte[4];
            stream.Read(type, 0, 4);
            value.ChunkType = PNGUtils.ParseString(type, 4);

            byte[] data = new byte[readLength];
            stream.Read(data, 0, (int)readLength);
            value.ChunkData = data;

            byte[] crc = new byte[sizeof(uint)];
            stream.Read(crc, 0, sizeof(uint));
            uint readCRC = PNGUtils.ParseUint(crc);

            uint calcCRC = value.CalculateCRC();
            if (readCRC != calcCRC)
            {
                throw new ApplicationException($"PNG Chunk CRC Mismatch.  Chunk CRC = {readCRC}, Calculated CRC = {calcCRC}.");
            }
            return value;
        }

        protected virtual bool HandleChunk(PNGChunk chunk)
        {
            switch (chunk.ChunkType)
            {
                case IHDRChunk.NAME:
                    Handle_IHDR(chunk);
                    break;
                case PLTEChunk.NAME:
                    Handle_PLTE(chunk);
                    break;
                case IDATChunk.NAME:
                    Handle_IDAT(chunk);
                    break;
                case IENDChunk.NAME:
                    Handle_IEND(chunk);
                    break;
                case tRNSChunk.NAME:
                    Handle_tRNS(chunk);
                    break;
                case cHRMChunk.NAME:
                    Handle_cHRM(chunk);
                    break;
                case gAMAChunk.NAME:
                    Handle_gAMA(chunk);
                    break;
                case iCCPChunk.NAME:
                    Handle_iCCP(chunk);
                    break;
                case sBITChunk.NAME:
                    Handle_sBIT(chunk);
                    break;
                case sRGBChunk.NAME:
                    Handle_sRGB(chunk);
                    break;
                case tEXtChunk.NAME:
                    Handle_tEXt(chunk);
                    break;
                case zTXtChunk.NAME:
                    Handle_zTXt(chunk);
                    break;
                case iTXtChunk.NAME:
                    Handle_iTXt(chunk);
                    break;
                case bKGDChunk.NAME:
                    Handle_bKGD(chunk);
                    break;
                case hISTChunk.NAME:
                    Handle_hIST(chunk);
                    break;
                case pHYsChunk.NAME:
                    Handle_pHYs(chunk);
                    break;
                case sPLTChunk.NAME:
                    Handle_sPLT(chunk);
                    break;
                case tIMEChunk.NAME:
                    Handle_tIME(chunk);
                    break;
                default:
                    return false;
            }
            return true;
        }

        private void Handle_tIME(PNGChunk chunk)
        {
            if (tIME != null)
            {
                throw new ApplicationException("tIME chunk encountered more than once");
            }
            tIME = new tIMEChunk();
            tIME.ChunkData = chunk.ChunkData;
        }

        private void Handle_sPLT(PNGChunk chunk)
        {
            if (sPLT != null)
            {
                throw new ApplicationException("sPLT chunk encountered more than once");
            }
            sPLT = new sPLTChunk();
            sPLT.ChunkData = chunk.ChunkData;
        }

        private void Handle_pHYs(PNGChunk chunk)
        {
            if (pHYs != null)
            {
                throw new ApplicationException("pHYs chunk encountered more than once");
            }
            pHYs = new pHYsChunk();
            pHYs.ChunkData = chunk.ChunkData;
        }

        private void Handle_hIST(PNGChunk chunk)
        {
            if (hIST != null)
            {
                throw new ApplicationException("hIST chunk encountered more than once");
            }
            hIST = new hISTChunk();
            hIST.ChunkData = chunk.ChunkData;
        }

        private void Handle_bKGD(PNGChunk chunk)
        {
            if (bKGD != null)
            {
                throw new ApplicationException("bKGD chunk encountered more than once");
            }
            switch (IHDR.ColorType)
            {
                case 0:
                    bKGD = new bKGDChunkType0();
                    break;
                case 2:
                    bKGD = new bKGDChunkType2();
                    break;
                case 3:
                    bKGD = new bKGDChunkType3();
                    break;
                case 4:
                    bKGD = new bKGDChunkType4();
                    break;
                case 6:
                    bKGD = new bKGDChunkType6();
                    break;
                default:
                    throw new ApplicationException("Colour type is not supported");
            }
            bKGD.ChunkData = chunk.ChunkData;
        }

        private void Handle_iTXt(PNGChunk chunk)
        {
            iTXtChunk it = new iTXtChunk();
            it.ChunkData = chunk.ChunkData;
            iTXtList.Add(it);
        }

        private void Handle_zTXt(PNGChunk chunk)
        {
            zTXtChunk zt = new zTXtChunk();
            zt.ChunkData = chunk.ChunkData;
            zTXtList.Add(zt);
        }

        private void Handle_tEXt(PNGChunk chunk)
        {
            tEXtChunk txt = new tEXtChunk();
            txt.ChunkData = chunk.ChunkData;
            tEXtList.Add(txt);
        }

        private void Handle_sRGB(PNGChunk chunk)
        {
            if (sRGB != null)
            {
                throw new ApplicationException("sRGB chunk encountered more than once");
            }
            sRGB = new sRGBChunk();
            sRGB.ChunkData = chunk.ChunkData;
        }

        private void Handle_sBIT(PNGChunk chunk)
        {
            if (sBIT != null)
            {
                throw new ApplicationException("sBIT chunk encountered more than once");
            }
            switch (IHDR.ColorType)
            {
                case 0:
                    sBIT = new sBITChunkType0();
                    break;
                case 2:
                    sBIT = new sBITChunkType2();
                    break;
                case 3:
                    sBIT = new sBITChunkType3();
                    break;
                case 4:
                    sBIT = new sBITChunkType4();
                    break;
                case 6:
                    sBIT = new sBITChunkType6();
                    break;
                default:
                    throw new ApplicationException("Colour type is not supported");
            }
            sBIT.ChunkData = chunk.ChunkData;
        }

        private void Handle_iCCP(PNGChunk chunk)
        {
            if (iCCP != null)
            {
                throw new ApplicationException("iCCP chunk encountered more than once");
            }
            iCCP = new iCCPChunk();
            iCCP.ChunkData = chunk.ChunkData;
        }

        private void Handle_gAMA(PNGChunk chunk)
        {
            if (gAMA != null)
            {
                throw new ApplicationException("gAMA chunk encountered more than once");
            }
            gAMA = new gAMAChunk();
            gAMA.ChunkData = chunk.ChunkData;
        }

        private void Handle_cHRM(PNGChunk chunk)
        {
            if (cHRM != null)
            {
                throw new ApplicationException("cHRM chunk encountered more than once");
            }
            cHRM = new cHRMChunk();
            cHRM.ChunkData = chunk.ChunkData;
        }

        private void Handle_tRNS(PNGChunk chunk)
        {
            if (tRNS != null)
            {
                throw new ApplicationException("tRNS chunk encountered more than once");
            }
            switch (IHDR.ColorType)
            {
                case 0:
                    tRNS = new tRNSChunkType0();
                    break;
                case 2:
                    tRNS = new tRNSChunkType2();
                    break;
                case 3:
                    tRNS = new tRNSChunkType3();
                    break;
                case 4:
                case 6:
                    throw new ApplicationException("tRNS chunk encountered, Colour type does not support");
                default:
                    throw new ApplicationException("Colour type is not supported");
            }
            tRNS.ChunkData = chunk.ChunkData;
        }

        private void Handle_PLTE(PNGChunk chunk)
        {
            if (PLTE != null)
            {
                throw new ApplicationException("PLTE chunk encountered more than once");
            }
            PLTE = new PLTEChunk();
            PLTE.ChunkData = chunk.ChunkData;
        }

        private void Handle_IHDR(PNGChunk chunk)
        {
            if (IHDR != null)
            {
                throw new ApplicationException("IHDR defined more than once");
            }
            IHDR = new IHDRChunk();
            IHDR.ChunkData = chunk.ChunkData;
        }

        private void Handle_IDAT(PNGChunk chunk)
        {
            IDATChunk idatC = new IDATChunk();
            idatC.ChunkData = chunk.ChunkData;
            IDATList.Add(idatC);
        }

        private void Handle_IEND(PNGChunk chunk)
        {
            if (IEND != null)
            {
                throw new ApplicationException("IEND defined more than once");
            }
            IEND = new IENDChunk();
            IEND.ChunkData = chunk.ChunkData;
        }

        private void HandleDefaultChunk(PNGChunk chunk)
        {
            chunks.Add(chunk);
        }

        public virtual void Validate()
        {
            if (IHDR == null || IDATList.Count < 1 || IEND == null)
            {
                throw new ApplicationException("Required chunk(s) missing");
            }
            if (hIST != null && PLTE == null)
            {
                throw new ApplicationException("Cannot have a hIST chunk without a PLTE chunk");
            }
            if (hIST != null && hIST.Frequency.Length != PLTE.PaletteEntries.Count)
            {
                throw new ApplicationException("Number of hIST chunk entries different from number of PLTE chunk entries");
            }
        }

        #endregion
    }
}

