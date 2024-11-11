using System;
using System.Collections.Generic;
using System.IO;
using GameArchives.Common;

namespace GameArchives.DoubleFine
{
  public class PKGFile : IFile
  {
    public long Size { get; }
    public long CompressedSize => Size;
    public bool Compressed => false;

    public IDictionary<string, object> ExtendedInfo { get; }
    public Stream Stream => GetStream();
    public string Name { get; }
    public IDirectory Parent { get; }

    private long offset;
    private Stream archive;

    public PKGFile(string name, IDirectory parent, long size, long offset, Stream archive)
    {
      Name = name;
      Parent = parent;
      Size = size;

      this.offset = offset;
      this.archive = archive;

      ExtendedInfo = new Dictionary<string, object>();
    }

    public byte[] GetBytes()
    {
      byte[] bytes = new byte[CompressedSize];
      if (CompressedSize > Int32.MaxValue)
        throw new NotSupportedException("Can't read bytes for file larger than int32 max, yet.");

      using (Stream stream = this.GetStream())
      {
        stream.Read(bytes, 0, (int)CompressedSize);
      }

      return bytes;
    }

    public Stream GetStream() => new OffsetStream(archive, offset, CompressedSize);
  }
}
