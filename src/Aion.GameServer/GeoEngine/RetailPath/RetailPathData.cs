using System.Buffers.Binary;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailPathHeightKind { ExplicitCentimetres, Terrain, NoGround }

/// <summary>A serialized height, without terrain projection or actor surface selection.</summary>
public readonly record struct RetailPathHeight(RetailPathHeightKind Kind, int Centimetres = 0);

/// <summary>Raw direction encoding and its native uint32 decoded value, without route traversal.</summary>
public readonly record struct RetailPathSparseLink(byte Encoding, uint StoredValue, uint DecodedValue, int FileOffset);

public sealed class RetailPathSparseRecord
{
    internal RetailPathSparseRecord(int blobOffset, int fileOffset, int byteLength, int height,
        short cellX, short cellY, byte flags, RetailPathSparseLink d0, RetailPathSparseLink d1,
        RetailPathSparseLink d2, RetailPathSparseLink d3)
    {
        BlobOffset = blobOffset; FileOffset = fileOffset; ByteLength = byteLength;
        HeightCentimetres = height; CellX = cellX; CellY = cellY; Flags = flags;
        (link0, link1, link2, link3) = (d0, d1, d2, d3);
    }

    private readonly RetailPathSparseLink link0, link1, link2, link3;
    public int BlobOffset { get; }
    public int FileOffset { get; }
    public int ByteLength { get; }
    public int HeightCentimetres { get; }
    public short CellX { get; }
    public short CellY { get; }
    public byte Flags { get; }
    public RetailPathSparseLink GetLink(int direction) => direction switch
    {
        0 => link0, 1 => link1, 2 => link2, 3 => link3,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}

public sealed class RetailPathNode
{
    internal RetailPathNode(int fileOffset, byte type, int fileIndex, int layer, int sectorX, int sectorY,
        byte edgeMode, uint[] edges, ReadOnlyMemory<byte> rawBytes, ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<byte> exitMasks, int blobOffset, IReadOnlyList<RetailPathSparseRecord> records)
    {
        FileOffset = fileOffset; Type = type; FileIndex = fileIndex; Layer = layer;
        SectorX = sectorX; SectorY = sectorY; EdgeMode = edgeMode; Edges = Array.AsReadOnly(edges);
        RawBytes = rawBytes; Payload = payload; ExitMasks = exitMasks;
        BlobOffset = blobOffset; SparseRecords = records;
    }

    public int FileOffset { get; }
    public byte Type { get; }
    public int FileIndex { get; }
    public int Layer { get; }
    /// <summary>Sector indices, not metres.</summary>
    public int SectorX { get; }
    public int SectorY { get; }
    public byte EdgeMode { get; }
    public IReadOnlyList<uint> Edges { get; }
    public ReadOnlyMemory<byte> RawBytes { get; }
    public ReadOnlyMemory<byte> Payload { get; }
    public ReadOnlyMemory<byte> ExitMasks { get; }
    public int BlobOffset { get; }
    public IReadOnlyList<RetailPathSparseRecord> SparseRecords { get; }

    public int GetExitMask(int cellIndex)
    {
        CheckCell(cellIndex);
        if (Type == 16) throw new InvalidOperationException("Sparse records carry individual direction links.");
        return ExitMasks.IsEmpty ? 15 : (ExitMasks.Span[cellIndex >> 1] >> ((cellIndex & 1) * 4)) & 15;
    }

    /// <summary>Decode a raw grid height tag; does not sample terrain or select a reachable layer.</summary>
    public RetailPathHeight GetHeight(int cellIndex)
    {
        CheckCell(cellIndex);
        var p = Payload.Span;
        var terrain = new RetailPathHeight(RetailPathHeightKind.Terrain);
        var none = new RetailPathHeight(RetailPathHeightKind.NoGround);
        switch (Type & ~1)
        {
            case 0: return Explicit(BinaryPrimitives.ReadInt32LittleEndian(p));
            case 2: return terrain;
            case 4:
                int value = BinaryPrimitives.ReadInt32LittleEndian(p[(cellIndex * 4)..]);
                return value == int.MaxValue ? none : Explicit(value);
            case 6: return ((p[cellIndex >> 3] >> (cellIndex & 7)) & 1) != 0 ? none : terrain;
            case 8:
                int two = (p[8 + (cellIndex >> 2)] >> ((cellIndex & 3) * 2)) & 3;
                return two == 2 ? terrain : two == 3 ? none : Explicit(BinaryPrimitives.ReadInt32LittleEndian(p[(two * 4)..]));
            case 10:
                int four = (p[56 + (cellIndex >> 1)] >> ((cellIndex & 1) * 4)) & 15;
                return four == 14 ? terrain : four == 15 ? none : Explicit(BinaryPrimitives.ReadInt32LittleEndian(p[(four * 4)..]));
            case 12:
                int delta8 = p[4 + cellIndex];
                return delta8 == 254 ? terrain : delta8 == 255 ? none :
                    Explicit(unchecked(BinaryPrimitives.ReadInt32LittleEndian(p) + delta8));
            case 14:
                int delta16 = BinaryPrimitives.ReadUInt16LittleEndian(p[(4 + cellIndex * 2)..]);
                return delta16 == 65534 ? terrain : delta16 == 65535 ? none :
                    Explicit(unchecked(BinaryPrimitives.ReadInt32LittleEndian(p) + delta16));
            default: throw new InvalidOperationException("Sparse records carry their own height values.");
        }
    }

