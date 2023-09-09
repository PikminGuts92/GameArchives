using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using GameArchives.Common;

namespace GameArchives.Konami
{
  public class KonamiFile : IFile
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

    public KonamiFile(string name, IDirectory parent, long size, long offset, int unknown, Stream archive)
    {
      Name = name;
      Parent = parent;
      Size = size;

      this.offset = offset;
      this.archive = archive;

      ExtendedInfo = new Dictionary<string, object>()
      {
        { "unknown", unknown }
      };
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
