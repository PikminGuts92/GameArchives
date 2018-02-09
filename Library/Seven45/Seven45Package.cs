﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GameArchives.Seven45
{
  class Seven45Package : AbstractPackage
  {
    private Stream headerStream;
    private Stream[] contentFiles;
    private Common.DefaultDirectory root;

    public static PackageTestResult IsSeven45(IFile fn)
    {
      if (fn.Name.ToLower().EndsWith(".hdr.e.2"))
      {
        using (var stream = fn.GetStream())
        using (var s = new PowerChordCryptStream(stream))
        {
          if (s.ReadUInt32LE() == 0x745)
          {
            return PackageTestResult.YES;
          }
        }
      }
      return PackageTestResult.NO;
    }

    public static Seven45Package OpenFile(IFile f)
    {
      return new Seven45Package(f);
    }

    // Only one game uses this format and there is no datafile count in the header...
    const int NUM_DATA_FILES = 3;
    private Seven45Package(IFile f)
    {
      FileName = f.Name;
      headerStream = new PowerChordCryptStream(f.GetStream());
      contentFiles = new Stream[NUM_DATA_FILES];
      root = new Common.DefaultDirectory(null, "/");
      var baseName = f.Name.Replace(".hdr.e.2", "");
      for(var x = 0; x < NUM_DATA_FILES; x++)
      {
        contentFiles[x] = f.Parent.GetFile($"{baseName}.pk{x}").GetStream();
      }
      ParseHeader();
    }

    private class Header
    {
      public uint magic_seven45;
      public uint version;
      public uint block_size;
      public uint num_files;
      public uint num_unk;
      public uint num_dirs;
      public byte[] unk;
      public uint string_table_offset;
      public uint string_table_size;
      public uint num_offsets;

      public static Header Read(Stream s) => new Header
      {
        magic_seven45 = s.ReadUInt32LE(),
        version = s.ReadUInt32LE(),
        block_size = s.ReadUInt32LE(),
        num_files = s.ReadUInt32LE(),
        num_unk = s.ReadUInt32LE(),
        num_dirs = s.ReadUInt32LE(),
        unk = s.ReadBytes(8),
        string_table_offset = s.ReadUInt32LE(),
        string_table_size = s.ReadUInt32LE(),
        num_offsets = s.ReadUInt32LE()
      };
    }

    private struct FileEntry
    {
      public byte[] unk;
      public ushort dir_num;
      public uint string_num;
      public uint offset_num;
      public long size;
      public byte[] unk_2;
      public long time;

      public static FileEntry Read(Stream s) => new FileEntry
      {
        unk = s.ReadBytes(6),
        dir_num = s.ReadUInt16LE(),
        string_num = s.ReadUInt32LE(),
        offset_num = s.ReadUInt32LE(),
        size = s.ReadInt64LE(),
        unk_2 = s.ReadBytes(16),
        time = s.ReadInt64LE()
      };
    }

    private class DirEntry
    {
      public uint path_hash;
      public int parent;
      public uint string_num;

      public static DirEntry Read(Stream s) => new DirEntry
      {
        path_hash = s.ReadUInt32LE(),
        parent = s.ReadInt32LE(),
        string_num = s.ReadUInt32LE()
      };
    }

    private class OffsetEntry
    {
      public uint pk_offset;
      public ushort pk_num;
      public ushort unk;

      public static OffsetEntry Read(Stream s) => new OffsetEntry
      {
        pk_offset = s.ReadUInt32LE(),
        pk_num = s.ReadUInt16LE(),
        unk = s.ReadUInt16LE()
      };
    }

    private void ParseHeader()
    {
      headerStream.Position = 0;

      var header = Header.Read(headerStream);
      var fileEntries = new FileEntry[header.num_files];
      for(var i = 0; i < fileEntries.Length; i++)
      {
        fileEntries[i] = FileEntry.Read(headerStream);
      }

      var dirEntries = new DirEntry[header.num_dirs];
      for(var i = 0; i < dirEntries.Length; i++)
      {
        dirEntries[i] = DirEntry.Read(headerStream);
      }

      var stringTable = new string[header.num_files];
      var stringTableEnd = header.string_table_offset + header.string_table_size;
      var x = 0;
      while(headerStream.Position < stringTableEnd)
      {
        stringTable[x++] = headerStream.ReadASCIINullTerminated();
      }

      var fileOffsets = new OffsetEntry[header.num_offsets];
      for(var i = 0; i < fileOffsets.Length; i++)
      {
        fileOffsets[i] = OffsetEntry.Read(headerStream);
      }

      var dirs_flat = new Common.DefaultDirectory[header.num_dirs];
      dirs_flat[0] = root;
      for(var i = 1; i < dirs_flat.Length; i++)
      {
        var parent = dirs_flat[dirEntries[i].parent];
        parent.AddDir(dirs_flat[i] = new Common.DefaultDirectory(parent, stringTable[dirEntries[i].string_num]));
      }

      for(var i = 0; i < fileEntries.Length; i++)
      {
        var entry = fileEntries[i];
        var name = entry.string_num < stringTable.Length ? stringTable[entry.string_num] : "ERROR_FILENAME";
        dirs_flat[entry.dir_num].AddFile(new Common.OffsetFile(
          name,
          dirs_flat[entry.dir_num],
          contentFiles[fileOffsets[entry.offset_num].pk_num],
          fileOffsets[entry.offset_num].pk_offset,
          entry.size));
      }
    }

    public override string FileName { get; }

    public override IDirectory RootDirectory => root;

    public override long Size => headerStream.Length + contentFiles.Sum(f => f.Length);

    public override bool Writeable => false;

    public override void Dispose()
    {
      headerStream.Dispose();
      foreach(var x in contentFiles)
      {
        x.Dispose();
      }
    }
  }
}
