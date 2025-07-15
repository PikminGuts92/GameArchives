using System;
using System.IO;
using System.IO.Compression;

namespace GameArchives.Seven45
{
  public class CmpStream : Stream
  {
    private readonly MemoryStream _stream;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length { get; }

    public override long Position { get => _stream.Position; set => Seek(value, SeekOrigin.Begin); }

    public CmpStream(Stream stream)
    {
      stream.Seek(4, SeekOrigin.Begin); /// CMP1 magic
      var uncompressedSize = stream.ReadUInt32LE();

      var blockSize = stream.ReadUInt32LE();
      var blockCount = (int)Math.Ceiling((double)uncompressedSize / (double)blockSize);

      var compressedBlockSizes = new int[blockCount];
      for (var i = 0; i < blockCount; i++)
      {
        compressedBlockSizes[i] = stream.ReadUInt16LE();
      }

      var uncompressedStream = new MemoryStream(new byte[uncompressedSize]);
      for (var i = 0; i < blockCount; i++)
      {
        var compressedBlockSize = compressedBlockSizes[i];
        if (compressedBlockSize == 0)
        {
          // Block is uncompressed. Read rest of file (should always be last block)
          stream.CopyTo(uncompressedStream);
          break;
        }

        var compressedBytes = stream.ReadBytes(compressedBlockSize);
        using var gzipStream = new GZipStream(new MemoryStream(compressedBytes), CompressionMode.Decompress);
        gzipStream.CopyTo(uncompressedStream);
      }
      uncompressedStream.Seek(0, SeekOrigin.Begin);

      _stream = uncompressedStream;
      Length = uncompressedSize;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      return _stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      return _stream.Seek(offset, origin);
    }

    #region Not Supported
    public override void Flush()
    {
      throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
      throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      throw new NotImplementedException();
    }
    #endregion
  }
}