    private static RetailPathHeight Explicit(int value) => new(RetailPathHeightKind.ExplicitCentimetres, value);
    private static void CheckCell(int index)
    {
        if ((uint)index >= 1024) throw new ArgumentOutOfRangeException(nameof(index));
    }
}

public sealed class RetailPathSector
{
    internal RetailPathSector(int x, int y, int fileOffset, IReadOnlyList<RetailPathNode> nodes)
    { X = x; Y = y; FileOffset = fileOffset; Nodes = nodes; }
    public int X { get; }
    public int Y { get; }
    public int FileOffset { get; }
    /// <summary>Serialized order. Layer IDs are reversed by the native loader's prepend operation.</summary>
    public IReadOnlyList<RetailPathNode> Nodes { get; }
    public RetailPathNode? GetLayer(int layer) => (uint)layer < (uint)Nodes.Count ? Nodes[Nodes.Count - 1 - layer] : null;
}

/// <summary>Immutable raw-backed representation. No search, passability or motion integration.</summary>
public sealed class RetailPathData
{
    internal RetailPathData(byte[] ownedBytes, int blobBytes, int tableOffset, int tableCount,
        int width, int height, IReadOnlyList<RetailPathSector> sectors, int parsedBytes,
        int nodeCount, int recordCount, bool referencesValidated)
    {
        RawFile = ownedBytes; SparseBlob = RawFile.Slice(132, blobBytes);
        LinkTableFileOffset = tableOffset; LinkTableCount = tableCount;
        WorldWidthMetres = width; WorldHeightMetres = height;
        SectorsX = (int)(((long)width + 15) / 16); SectorsY = (int)(((long)height + 15) / 16);
        Sectors = sectors; ParsedBytes = parsedBytes; NodeCount = nodeCount;
        UniqueSparseRecordCount = recordCount; ReferencesValidated = referencesValidated;
    }

    public ReadOnlyMemory<byte> RawFile { get; }
    public ReadOnlyMemory<byte> Header => RawFile[..128];
    public ReadOnlyMemory<byte> GuidBytes => RawFile[..16];
    public Guid BuildGuid => new(GuidBytes.Span);
    public ushort MinorVersion => BinaryPrimitives.ReadUInt16LittleEndian(RawFile.Span[16..]);
    public ushort MajorVersion => BinaryPrimitives.ReadUInt16LittleEndian(RawFile.Span[18..]);
    public ReadOnlyMemory<byte> SparseBlob { get; }
    public int LinkTableFileOffset { get; }
    public int LinkTableCount { get; }
    public int WorldWidthMetres { get; }
    public int WorldHeightMetres { get; }
    public int SectorsX { get; }
    public int SectorsY { get; }
    public IReadOnlyList<RetailPathSector> Sectors { get; }
    public int ParsedBytes { get; }
    public ReadOnlyMemory<byte> TrailingBytes => RawFile[ParsedBytes..];
    public int NodeCount { get; }
    public int UniqueSparseRecordCount { get; }
    /// <summary>Used directed targets were structurally checked; this proves no actor passability or routing equivalence.</summary>
    public bool ReferencesValidated { get; }
    public uint GetLinkTableEntry(int index)
    {
        if ((uint)index >= (uint)LinkTableCount) throw new ArgumentOutOfRangeException(nameof(index));
        return BinaryPrimitives.ReadUInt32LittleEndian(RawFile.Span[(LinkTableFileOffset + index * 4)..]);
    }
    public RetailPathSector GetSector(int x, int y)
    {
        if ((uint)x >= (uint)SectorsX || (uint)y >= (uint)SectorsY) throw new ArgumentOutOfRangeException(nameof(x));
        return Sectors[y * SectorsX + x];
    }
}
