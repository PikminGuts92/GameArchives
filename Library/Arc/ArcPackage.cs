using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace GameArchives.Arc
{
  public class ArcPackage : AbstractPackage
  {
    private const uint ARC_MAGIC = 0x19751120;

    public static PackageTestResult IsArc(IFile file)
    {
      switch (Path.GetExtension(file.Name).ToLower())
      {
        case ".arc":
          break;
        default:
          return PackageTestResult.NO;
      }

      using (Stream stream = file.GetStream())
      {
        stream.Position = 0;
        uint magic = stream.ReadUInt32BE();
        return magic == ARC_MAGIC ? PackageTestResult.YES : PackageTestResult.NO;
      }
    }

    public static ArcPackage OpenFile(IFile file)
    {
      return new ArcPackage(file);
    }

    public override string FileName { get; }
    public override IDirectory RootDirectory => root;
    public override long Size => stream.Length;
    public override bool Writeable => false;
    public override Type FileType => typeof(ArcFile);

    private Stream stream;
    private ArcDirectory root;

    public ArcPackage(IFile file)
    {
      FileName = file.Name;
      root = new ArcDirectory(null, ROOT_DIR);
      stream = file.GetStream();

      // Go to entries
      stream.Seek(8, SeekOrigin.Begin);

      uint entry_count = stream.ReadUInt32BE();
      stream.Position += 4; // Skip 0 constant

      long entryOffset = stream.Position;

      // Read entries
      for (uint i = 0; i < entry_count; i++)
      {
        uint pathOffset;
        uint offset, size, compressedSize;
        string pathName, fileName;

        // Read offset details
        stream.Seek(entryOffset, SeekOrigin.Begin);
        pathOffset = stream.ReadUInt32BE();
        offset = stream.ReadUInt32BE();
        size = stream.ReadUInt32BE();
        compressedSize = stream.ReadUInt32BE();

        entryOffset = stream.Position;

        // Read file path
        stream.Seek(pathOffset, SeekOrigin.Begin);
        pathName = stream.ReadASCIINullTerminated();

        // Add file
        ArcDirectory dir = MakeOrGetDirectory(pathName);
        fileName = pathName.Split('/').Last();
        dir.AddFile(new ArcFile(fileName, dir, size, compressedSize, offset, stream));
      }
    }

    private ArcDirectory MakeOrGetDirectory(string path)
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
          current = new ArcDirectory(last, breadcrumbs[idx]);
          (last as ArcDirectory).AddDir(current as ArcDirectory);
        }
        last = current;
      }

      return last as ArcDirectory;
    }

    public override void Dispose()
    {
      stream.Close();
      stream.Dispose();
    }
  }
}
