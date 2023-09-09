using GameArchives.Arc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace GameArchives.Konami
{
  public class KonamiPackage : AbstractPackage
  {
    private const string KONAMI_MAGIC = "Konami Computer Entertainment Hawaii, Inc.";
    private const string EXT_KONAMI_BIN = ".bin";
    private const string EXT_KONAMI_HEADER = ".hbn";

    public static PackageTestResult IsKonami(IFile file)
    {
      bool isBin = false;

      switch (Path.GetExtension(file.Name)?.ToLower())
      {
        case EXT_KONAMI_BIN:
          isBin = true;
          break;
        case EXT_KONAMI_HEADER:
          break;
        default:
          return PackageTestResult.NO;
      }

      // Check if input file has konami header
      if (!HasKonamiMagic(file))
      {
        return PackageTestResult.NO;
      }

      var fileName = Path.GetFileNameWithoutExtension(file.Name);

      if (isBin)
      {
        // Check for paired header
        var fileNameHeader = $"{fileName}{EXT_KONAMI_HEADER}";
        var headerFile = file
          .Parent
          .Files
          .FirstOrDefault(x => x.Name.Equals(fileNameHeader, StringComparison.InvariantCultureIgnoreCase));

        // Verify header has magic
        if (headerFile is not null && HasKonamiMagic(headerFile))
        {
          return PackageTestResult.YES;
        }
      }
      else
      {
        // Check for paired bin
        var fileNameBin = $"{fileName}{EXT_KONAMI_BIN}";
        var binFile = file
          .Parent
          .Files
          .FirstOrDefault(x => x.Name.Equals(fileNameBin, StringComparison.InvariantCultureIgnoreCase));

        // Verify bin has magic
        if (binFile is not null && HasKonamiMagic(binFile))
        {
          return PackageTestResult.YES;
        }
      }

      return PackageTestResult.NO;
    }

    private static bool HasKonamiMagic(IFile file)
    {
      using var stream = file.GetStream();

      stream.Position = 0;
      var magic = stream.ReadASCIINullTerminated(KONAMI_MAGIC.Length);

      return KONAMI_MAGIC.Equals(magic, StringComparison.InvariantCultureIgnoreCase);
    }

    public static KonamiPackage OpenFile(IFile file)
    {
      return new KonamiPackage(file);
    }

    public override string FileName { get; }
    public override IDirectory RootDirectory => root;
    public override long Size => stream.Length;
    public override bool Writeable => false;
    public override Type FileType => typeof(ArcFile);

    private Stream stream;
    private KonamiDirectory root;

    public KonamiPackage(IFile file)
    {
      var (headerFile, binFile) = GetHeaderAndBinFiles(file);

      FileName = binFile.Name;
      root = new KonamiDirectory(null, ROOT_DIR);
      stream = binFile.GetStream();

      // Parse header file
      using var reader = new StreamReader(headerFile.GetStream(), Encoding.UTF8);

      // Skip header
      reader.ReadLine();

      // Not sure if comment is always here... assuming "!!" is comment
      // Skip comments and whitespace lines
      var currentLine = reader.ReadLine();
      while (!reader.EndOfStream && (currentLine.StartsWith("!!") || string.IsNullOrWhiteSpace(currentLine)))
      {
        currentLine = reader.ReadLine().Trim();
      }

      // Try parsing file count
      int fileCount;

      if (string.IsNullOrEmpty(currentLine))
      {
        throw new Exception("No file count found in header (.hbn)");
      }
      else if (currentLine.Any(c => !char.IsNumber(c)))
      {
        throw new Exception($"Unexpected value of \"{currentLine}\" found for file count in header (.hbn)");
      }
      else if (!int.TryParse(currentLine, out fileCount))
      {
        throw new Exception($"Unable to parse value of \"{currentLine}\" as integer for file count in header (.hbn)");
      }

      // Parse entry offsets
      var foundFileCount = 0;
      while (!reader.EndOfStream && foundFileCount < fileCount)
      {
        // Read line
        currentLine = reader.ReadLine().Trim();
        if (currentLine.StartsWith("!!")) continue; // Skip comment. Not found but assuming possible.

        var splitLine = currentLine.Split(',');

        var fileName = splitLine[0];
        var offset = int.Parse(splitLine[1]);
        var size = int.Parse(splitLine[2]);
        var unknown = int.Parse(splitLine[3]);

        // Add file
        root.AddFile(new KonamiFile(fileName, root, size, offset, unknown, stream));

        foundFileCount++;
      }

      if (foundFileCount < fileCount)
      {
        throw new Exception($"Number of parsed file entries of {foundFileCount} in header (.hbn) does not match given count of {fileCount}");
      }
    }

    private (IFile, IFile) GetHeaderAndBinFiles(IFile file)
    {
      var fileName = Path.GetFileNameWithoutExtension(file.Name);
      var fileNameHeader = $"{fileName}{EXT_KONAMI_HEADER}";
      var fileNameBin = $"{fileName}{EXT_KONAMI_BIN}";

      var files = file
        .Parent
        .Files
        .Where(x => x.Name.Equals(fileNameHeader, StringComparison.InvariantCultureIgnoreCase)
          || x.Name.Equals(fileNameBin, StringComparison.InvariantCultureIgnoreCase))
        .OrderByDescending(x => x.Name.ToLower()) // Pushes header to first element
        .ToList();

      // At this point, both files should exist
      return (files[0], files[1]);
    }

    public override void Dispose()
    {
      stream.Close();
      stream.Dispose();
    }
  }
}
