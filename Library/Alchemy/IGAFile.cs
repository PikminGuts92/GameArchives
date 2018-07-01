using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using GameArchives.Common;

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
    private uint chunkSize; // Usually 2048

    public IGAFile(string name, string tempPath, IDirectory parent, long size, bool compressed, ushort compressionMode, long offset, Stream stream, uint chunkSize)
    {
      Name = name;
      Parent = parent;
      Size = size;
      Compressed = compressed;
      CompressedSize = compressed ? GetCompressedSize(stream, offset) : 0;

      this.offset = offset;
      this.stream = stream;
      this.compressionMode = compressionMode;
      this.chunkSize = chunkSize;

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
      uint currentSize = 0;
      stream.Seek(offset, SeekOrigin.Begin);
      var ms = new MemoryStream();

      while (currentSize < Size)
      {
        var deflateSize = stream.ReadUInt16LE();

        if (deflateSize < Size)
        {
          if ((compressionMode & 0x2000) != 0) deflateSize += 5; // Skips unneeded data
          
          var data = InflateBlock(stream.ReadBytes(deflateSize));
          ms.Write(data, 0, data.Length);
        }
        else
        {

        }

        // Rounds up to chunk size
        if ((stream.Position % chunkSize) > 0)
          stream.Position += chunkSize - (stream.Position % chunkSize);

      }

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
