using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GameArchives.Alchemy
{
  public class IGAPackage : AbstractPackage
  {
    private const uint IGA_MAGIC = 0x4947411A;
    private Stream stream;
    private IGADirectory root;

    public override string FileName { get; }
    public override IDirectory RootDirectory => root;
    public override long Size => stream.Length;
    public override bool Writeable => false;
    public override Type FileType => typeof(IGAPackage);

    public IGAPackage(IFile file)
    {
      FileName = file.Name;
      root = new IGADirectory(null, ROOT_DIR);
      stream = file.GetStream();

      var magic = stream.ReadUInt32BE();
      if (magic != IGA_MAGIC)
        throw new InvalidDataException("File does not have a valid Alchemy PAK header");

      var version = stream.ReadUInt32LE();
      stream.Position += 4; // Not sure
      
      //if (version != 11) throw new InvalidDataException("Unknown Alchemy PAK compression");

      var fileCount = stream.ReadInt32LE();
      var padSize = stream.ReadUInt32LE();
      stream.Position += 4;
      var fileNamesOffset = stream.ReadInt32LE();

      if (version < 11 && fileNamesOffset < padSize)
        fileNamesOffset = stream.ReadInt32LE();

      stream.Position += 12; // Unknown data + 0 + strings count

      if (version >= 9)
      {
        fileNamesOffset = stream.ReadInt32LE();
        stream.Position += 12; // 0 + file names chunk size + 1
      }
      else
        stream.Position += 8;
      
      stream.Position += fileCount * 4; // File hashes
      var fileInfoOffset = stream.Position; // Save for later

      // Reads compact file sizes
      stream.Seek(fileCount * 16, SeekOrigin.Current);
      var compactSizes = stream.ReadBytes(FindMagic(stream, 0xFFFFFFFF)); // Used for compressed chunks
      
      // Reads file names
      stream.Seek(fileNamesOffset, SeekOrigin.Begin);

      var nameOffsets = Enumerable.Range(0, fileCount).Select(x => stream.ReadInt32LE() - (fileCount * 4)).ToArray();
      var tableOffset = stream.Position;
      var fileNames = new string[fileCount][];

      for (int i = 0; i < fileCount; i++)
      {
        stream.Seek(tableOffset + (long)nameOffsets[i], SeekOrigin.Begin);
        fileNames[i] = new string[]
        {
          stream.ReadASCIINullTerminated(), // Temp path
          stream.ReadASCIINullTerminated()  // Final path
        };
      }
      
      // Reads file info
      stream.Seek(fileInfoOffset, SeekOrigin.Begin);
      
      for (int i = 0; i < fileCount; i++)
      {
        var fileOffset = stream.ReadUInt32LE();
        if (fileOffset >= 0xFF000000) fileOffset = padSize;

        var flags = stream.ReadUInt32LE();
        var uncompressedSize = stream.ReadUInt32LE();
        var inflateSizeOffset = stream.ReadUInt16LE();
        var compressionMode = stream.ReadUInt16LE(); // Usually 0x20000000

        var dir = MakeOrGetDirectory(fileNames[i][1]);
        var fileName = fileNames[i][1].Split('/').Last();

        dir.AddFile(new IGAFile(fileName, fileNames[i][0], dir, uncompressedSize, inflateSizeOffset != 0xFFFF, compressionMode, fileOffset, stream, padSize));
      }
    }

    private IGADirectory MakeOrGetDirectory(string path)
    {
      string[] breadcrumbs = path.Split('/');
      IDirectory last = root;
      IDirectory current;

      if (breadcrumbs.Length == 1)
        return root;

      for (var idx = 0; idx < breadcrumbs.Length - 1; idx++)
      {
        if (!last.TryGetDirectory(breadcrumbs[idx], out current))
        {
          current = new IGADirectory(last, breadcrumbs[idx]);
          (last as IGADirectory).AddDir(current as IGADirectory);
        }
        last = current;
      }

      return last as IGADirectory;
    }

    private static long ReadSizeFromCompact(byte[] data, uint i)
    {
      if (i < 0 || i >= data.Length) return 0;

      long size = 0;
      int shift = 0;

      do
      {
        size += (data[i] & 0x7F) << (shift * 8);

        shift++;
        i++;
      } while (i < data.Length && (data[i-1] & 0x80) != 0);

      return size;
    }

    private static int FindMagic(Stream stream, uint magic)
    {
      long start = stream.Position, currentPosition = stream.Position;
      uint currentMagic = 0;

      while (magic != currentMagic)
      {
        if (stream.Position == stream.Length)
        {
          // Couldn't find it
          stream.Seek(start, SeekOrigin.Begin);
          return -1;
        }

        currentMagic = (currentMagic << 8) | (uint)stream.ReadByte();
        currentPosition++;
      }

      stream.Seek(start, SeekOrigin.Begin);
      return (int)((currentPosition - 4) - start);
    }

    public static PackageTestResult IsIGAPak(IFile file)
    {
      if (Path.GetExtension(file.Name).ToLower() != ".pak")
        return PackageTestResult.NO;

      using (Stream stream = file.GetStream())
      {
        stream.Position = 0;
        var magic = stream.ReadUInt32BE();
        return magic == IGA_MAGIC ? PackageTestResult.YES : PackageTestResult.NO;
      }
    }

    public static IGAPackage OpenFile(IFile file)
    {
      return new IGAPackage(file);
    }
    
    public override void Dispose()
    {
      stream.Close();
      stream.Dispose();
    }
  }
}
