using System.IO;
using System.Text;

namespace LegacyX.Checker.Scanning;

/// <summary>
/// The parts of a Windows program (.exe, .dll, .sys) that say what it is without running it: whether it is signed, which
/// functions it asks Windows for, and how its sections are named (protectors leave their names there).
/// </summary>
public sealed class PeFile
{
    public bool IsSigned { get; private set; }
    public bool IsManaged { get; private set; }
    public List<string> Imports { get; } = new();
    public List<string> SectionNames { get; } = new();

    private sealed record Section(uint VirtualAddress, uint VirtualSize, uint RawPointer, uint RawSize);

    /// <summary>Reads only headers and the import table. Returns null when the file is not a program or cannot be read.</summary>
    public static PeFile? Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream, Encoding.ASCII);
            if (stream.Length < 0x200 || reader.ReadUInt16() != 0x5A4D) return null; // "MZ"
            stream.Position = 0x3C;
            var peOffset = reader.ReadInt32();
            if (peOffset < 0 || peOffset > stream.Length - 0x100) return null;
            stream.Position = peOffset;
            if (reader.ReadUInt32() != 0x00004550) return null; // "PE\0\0"
            stream.Position = peOffset + 6;
            var sectionCount = reader.ReadUInt16();
            stream.Position = peOffset + 20;
            var optionalSize = reader.ReadUInt16();
            var optionalStart = peOffset + 24;
            stream.Position = optionalStart;
            var magic = reader.ReadUInt16();
            var plus = magic == 0x20B;
            if (magic != 0x10B && !plus) return null;
            var directories = optionalStart + (plus ? 0x70 : 0x60);
            if (sectionCount == 0 || sectionCount > 96) return null;

            var pe = new PeFile();
            // Data directory 1 = imports, 4 = signature, 14 = .NET header.
            stream.Position = directories + 1 * 8;
            var importRva = reader.ReadUInt32();
            stream.Position = directories + 4 * 8 + 4;
            pe.IsSigned = reader.ReadUInt32() > 0;
            stream.Position = directories + 14 * 8;
            pe.IsManaged = reader.ReadUInt32() > 0;

            var sections = new List<Section>();
            stream.Position = optionalStart + optionalSize;
            for (var index = 0; index < sectionCount; index += 1)
            {
                var name = Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0');
                var virtualSize = reader.ReadUInt32();
                var virtualAddress = reader.ReadUInt32();
                var rawSize = reader.ReadUInt32();
                var rawPointer = reader.ReadUInt32();
                reader.ReadBytes(16);
                pe.SectionNames.Add(name);
                sections.Add(new Section(virtualAddress, virtualSize, rawPointer, rawSize));
            }
            if (importRva != 0) ReadImports(stream, reader, sections, importRva, plus, pe.Imports);
            return pe;
        }
        catch
        {
            return null;
        }
    }

    private static long Offset(List<Section> sections, uint rva)
    {
        foreach (var section in sections)
        {
            var size = Math.Max(section.VirtualSize, section.RawSize);
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + size) return section.RawPointer + (rva - section.VirtualAddress);
        }
        return -1;
    }

    private static string ReadAscii(Stream stream, BinaryReader reader, long offset)
    {
        if (offset < 0 || offset >= stream.Length) return "";
        stream.Position = offset;
        var builder = new StringBuilder();
        for (var count = 0; count < 128; count += 1)
        {
            var value = reader.ReadByte();
            if (value == 0) break;
            builder.Append((char)value);
        }
        return builder.ToString();
    }

    private static void ReadImports(Stream stream, BinaryReader reader, List<Section> sections, uint importRva, bool plus, List<string> imports)
    {
        var table = Offset(sections, importRva);
        if (table < 0) return;
        for (var descriptor = 0; descriptor < 256; descriptor += 1)
        {
            stream.Position = table + descriptor * 20L;
            var originalFirstThunk = reader.ReadUInt32();
            reader.ReadUInt32();
            reader.ReadUInt32();
            var nameRva = reader.ReadUInt32();
            var firstThunk = reader.ReadUInt32();
            if (nameRva == 0 && firstThunk == 0) break;
            imports.Add(ReadAscii(stream, reader, Offset(sections, nameRva)).ToLowerInvariant());
            var thunks = Offset(sections, originalFirstThunk != 0 ? originalFirstThunk : firstThunk);
            if (thunks < 0) continue;
            var step = plus ? 8 : 4;
            for (var entry = 0; entry < 4000; entry += 1)
            {
                stream.Position = thunks + entry * (long)step;
                var value = plus ? reader.ReadUInt64() : reader.ReadUInt32();
                if (value == 0) break;
                var byOrdinal = plus ? (value & 0x8000000000000000UL) != 0 : (value & 0x80000000UL) != 0;
                if (byOrdinal) continue;
                // A name entry points at a 2 byte hint and then the function name.
                var at = Offset(sections, (uint)(value & 0x7FFFFFFF));
                if (at < 0) continue;
                var name = ReadAscii(stream, reader, at + 2);
                if (name.Length > 0) imports.Add(name);
            }
        }
    }
}
