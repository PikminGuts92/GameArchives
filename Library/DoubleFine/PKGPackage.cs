using System;
using System.IO;

namespace GameArchives.DoubleFine
{
  public class PKGPackage : AbstractPackage
  {
    private const string PKG_MAGIC = "ZPKG";
    private const string PKG_EXT = ".pkg";
    private const uint PKG_VERSION = 1;

    public static PackageTestResult IsPkg(IFile file)
    {
      switch (Path.GetExtension(file.Name)?.ToLower())
      {
        case PKG_EXT:
          break;
        default:
          return PackageTestResult.NO;
      }

      // Check if input file has pkg header
      if (!HasPkgMagicAndVersion(file))
      {
        return PackageTestResult.NO;
      }

      return PackageTestResult.YES;
    }

    private static bool HasPkgMagicAndVersion(IFile file)
    {
      using var stream = file.GetStream();
      stream.Position = 0;

      var magic = stream.ReadASCIINullTerminated(PKG_MAGIC.Length);
      var version = stream.ReadUInt32LE();

      return PKG_MAGIC.Equals(magic, StringComparison.InvariantCultureIgnoreCase)
        && version == PKG_VERSION;
    }

    public static PKGPackage OpenFile(IFile file)
    {
      return new PKGPackage(file);
    }

    public override string FileName { get; }
    public override IDirectory RootDirectory => root;
    public override long Size => stream.Length;
    public override bool Writeable => false;
    public override Type FileType => typeof(PKGFile);

    private Stream stream;
    private PKGDirectory root;

    public PKGPackage(IFile file)
    {
      FileName = file.Name;
      root = new PKGDirectory(null, ROOT_DIR);
      stream = file.GetStream();

      // Skip magic + version + end header offset
      stream.Seek(12, SeekOrigin.Begin);

      var file_count = stream.ReadUInt32LE();
      stream.Seek(8, SeekOrigin.Current); // Skip unknown offset + count
      var file_names_offset = stream.ReadUInt32LE();
      var extensions_offset = stream.ReadUInt32LE();

      // Read file entries
      stream.Seek(512, SeekOrigin.Begin);
      for (uint i = 0; i < file_count; i++)
      {
        stream.Seek(1, SeekOrigin.Current);
        var rel_ext_offset = stream.ReadUInt16LE();
        stream.Seek(1, SeekOrigin.Current);

        var rel_name_offset = stream.ReadUInt32LE();

        var file_offset = stream.ReadUInt32LE();
        var file_size = stream.ReadUInt32LE();

        // Read file name + extension
        var current_offset = stream.Position;
        stream.Seek(file_names_offset + rel_name_offset, SeekOrigin.Begin);
        var name = stream.ReadASCIINullTerminated();

        stream.Seek(extensions_offset + rel_ext_offset, SeekOrigin.Begin);
        var ext = stream.ReadASCIINullTerminated();

        var file_name = string.IsNullOrEmpty(ext) ? name : $"{name}.{ext}";
        root.AddFile(new PKGFile(file_name, root, file_size, file_offset, stream));

        stream.Seek(current_offset, SeekOrigin.Begin);
      }
    }

    public override void Dispose()
    {
      stream.Close();
      stream.Dispose();
    }
  }
}
