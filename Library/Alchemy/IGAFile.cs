using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using GameArchives.Common;
using SevenZip.Compression.LZMA;

namespace GameArchives.Alchemy
{
  public class IGAFile : IFile
  {
    public long Size { get; }
    public bool Compressed { get; }
    public long CompressedSize { get; }

    public IDictionary<string, object> ExtendedInfo { get; }
    public Stream Stream => GetStream();
    public string Name { get; }
    public IDirectory Parent { get; }

    private long offset;
    private Stream stream;
    private ushort compressionMode;
    private uint padSize; // Usually 2048

    public IGAFile(string name, string tempPath, IDirectory parent, long size, bool compressed, ushort compressionMode, long offset, Stream stream, uint padSize)
    {
      Name = name;
      Parent = parent;
      Size = size;
      Compressed = compressed;
      CompressedSize = compressed ? GetCompressedSize(stream, offset) : 0;

      this.offset = offset;
      this.stream = stream;
      this.compressionMode = compressionMode;
      this.padSize = padSize;

      ExtendedInfo = new Dictionary<string, object>();
      ExtendedInfo.Add("TempPath", tempPath);
    }

    private static long GetCompressedSize(Stream stream, long offset)
    {
      var origOffset = stream.Position;
      stream.Seek(offset, SeekOrigin.Begin);

      var size = stream.ReadUInt16LE();
      stream.Seek(origOffset, SeekOrigin.Begin);
      return size;
    }

    public byte[] GetBytes()
    {
      using (var s = GetStream())
      {
        var data = new byte[s.Length];
        s.Read(data, 0, data.Length);
        return data;
      }
    }

    public Stream GetStream()
    {
      if (Compressed)
      {
        return CreateInflatedStream();
      }
      else
        return new OffsetStream(stream, offset, Size);
    }

    private Stream CreateInflatedStream()
    {
      long remainingSize = Size;
      stream.Seek(offset, SeekOrigin.Begin);
      var ms = new MemoryStream();

      var lzmaDecoder = new SevenZip.Compression.LZMA.Decoder();
      const int BUFFER_SIZE = 0x8000;

      int i = 0;
      var start = 0L;
      List<uint> sizes = new List<uint>();
      while (remainingSize > 0)
      {
        start = stream.Position;
        var chunkSize = stream.ReadUInt16LE();
        var chunkSizeUnc = (remainingSize < BUFFER_SIZE) ? remainingSize : BUFFER_SIZE;

        if (chunkSize < BUFFER_SIZE)
        {
          stream.Position += 1;
          sizes.Add(stream.ReadUInt32LE());
          stream.Position -= 5;

          if ((compressionMode & 0x2000) != 0)
            lzmaDecoder.SetDecoderProperties(stream.ReadBytes(5)); // Properties + dictionary size

          lzmaDecoder.Code(stream, ms, chunkSize, chunkSizeUnc, null); // Doesn't do anything with progress anyway :(


          //var data = InflateBlock(stream.ReadBytes(deflateSize));
          //ms.Write(data, 0, data.Length);
        }
        else
        {

        }

        // Rounds up to chunk size
        if ((stream.Position % this.padSize) > 0)
          stream.Position += this.padSize - (stream.Position % this.padSize);

        remainingSize -= chunkSizeUnc;
        i++;
        /*
        currentSize += BUFFER_SIZE;
        if (currentSize > Size)
          currentSize = ms.Length;
        */
      }
      ms.Seek(0, SeekOrigin.Begin);
      return ms;
    }

    private byte[] InflateBlock(byte[] data)
    {
      throw new NotImplementedException();
      /*
      using (var msInflate = new MemoryStream())
      {
        using (var ms = new MemoryStream(data))
        {
          var zlib = new DeflateStream(ms, CompressionMode.Decompress);

          zlib.CopyTo(ms);
        }

        return msInflate.ToArray();
      }*/
    }
  }
}
