using System.Buffers.Binary;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailPathDataReaderTests
{
    [Theory]
    [InlineData(0, 4)] [InlineData(1, 4)] [InlineData(2, 0)] [InlineData(3, 0)]
    [InlineData(4, 4096)] [InlineData(5, 4096)] [InlineData(6, 128)] [InlineData(7, 128)]
    [InlineData(8, 264)] [InlineData(9, 264)] [InlineData(10, 568)] [InlineData(11, 568)]
    [InlineData(12, 1028)] [InlineData(13, 1028)] [InlineData(14, 2052)] [InlineData(15, 2052)]
    public void PreservesEveryGridEncodingAndItsExitBytes(int type, int payloadBytes)
    {
        var raw = Grid((byte)type);
        var file = Fixture([0, 0, 0, 0], [], [raw]);
        var result = RetailPathDataReader.Read(file, new(16, 16));
        Assert.True(result.Success, result.Error?.ToString());
        var node = Assert.Single(Assert.Single(result.Data!.Sectors).Nodes);
        Assert.Equal(type, node.Type);
        Assert.Equal(payloadBytes, node.Payload.Length);
        Assert.Equal((type & 1) == 1 ? 512 : 0, node.ExitMasks.Length);
        Assert.Equal(raw, node.RawBytes.ToArray());
        Assert.Equal(file, result.Data.RawFile.ToArray());
        Assert.Equal(file.Length, result.Data.ParsedBytes);
    }

    [Fact]
    public void PreservesSparseEncodingAndDuplicateCoordinatesOnDifferentHeights()
    {
        var blob = Blob(Record(-200, 1, 1), Record(300, 1, 1));
        var result = Read(Fixture(blob, [], [Sparse(4, 2)]));
        var records = Assert.Single(Assert.Single(result.Sectors).Nodes).SparseRecords;
        Assert.Equal(2, result.UniqueSparseRecordCount);
        Assert.Equal(new[] { -200, 300 }, records.Select(r => r.HeightCentimetres));
        Assert.All(records, r => { Assert.Equal(1, r.CellX); Assert.Equal(1, r.CellY); });
        Assert.Equal(blob, result.SparseBlob.ToArray());
    }

    [Fact]
    public void DecodesHeightTagsWithoutProjectingTerrainAndUsesNativeIntegerWrap()
    {
        Assert.Equal(-12345, Height(Grid(0, p => I32(p, 0, -12345)), 0).Centimetres);
        Assert.Equal(RetailPathHeightKind.Terrain, Height(Grid(2), 0).Kind);
        Assert.Equal(RetailPathHeightKind.NoGround, Height(Grid(4, p => I32(p, 4, int.MaxValue)), 1).Kind);
        Assert.Equal(RetailPathHeightKind.NoGround, Height(Grid(6, p => p[0] = 2), 1).Kind);
        Assert.Equal(RetailPathHeightKind.Terrain, Height(Grid(6, p => p[0] = 2), 0).Kind);
        var two = Grid(8, p => { I32(p, 0, -50); I32(p, 4, 600); p[8] = 0b_11_10_01_00; });
        Assert.Equal(-50, Height(two, 0).Centimetres);
        Assert.Equal(600, Height(two, 1).Centimetres);
        Assert.Equal(RetailPathHeightKind.Terrain, Height(two, 2).Kind);
        Assert.Equal(RetailPathHeightKind.NoGround, Height(two, 3).Kind);
        var four = Grid(10, p => p[56] = 0xfe);
        Assert.Equal(RetailPathHeightKind.Terrain, Height(four, 0).Kind);
        Assert.Equal(RetailPathHeightKind.NoGround, Height(four, 1).Kind);
        var u8 = Grid(12, p => { I32(p, 0, int.MaxValue); p[4] = 1; p[5] = 254; p[6] = 255; });
        Assert.Equal(int.MinValue, Height(u8, 0).Centimetres);
        Assert.Equal(RetailPathHeightKind.Terrain, Height(u8, 1).Kind);
        Assert.Equal(RetailPathHeightKind.NoGround, Height(u8, 2).Kind);
        var u16 = Grid(14, p => { I32(p, 0, int.MaxValue); U16(p, 4, 1); U16(p, 6, 65534); U16(p, 8, 65535); });
        Assert.Equal(int.MinValue, Height(u16, 0).Centimetres);
        Assert.Equal(RetailPathHeightKind.Terrain, Height(u16, 1).Kind);
        Assert.Equal(RetailPathHeightKind.NoGround, Height(u16, 2).Kind);
    }

    [Fact]
    public void RetainsOddExitNibblesAndSerializedLayerOrder()
    {
        var odd = Grid(1); odd[22] = 0xa5;
        var data = Read(Fixture([0, 0, 0, 0], [], [odd, Grid(0)]));
        var sector = Assert.Single(data.Sectors);
        Assert.Equal(1, sector.Nodes[0].Layer);
        Assert.Equal(0, sector.Nodes[1].Layer);
        Assert.Same(sector.Nodes[0], sector.GetLayer(1));
        Assert.Equal(5, sector.Nodes[0].GetExitMask(0));
        Assert.Equal(10, sector.Nodes[0].GetExitMask(1));
    }

    [Fact]
    public void TakesOwnedBytesAndReadOnlyCollections()
    {
        var file = Fixture([0, 0, 0, 0], [], [Grid(0, p => I32(p, 0, -500))]);
        var data = Read(file);
        file[0] = 99; file[^1] = 99;
        Assert.Equal(0, data.GuidBytes.Span[0]);
        Assert.Equal(-500, data.Sectors[0].Nodes[0].GetHeight(0).Centimetres);
        Assert.Throws<NotSupportedException>(() => ((IList<RetailPathSector>)data.Sectors).Clear());
    }

    [Fact]
    public void UsesExplicitRectangularDimensionsAndKeepsEmptySectors()
    {
        var file = Fixture([0, 0, 0, 0], [], [Grid(0)], []);
        var result = RetailPathDataReader.Read(file, new(17, 16));
        Assert.True(result.Success, result.Error?.ToString());
        Assert.Equal(2, result.Data!.SectorsX);
        Assert.Equal(1, result.Data.SectorsY);
        Assert.Empty(result.Data.GetSector(1, 0).Nodes);
        Assert.Equal(RetailPathErrorCode.Truncated, RetailPathDataReader.Read(file, new(32, 32)).Error!.Code);
    }

    [Fact]
    public void AcceptsDirectedOneWayAndCrossLayerLinksWithoutAddingReverseEdges()
    {
        var links = new uint[32]; links[0] = 0x81;
        var left = Grid(0, mode: 14, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var upper = Grid(0, p => I32(p, 0, 100));
        var lower = Grid(0, p => I32(p, 0, 200));
        var data = Read(Fixture([0, 0, 0, 0], links, [left], [upper, lower]), 32);
        Assert.Equal(0x81u, data.GetLinkTableEntry(0));
        Assert.Equal(100, data.GetSector(1, 0).GetLayer(1)!.GetHeight(0).Centimetres);
        Assert.Equal(uint.MaxValue, data.GetSector(1, 0).GetLayer(1)!.Edges[2]);
        Assert.True(data.ReferencesValidated);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(5)] [InlineData(7)]
    [InlineData(9)] [InlineData(11)] [InlineData(13)] [InlineData(15)]
    public void ValidatesOddBoundaryCrossingsWithoutUsingTheLocalExitNibble(int type)
    {
        var links = new uint[32]; links[0] = 0x81;
        var portal = Grid((byte)type, mode: 14, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var upper = Grid(0, p => I32(p, 0, 100));
        var lower = Grid(0, p => I32(p, 0, 200));
        var data = Read(Fixture([0, 0, 0, 0], links, [portal], [upper, lower]), 32);
        Assert.Equal(0, data.GetSector(0, 0).Nodes[0].GetExitMask(31));
        Assert.True(data.ReferencesValidated);
        Assert.Equal(100, data.GetSector(1, 0).GetLayer(1)!.GetHeight(0).Centimetres);
        Assert.Equal(uint.MaxValue, data.GetSector(1, 0).GetLayer(1)!.Edges[2]);

        // The blocking-mask branch also ignores the local nibble and selects layer zero.
        var mask = Grid((byte)type, edges: [uint.MaxValue - 1, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var maskData = Read(Fixture([0, 0, 0, 0], [], [mask], [Grid(0)]), 32);
        Assert.Equal(0, maskData.GetSector(0, 0).Nodes[0].GetExitMask(31));
        Assert.True(maskData.ReferencesValidated);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(5)] [InlineData(7)]
    [InlineData(9)] [InlineData(11)] [InlineData(13)] [InlineData(15)]
    public void RejectsOddBoundaryTargetsHiddenByAZeroLocalExitNibble(int type)
    {
        var portal = Grid((byte)type, mode: 14, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var absentTable = RetailPathDataReader.Read(Fixture([0, 0, 0, 0], [], [portal], [Grid(0)]), new(32, 16));
        Assert.Equal(RetailPathErrorCode.LinkTableOutOfRange, absentTable.Error!.Code);
        Assert.Equal(143, absentTable.Error.FileOffset);

        var links = new uint[32]; links[0] = 0x81; // Missing layer one in the next sector.
        var absentLayer = RetailPathDataReader.Read(Fixture([0, 0, 0, 0], links, [portal], [Grid(0)]), new(32, 16));
        Assert.Equal(RetailPathErrorCode.TargetLayerOutOfRange, absentLayer.Error!.Code);
        Assert.Equal(140, absentLayer.Error.FileOffset);

        var blob = Blob(Record(100, 32, 0));
        links[0] = 5u << 7; // The next sector owns a sparse record at offset four, not five.
        var absentRecord = RetailPathDataReader.Read(Fixture(blob, links, [portal], [Sparse(4, 1)]), new(32, 16));
        Assert.Equal(RetailPathErrorCode.TargetRecordOutOfRange, absentRecord.Error!.Code);
        Assert.Equal(128 + 4 + blob.Length + 4, absentRecord.Error.FileOffset);

        var uncheckedData = RetailPathDataReader.Read(Fixture(blob, links, [portal], [Sparse(4, 1)]),
            new(32, 16) { ValidateReferences = false });
        Assert.True(uncheckedData.Success, uncheckedData.Error?.ToString());
        Assert.False(uncheckedData.Data!.ReferencesValidated);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(5)] [InlineData(7)]
    [InlineData(9)] [InlineData(11)] [InlineData(13)] [InlineData(15)]
    public void KeepsOddBoundaryWorldBoundsBlockingMasksAndZeroPortalSentinels(int type)
    {
        for (int direction = 0; direction < 4; direction++)
        {
            var edges = new uint[] { uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue };
            edges[direction] = uint.MaxValue - 1;
            var outside = Grid((byte)type, edges: edges);
            Error(Fixture([0, 0, 0, 0], [], [outside]), RetailPathErrorCode.TargetSectorOutOfRange, 143 + direction * 4);
        }

        var blocked = Read(Fixture([0, 0, 0, 0], [], [Grid((byte)type)]));
        Assert.True(blocked.ReferencesValidated);
        var zeroPortal = Grid((byte)type, mode: 14, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var closed = Read(Fixture([0, 0, 0, 0], new uint[32], [zeroPortal]));
        Assert.True(closed.ReferencesValidated); // Zero links do not attempt an outside-world target.
    }

    [Fact]
    public void UsesNativeTargetNodeTypeRatherThanHardRejectingNoncanonicalGridSentinel()
    {
        var links = new uint[32]; links[0] = 0x100;
        var left = Grid(0, mode: 14, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var result = RetailPathDataReader.Read(Fixture([0, 0, 0, 0], links, [left], [Grid(0)]), new(32, 16));
        Assert.True(result.Success, result.Error?.ToString());
        Assert.Contains(result.Diagnostics, d => d.Code == "NoncanonicalGridLinkOffset");
        Assert.Equal(0x100u, result.Data!.GetLinkTableEntry(0));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PreservesShortAndFullSparseLinksAndIgnoresLayerBitsWithinSameSector(bool relative)
    {
        // First record is 11 bytes for a relative link, 13 for a full link.
        int second = relative ? 15 : 17;
        var first = Record(100, 1, 1, relative ? (byte)1 : (byte)2,
            relative ? BitConverter.GetBytes((ushort)(second - 4)) : BitConverter.GetBytes(((uint)second << 7) | 127));
        var blob = Blob(first, Record(101, 2, 1));
        var data = Read(Fixture(blob, [], [Sparse(4, 2)]));
        var link = data.Sectors[0].Nodes[0].SparseRecords[0].GetLink(0);
        Assert.Equal((byte)(relative ? 1 : 2), link.Encoding);
        Assert.Equal((uint)second, link.DecodedValue >> 7);
        Assert.Equal((uint)(relative ? 0 : 127), link.DecodedValue & 127);
    }

    [Fact]
    public void ValidatesGridLinksToSparseRecordBoundaries()
    {
        var links = new uint[32]; links[0] = 4u << 7;
        var left = Grid(0, mode: 14, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var file = Fixture(Blob(Record(100, 32, 0)), links, [left], [Sparse(4, 1)]);
        var data = Read(file, 32);
        Assert.Equal(1, data.UniqueSparseRecordCount);
        links[0] = 5u << 7;
        var bad = RetailPathDataReader.Read(Fixture(Blob(Record(100, 32, 0)), links, [left], [Sparse(4, 1)]), new(32, 16));
        Assert.Equal(RetailPathErrorCode.TargetRecordOutOfRange, bad.Error!.Code);
    }

    [Fact]
    public void ReportsGuidMinorReservedAndTrailingCompatibilitySeparately()
    {
        var file = Fixture([0, 0, 0, 0], [], [Grid(0)]).Concat(new byte[] { 9, 8 }).ToArray();
        file[30] = 7;
        U16(file, 16, 6);
        var strict = RetailPathDataReader.Read(file, new(16, 16));
        Assert.Equal(RetailPathErrorCode.UnsupportedVersion, strict.Error!.Code);
        var options = new RetailPathReadOptions(16, 16) { AllowOtherMajor6MinorVersions = true, ExpectedCollisionGuid = Guid.Parse("11111111-1111-1111-1111-111111111111") };
        var result = RetailPathDataReader.Read(file, options);
        Assert.True(result.Success, result.Error?.ToString());
        Assert.Contains(result.Diagnostics, d => d.Code == "MinorVersionMismatch");
        Assert.Contains(result.Diagnostics, d => d.Code == "CollisionGuidMismatch");
        Assert.Contains(result.Diagnostics, d => d.Code == "ReservedHeaderBytes");
        Assert.Equal(new byte[] { 9, 8 }, result.Data!.TrailingBytes.ToArray());
        Assert.Equal(7, result.Data.Header.Span[30]);
        Assert.Equal(RetailPathErrorCode.TrailingBytes, RetailPathDataReader.Read(file, options with { RequireExactEnd = true }).Error!.Code);
        U16(file, 18, 7);
        Assert.Equal(RetailPathErrorCode.UnsupportedVersion, RetailPathDataReader.Read(file, options).Error!.Code);
    }

    [Fact]
    public void RetainsSharedSparseRangesByDefaultAndOffersExplicitStrictPolicy()
    {
        var file = Fixture(Blob(Record(100, 1, 1)), [], [Sparse(4, 1), Sparse(4, 1)]);
        var result = RetailPathDataReader.Read(file, new(16, 16));
        Assert.True(result.Success, result.Error?.ToString());
        Assert.Contains(result.Diagnostics, d => d.Code == "SharedSparseRecord");
        Assert.Equal(1, result.Data!.UniqueSparseRecordCount);
        Assert.Equal(RetailPathErrorCode.SparseRecordOverlap,
            RetailPathDataReader.Read(file, new(16, 16) { RejectOverlappingSparseRecords = true }).Error!.Code);
    }

    [Fact]
    public void EveryTruncationOfACompleteCompressedFileIsAnOffsetBearingFailure()
    {
        var file = Fixture([0, 0, 0, 0], [], [Grid(15)]);
        for (int length = 0; length < file.Length; length++)
        {
            var result = RetailPathDataReader.Read(file.AsSpan(0, length), new(16, 16));
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.InRange(result.Error!.FileOffset, 0, file.Length);
        }
    }

    [Fact]
    public void RejectsInvalidLengthsTypesSignedCountsAndSparseOverruns()
    {
        var file = Fixture([0, 0, 0, 0], [], [Grid(0)]);
        var negative = file.ToArray(); I32(negative, 128, -1);
        Error(negative, RetailPathErrorCode.InvalidLength, 128);
        var negativeLinks = file.ToArray(); I32(negativeLinks, 136, -1);
        Error(negativeLinks, RetailPathErrorCode.InvalidLength, 136);
        var count = file.ToArray(); count[140] = 128;
        Error(count, RetailPathErrorCode.InvalidNodeCount, 140);
        var type = file.ToArray(); type[141] = 17;
        Error(type, RetailPathErrorCode.UnknownNodeType, 141);
        var shortBlob = Blob(Record(1, 1, 1));
        // The error names the node's blob-offset field: header + blob-length + blob
        // + portal-count + sector-count + type. A 13-byte blob puts it at 151.
        int sparseOffsetField = 128 + 4 + shortBlob.Length + 4 + 1 + 1;
        Error(Fixture(shortBlob, [], [Sparse(4, 2)]), RetailPathErrorCode.InvalidSparseRecord, sparseOffsetField);
        var record = Record(1, 1, 1, 2); // A full-link flag without its required four bytes.
        Error(Fixture(Blob(record), [], [Sparse(4, 1)]), RetailPathErrorCode.InvalidSparseRecord, 145);
    }

    [Fact]
    public void EnforcesBudgetsBeforeLargeAllocationsAndBoundsOverflowingTables()
    {
        var file = Fixture([0, 0, 0, 0], [], [Grid(0)]);
        Assert.Equal(RetailPathErrorCode.InvalidOptions, RetailPathDataReader.Read(file, new(0, 16)).Error!.Code);
        Assert.Equal(RetailPathErrorCode.AllocationLimitExceeded, RetailPathDataReader.Read(file, new(int.MaxValue, int.MaxValue)).Error!.Code);
        Assert.Equal(RetailPathErrorCode.InputLimitExceeded, RetailPathDataReader.Read(file, new(16, 16) { MaxInputBytes = 136 }).Error!.Code);
        Assert.Equal(RetailPathErrorCode.AllocationLimitExceeded, RetailPathDataReader.Read(file, new(16, 16) { MaxNodes = 0 }).Error!.Code);
        Assert.Equal(RetailPathErrorCode.AllocationLimitExceeded, RetailPathDataReader.Read(file, new(16, 16) { MaxMetadataBytes = 128 }).Error!.Code);
        var overflow = file.ToArray(); I32(overflow, 136, int.MaxValue);
        Assert.Equal(RetailPathErrorCode.InvalidLength, RetailPathDataReader.Read(overflow, new(16, 16) { MaxLinkTableEntries = int.MaxValue }).Error!.Code);
        Assert.Equal(RetailPathErrorCode.AllocationLimitExceeded,
            RetailPathDataReader.Read(Fixture(Blob(Record(1, 1, 1)), [], [Sparse(4, 1)]), new(16, 16) { MaxSparseRecords = 0 }).Error!.Code);
    }

    [Fact]
    public void RejectsUsedBadEdgeIndicesAndMissingLayersOrWorldSectors()
    {
        var invalidBase = Grid(0, mode: 14, edges: [uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        Error(Fixture([0, 0, 0, 0], [], [invalidBase]), RetailPathErrorCode.LinkTableOutOfRange, 143);
        var links = new uint[32]; links[0] = 0x82;
        var left = Grid(0, mode: 14, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var badLayer = RetailPathDataReader.Read(Fixture([0, 0, 0, 0], links, [left], [Grid(0)]), new(32, 16));
        Assert.Equal(RetailPathErrorCode.TargetLayerOutOfRange, badLayer.Error!.Code);
        var outside = Grid(0, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        Error(Fixture([0, 0, 0, 0], [], [outside]), RetailPathErrorCode.TargetSectorOutOfRange, 143);
    }

    [Fact]
    public void StreamBudgetAndSuppressedDiagnosticsAreVisible()
    {
        var file = Fixture([0, 0, 0, 0], [], [Grid(0)]);
        using var stream = new MemoryStream(file);
        Assert.Equal(RetailPathErrorCode.InputLimitExceeded, RetailPathDataReader.Read(stream, new(16, 16) { MaxInputBytes = 136 }).Error!.Code);
        var result = RetailPathDataReader.Read(file, new(16, 16) { MaxDiagnostics = 0, ValidateReferences = false });
        Assert.True(result.Success, result.Error?.ToString());
        Assert.Empty(result.Diagnostics);
        Assert.True(result.SuppressedDiagnostics > 0);
        Assert.False(result.Data!.ReferencesValidated);
    }

    private static RetailPathHeight Height(byte[] node, int cell) => Read(Fixture([0, 0, 0, 0], [], [node])).Sectors[0].Nodes[0].GetHeight(cell);
    private static RetailPathData Read(byte[] bytes, int width = 16)
    {
        var result = RetailPathDataReader.Read(bytes, new(width, 16));
        Assert.True(result.Success, result.Error?.ToString());
        return result.Data!;
    }
    private static void Error(byte[] bytes, RetailPathErrorCode code, int offset)
    {
        var result = RetailPathDataReader.Read(bytes, new(16, 16));
        Assert.False(result.Success);
        Assert.Equal(code, result.Error!.Code);
        Assert.Equal(offset, result.Error.FileOffset);
        Assert.Null(result.Data);
    }
    private static byte[] Grid(byte type, Action<byte[]>? fill = null, byte mode = 15, uint[]? edges = null)
    {
        int size = (type & ~1) switch { 0 => 4, 2 => 0, 4 => 4096, 6 => 128, 8 => 264, 10 => 568, 12 => 1028, 14 => 2052, _ => throw new ArgumentOutOfRangeException(nameof(type)) };
        var payload = new byte[size]; fill?.Invoke(payload);
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
        w.Write(type); w.Write(mode);
        foreach (uint edge in edges ?? [uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue]) w.Write(edge);
        w.Write(payload);
        if ((type & 1) != 0) w.Write(new byte[512]);
        return ms.ToArray();
    }
    private static byte[] Sparse(int offset, ushort count)
    {
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
        w.Write((byte)16); w.Write(offset); w.Write(count); return ms.ToArray();
    }
    private static byte[] Record(int height, short x, short y, byte flags = 0, byte[]? links = null)
    {
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
        w.Write(height); w.Write(x); w.Write(y); w.Write(flags); if (links is not null) w.Write(links);
        return ms.ToArray();
    }
    private static byte[] Blob(params byte[][] records) => new byte[4].Concat(records.SelectMany(r => r)).ToArray();
    private static byte[] Fixture(byte[] blob, uint[] links, params byte[][][] sectors)
    {
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
        var header = new byte[128]; U16(header, 16, 5); U16(header, 18, 6); w.Write(header);
        w.Write(blob.Length); w.Write(blob); w.Write(links.Length); foreach (uint link in links) w.Write(link);
        foreach (var nodes in sectors) { w.Write((byte)nodes.Length); foreach (var node in nodes) w.Write(node); }
        return ms.ToArray();
    }
    private static void I32(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void U16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
