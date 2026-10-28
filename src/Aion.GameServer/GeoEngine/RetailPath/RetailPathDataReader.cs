using System.Buffers.Binary;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>
/// Inactive, bounded 6.5 serialization reader. Native evidence: NPCServer 0x140220f60,
/// flat 0x14021f330, terrain 0x14021f730, simple 0x14021fc30, complex 0x140220850.
/// It does not replace GeoService, choose actor surfaces or activate NPC routing.
/// </summary>
public static class RetailPathDataReader
{
    public static RetailPathReadResult Read(ReadOnlySpan<byte> bytes, RetailPathReadOptions options)
    {
        var optionError = ValidateOptions(options);
        if (optionError is not null) return Failure(optionError);
        if (bytes.Length > options.MaxInputBytes)
            return Failure(new(RetailPathErrorCode.InputLimitExceeded, 0, "Path input exceeds the configured byte budget."));
        if (bytes.Length < 136)
            return Failure(new(RetailPathErrorCode.Truncated, bytes.Length, "A path header and both table lengths require at least 136 bytes."));
        // Own the bytes: subsequent mutation of a caller's array must not change parsed data.
        return new Parser(bytes.ToArray(), options).Run();
    }

    public static RetailPathReadResult Read(Stream stream, RetailPathReadOptions options)
    {
        var optionError = ValidateOptions(options);
        if (optionError is not null) return Failure(optionError);
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) return Failure(new(RetailPathErrorCode.InputOutput, 0, "Stream is not readable."));
        try
        {
            // The growable input buffer and owned parser copy coexist temporarily. Byte/count
            // budgets are separate defensive limits, not a global managed-memory peak cap.
            using var copy = new MemoryStream();
            var chunk = new byte[16 * 1024];
            while (true)
            {
                int read = stream.Read(chunk, 0, chunk.Length);
                if (read == 0) break;
                if (copy.Length + read > options.MaxInputBytes)
                    return Failure(new(RetailPathErrorCode.InputLimitExceeded, copy.Length, "Path stream exceeds the configured byte budget."));
                copy.Write(chunk, 0, read);
            }
            return Read(copy.GetBuffer().AsSpan(0, (int)copy.Length), options);
        }
        catch (IOException ex)
        {
            return Failure(new(RetailPathErrorCode.InputOutput, -1, ex.Message));
        }
    }

    private static RetailPathReadError? ValidateOptions(RetailPathReadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.WorldWidthMetres <= 0 || options.WorldHeightMetres <= 0 ||
            options.MaxInputBytes < 136 || options.MaxSectors <= 0 || options.MaxNodes < 0 ||
            options.MaxSparseRecords < 0 || options.MaxLinkTableEntries < 0 ||
            options.MaxMetadataBytes < 0 || options.MaxDiagnostics < 0)
            return new(RetailPathErrorCode.InvalidOptions, 0, "World dimensions must be explicit positive metres, and budgets must be valid.");
        long sx = ((long)options.WorldWidthMetres + 15) / 16;
        long sy = ((long)options.WorldHeightMetres + 15) / 16;
        if (sx * sy > options.MaxSectors || sx * sy * 128 > options.MaxMetadataBytes)
            return new(RetailPathErrorCode.AllocationLimitExceeded, 0, "Explicit sector dimensions exceed the sector or metadata budget.");
        return null;
    }

    private static RetailPathReadResult Failure(RetailPathReadError error) =>
        new(null, error, Array.Empty<RetailPathDiagnostic>(), 0);

    private sealed class ParseFailure(RetailPathReadError error) : Exception
    { public RetailPathReadError Error { get; } = error; }

    private sealed class Parser(byte[] bytes, RetailPathReadOptions options)
    {
        private readonly List<RetailPathDiagnostic> diagnostics = [];
        private readonly Dictionary<int, RetailPathSparseRecord> recordBoundaries = [];
        private int suppressed, position, blobBytes, linkOffset, linkCount, nodeCount, recordCount;
        private long metadataBytes;
        private readonly int sectorsX = (int)(((long)options.WorldWidthMetres + 15) / 16);
        private readonly int sectorsY = (int)(((long)options.WorldHeightMetres + 15) / 16);
        private RetailPathData? data;

        public RetailPathReadResult Run()
        {
            try
            {
                ushort minor = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(16));
                ushort major = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(18));
                if (major != 6 || (minor != 5 && !options.AllowOtherMajor6MinorVersions))
                    Fail(RetailPathErrorCode.UnsupportedVersion, 16, $"Supported path format is 6.5; input is {major}.{minor}.");
                if (minor != 5)
                    Warn("MinorVersionMismatch", 16, $"Major 6 minor {minor} parsed using the explicitly requested 6.5 layout; native logs this mismatch.");
                if (options.ExpectedCollisionGuid is Guid expected && new Guid(bytes.AsSpan(0, 16)) != expected)
                    Warn("CollisionGuidMismatch", 0, "Path GUID differs from the caller's collision GUID; native logs and continues. Routing compatibility remains a caller decision.");
                if (bytes.AsSpan(20, 108).IndexOfAnyExcept((byte)0) >= 0)
                    Warn("ReservedHeaderBytes", 20, "Nonzero reserved header bytes are preserved without interpretation.");
                position = 128;
                blobBytes = ReadInt();
                if (blobBytes <= 0) Fail(RetailPathErrorCode.InvalidLength, 128, "Native requires a positive sparse blob length.");
                Take(blobBytes);
                int countOffset = position;
                linkCount = ReadInt();
                if (linkCount < 0) Fail(RetailPathErrorCode.InvalidLength, countOffset, "Link table count is negative.");
                if (linkCount > options.MaxLinkTableEntries)
                    Fail(RetailPathErrorCode.AllocationLimitExceeded, countOffset, "Link table count exceeds its budget.");
                linkOffset = position;
                Take(CheckedBytes(linkCount, 4, countOffset));
                int sectorCount = checked(sectorsX * sectorsY);
                Spend((long)sectorCount * 128, position);
                var sectors = new List<RetailPathSector>(sectorCount);
                for (int sy = 0; sy < sectorsY; sy++)
                for (int sx = 0; sx < sectorsX; sx++)
                {
                    int offset = position;
                    byte count = ReadByte();
                    if (count >= 128) Fail(RetailPathErrorCode.InvalidNodeCount, offset, "Sector node count is a negative signed byte.");
                    if ((long)nodeCount + count > options.MaxNodes)
                        Fail(RetailPathErrorCode.AllocationLimitExceeded, offset, "Node count exceeds its budget.");
                    Spend((long)count * 256, offset);
                    var nodes = new List<RetailPathNode>(count);
                    for (int k = 0; k < count; k++) nodes.Add(ReadNode(sx, sy, k, count - 1 - k));
                    nodeCount += count;
                    sectors.Add(new(sx, sy, offset, nodes.AsReadOnly()));
                }
                if (position < bytes.Length)
                {
                    if (options.RequireExactEnd) Fail(RetailPathErrorCode.TrailingBytes, position, "Input has bytes beyond the explicit world sector grid.");
                    Warn("TrailingBytes", position, $"Preserved {bytes.Length - position} bytes after the explicit grid; native does not enforce EOF.");
                }
                CheckBlobCoverage();
                data = new(bytes, blobBytes, linkOffset, linkCount, options.WorldWidthMetres, options.WorldHeightMetres,
                    sectors.AsReadOnly(), position, nodeCount, recordBoundaries.Count, options.ValidateReferences);
                if (options.ValidateReferences) CheckReferences();
                else Warn("ReferencesNotValidated", linkOffset, "Only serialization was validated; directed link references remain unchecked.");
                return new(data, null, diagnostics.AsReadOnly(), suppressed);
            }
            catch (ParseFailure ex)
            {
                return new(null, ex.Error, diagnostics.AsReadOnly(), suppressed);
            }
        }

        private RetailPathNode ReadNode(int sx, int sy, int fileIndex, int layer)
        {
            int start = position;
            byte type = ReadByte();
            if (type > 16) Fail(RetailPathErrorCode.UnknownNodeType, start, $"Unknown node type {type}.");
            if (type == 16)
            {
                int off = ReadInt();
                ushort count = ReadUShort();
                if (off < 0 || (long)off + (long)count * 9 > blobBytes)
                    Fail(RetailPathErrorCode.InvalidSparseRecord, start + 1, "Sparse record range cannot fit inside the blob.");
                if ((long)recordCount + count > options.MaxSparseRecords)
                    Fail(RetailPathErrorCode.AllocationLimitExceeded, start + 5, "Sparse record count exceeds its budget.");
                Spend((long)count * 256, start);
                var records = new List<RetailPathSparseRecord>(count);
                int cursor = off;
                for (int i = 0; i < count; i++)
                {
                    var record = ReadRecord(cursor, off);
                    records.Add(record);
                    if (!recordBoundaries.TryAdd(cursor, record))
                    {
                        if (options.RejectOverlappingSparseRecords)
                            Fail(RetailPathErrorCode.SparseRecordOverlap, record.FileOffset, "Sparse records share a blob range.");
                        Warn("SharedSparseRecord", record.FileOffset, "Multiple serialized nodes reference the same raw record; each node-relative interpretation is retained.");
                    }
                    cursor += record.ByteLength;
                }
                recordCount += count;
                return new(start, type, fileIndex, layer, sx, sy, 0, [], bytes.AsMemory(start, position - start),
                    ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, off, records.AsReadOnly());
            }
            byte mode = ReadByte();
            var edges = new uint[4];
            for (int d = 0; d < 4; d++) edges[d] = ReadUInt();
            int size = (type & ~1) switch
            { 0 => 4, 2 => 0, 4 => 4096, 6 => 128, 8 => 264, 10 => 568, 12 => 1028, 14 => 2052, _ => 0 };
            var payload = Take(size);
            var exits = (type & 1) != 0 ? Take(512) : ReadOnlyMemory<byte>.Empty;
            return new(start, type, fileIndex, layer, sx, sy, mode, edges, bytes.AsMemory(start, position - start), payload,
                exits, 0, Array.Empty<RetailPathSparseRecord>());
        }

        private RetailPathSparseRecord ReadRecord(int off, int nodeBlobOffset)
        {
            BlobRange(off, 9);
            var p = bytes.AsSpan(132 + off);
            int z = BinaryPrimitives.ReadInt32LittleEndian(p);
            short x = BinaryPrimitives.ReadInt16LittleEndian(p[4..]);
            short y = BinaryPrimitives.ReadInt16LittleEndian(p[6..]);
            byte flags = p[8];
            int cursor = off + 9;
            RetailPathSparseLink Link(int direction)
            {
                byte encoding = (byte)((flags >> (direction * 2)) & 3);
                int field = cursor;
                uint stored = 0, decoded = 0;
                if (encoding == 1)
                {
                    BlobRange(cursor, 2);
                    stored = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(132 + cursor));
                    // Native computes the relative encoding in uint32 arithmetic.
                    decoded = unchecked(((uint)nodeBlobOffset + stored) << 7);
                    cursor += 2;
                }
                else if (encoding == 2)
                {
                    BlobRange(cursor, 4);
                    stored = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(132 + cursor));
                    decoded = stored; cursor += 4;
                }
                return new(encoding, stored, decoded, 132 + field);
            }
            var d0 = Link(0); var d1 = Link(1); var d2 = Link(2); var d3 = Link(3);
            return new(off, 132 + off, cursor - off, z, x, y, flags, d0, d1, d2, d3);
        }

        private void CheckBlobCoverage()
        {
            int end = 0;
            long covered = 0;
            foreach (int start in recordBoundaries.Keys.Order())
            {
                var record = recordBoundaries[start];
                int next = start + record.ByteLength;
                if (start < end)
                {
                    if (options.RejectOverlappingSparseRecords)
                        Fail(RetailPathErrorCode.SparseRecordOverlap, 132 + start, "Sparse record ranges overlap.");
                    Warn("SparseRecordOverlap", 132 + start, "Raw record ranges overlap; bytes remain preserved and activation needs an explicit compatibility decision.");
                }
                covered += System.Math.Max(0, next - System.Math.Max(start, end));
                end = System.Math.Max(end, next);
            }
            if (covered < blobBytes)
                Warn("UnreferencedBlobBytes", 132, $"Preserved {blobBytes - covered} unreferenced blob bytes, including any reserved prefix; native does not require full coverage.");
        }

        private void CheckReferences()
        {
            foreach (var sector in data!.Sectors)
            foreach (var node in sector.Nodes)
            {
                if (node.Type == 16)
                {
                    foreach (var record in node.SparseRecords)
                    {
                        if (record.CellX / 32 != sector.X || record.CellY / 32 != sector.Y || record.CellX < 0 || record.CellY < 0)
                            Warn("SparseRecordOutsideOwnerSector", record.FileOffset, "Raw sparse coordinates lie outside their owning serialized sector.");
                        for (int d = 0; d < 4; d++)
                        {
                            var link = record.GetLink(d);
                            if (link.DecodedValue != 0)
                                CheckTarget(node, record.CellX, record.CellY, d, link.DecodedValue, link.FileOffset, false);
                        }
                    }
                    continue;
                }
                for (int d = 0; d < 4; d++)
                for (int along = 0; along < 32; along++)
                {
                    int lx = d == 0 ? 31 : d == 2 ? 0 : along;
                    int ly = d == 1 ? 31 : d == 3 ? 0 : along;
                    int cell = ly * 32 + lx;
                    if (node.GetHeight(cell).Kind == RetailPathHeightKind.NoGround) continue;
                    // Native odd-type neighbor consumers check the per-cell exit nibble only
                    // inside a sector. Crossings dispatch straight to the common edge consumer.
                    // A zero local exit bit must not hide a used boundary target from validation.
                    bool mask = (node.EdgeMode & (1 << d)) != 0;
                    uint link;
                    int offset;
                    if (mask)
                    {
                        if ((node.Edges[d] & (1u << along)) != 0) continue;
                        link = 0x80; offset = node.FileOffset + 2 + d * 4;
                    }
                    else
                    {
                        long index = (long)node.Edges[d] + along;
                        offset = node.FileOffset + 2 + d * 4;
                        if (index >= linkCount) Fail(RetailPathErrorCode.LinkTableOutOfRange, offset, "Used sector edge indexes outside the link table.");
                        offset = linkOffset + (int)index * 4;
                        link = data!.GetLinkTableEntry((int)index);
                        if (link == 0) continue;
                    }
                    CheckTarget(node, sector.X * 32 + lx, sector.Y * 32 + ly, d, link, offset, mask);
                }
            }
        }

        private void CheckTarget(RetailPathNode source, int cx, int cy, int direction, uint link, int offset, bool mask)
        {
            int dx = direction == 0 ? 1 : direction == 2 ? -1 : 0;
            int dy = direction == 1 ? 1 : direction == 3 ? -1 : 0;
            int tx = cx + dx, ty = cy + dy;
            if (tx < 0 || ty < 0 || tx / 32 >= sectorsX || ty / 32 >= sectorsY)
                Fail(RetailPathErrorCode.TargetSectorOutOfRange, offset, "Used directed link leaves the explicit world sector grid.");
            bool sameSector = tx / 32 == source.SectorX && ty / 32 == source.SectorY;
            // Sparse same-sector links retain this sector object and ignore the layer bits in native.
            var target = sameSector ? source : data!.GetSector(tx / 32, ty / 32).GetLayer(mask ? 0 : (int)(link & 127));
            if (target is null) Fail(RetailPathErrorCode.TargetLayerOutOfRange, offset, "Used directed link references a missing target layer.");
            if (target!.Type != 16)
            {
                if (!mask && (link >> 7) != 1)
                    Warn("NoncanonicalGridLinkOffset", offset, "Native selects a grid target by node type and ignores its blob offset; raw noncanonical link retained.");
                if (target.GetHeight((ty & 31) * 32 + (tx & 31)).Kind == RetailPathHeightKind.NoGround)
                    Warn("GridLinkToNoGround", offset, "A directed link names a grid no-ground tag; actor/passability interpretation remains unresolved.");
                return;
            }
            int recordOffset = (int)(link >> 7);
            if (!recordBoundaries.TryGetValue(recordOffset, out var record))
                Fail(RetailPathErrorCode.TargetRecordOutOfRange, offset, "Used directed link does not land on a parsed sparse record boundary.");
            if (record!.CellX != tx || record.CellY != ty)
                Warn("SparseTargetCoordinateMismatch", offset, "Raw link record coordinates differ from the adjacent cell; native pointer interpretation is retained for review.");
        }

        private void Spend(long cost, int offset)
        {
            if (cost > options.MaxMetadataBytes - metadataBytes)
                Fail(RetailPathErrorCode.AllocationLimitExceeded, offset, "Conservative metadata allocation estimate exceeds its budget.");
            metadataBytes += cost;
        }
        private static int CheckedBytes(int count, int unit, int offset)
        {
            if ((long)count * unit > int.MaxValue)
                throw new ParseFailure(new(RetailPathErrorCode.InvalidLength, offset, "Table byte size overflows the supported range."));
            return count * unit;
        }
        private void BlobRange(int offset, int count)
        {
            if (offset < 0 || count > blobBytes - offset)
                Fail(RetailPathErrorCode.InvalidSparseRecord, 132L + offset, "Sparse record or link field runs outside the blob.");
        }
        private ReadOnlyMemory<byte> Take(int count)
        {
            if (count < 0 || count > bytes.Length - position)
                Fail(RetailPathErrorCode.Truncated, position, "Serialized field runs outside the input.");
            var result = bytes.AsMemory(position, count); position += count; return result;
        }
        private byte ReadByte() => Take(1).Span[0];
        private ushort ReadUShort() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2).Span);
        private int ReadInt() => BinaryPrimitives.ReadInt32LittleEndian(Take(4).Span);
        private uint ReadUInt() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4).Span);
        private void Warn(string code, long offset, string message)
        {
            if (diagnostics.Count < options.MaxDiagnostics)
            {
                Spend(256, (int)System.Math.Clamp(offset, 0L, int.MaxValue));
                diagnostics.Add(new(code, offset, message));
            }
            else if (suppressed < int.MaxValue) suppressed++;
        }
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        private static void Fail(RetailPathErrorCode code, long offset, string message) =>
            throw new ParseFailure(new(code, offset, message));
    }
}
