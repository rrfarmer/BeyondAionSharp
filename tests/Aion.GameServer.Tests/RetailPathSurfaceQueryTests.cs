using System.Buffers.Binary;
using System.Security.Cryptography;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailPathSurfaceQueryTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, true)] [InlineData(3, true)]
    [InlineData(4, false)] [InlineData(5, false)] [InlineData(6, true)] [InlineData(7, true)]
    [InlineData(8, false)] [InlineData(9, false)] [InlineData(10, false)] [InlineData(11, false)]
    [InlineData(12, false)] [InlineData(13, false)] [InlineData(14, false)] [InlineData(15, false)]
    public void EveryGridTypeResolvesItsOwnHeightAndDirectedInteriorIdentity(int type, bool usesTerrain)
    {
        var data = ReadPath(Fixture([0, 0, 0, 0], [], [Grid((byte)type)]));
        var query = new RetailPathSurfaceQuery(data, Terrain(16, 16, 320));
        var candidates = query.EnumerateSurfaceCandidates(1, 1, usesTerrain ? 1000 : 0);
        Assert.Equal(RetailSurfaceQueryStatus.Complete, candidates.Status);
        var candidate = Assert.Single(candidates.Candidates);
        Assert.Equal(RetailSurfaceCandidateStatus.WithinTolerance, candidate.Status);
        Assert.Equal(usesTerrain ? 10f : 0f, candidate.Height.Metres);
        var next = query.GetCardinalNeighbor(candidate.Surface, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Found, next.Status);
        Assert.Same(candidate.Surface, next.Source);
        Assert.Equal(2, next.Target!.CellX); Assert.Equal(1, next.Target.CellY);
        Assert.Same(candidate.Surface.Node, next.Target.Node);
        Assert.Equal(0x80000022u, next.Target.NativeId);
        Assert.Equal(1.25f, next.Target.CentreXMetres);
    }

    [Theory]
    [InlineData(69, true)] [InlineData(70, false)] [InlineData(-69, true)] [InlineData(-70, false)]
    public void EndpointToleranceIsStrictCentimetresForFlatAndSparse(int queryCm, bool matches)
    {
        var data = ReadPath(Fixture(Blob(Record(0, 1, 1)), [], [Grid(0), Sparse(4, 1)]));
        var result = new RetailPathSurfaceQuery(data).EnumerateSurfaceCandidates(1, 1, queryCm);
        Assert.Equal(RetailSurfaceQueryStatus.Complete, result.Status);
        Assert.Equal(new[] { 16, 0 }, result.Candidates.Select(c => (int)c.Surface.Node.Type));
        Assert.All(result.Candidates, c => Assert.Equal(matches ? RetailSurfaceCandidateStatus.WithinTolerance : RetailSurfaceCandidateStatus.OutsideTolerance, c.Status));
        Assert.Equal(new[] { 0, 1 }, result.Candidates.Select(c => c.Surface.Layer));
    }

    [Fact]
    public void SimpleCachedFloatCentimetresDifferFromFlatRawCentimetres()
    {
        var data = ReadPath(Fixture([0, 0, 0, 0], [], [Grid(0, p => I32(p, 0, 53)), Grid(4, p => I32(p, 132, 53))]));
        var result = new RetailPathSurfaceQuery(data).EnumerateSurfaceCandidates(1, 1, 122);
        Assert.Equal(52, result.Candidates[0].Height.ComparisonCentimetres);
        Assert.Equal(RetailSurfaceCandidateStatus.OutsideTolerance, result.Candidates[0].Status);
        Assert.Equal(53, result.Candidates[1].Height.ComparisonCentimetres);
        Assert.Equal(RetailSurfaceCandidateStatus.WithinTolerance, result.Candidates[1].Status);
        Assert.Equal(BitConverter.SingleToInt32Bits(0.53f), BitConverter.SingleToInt32Bits(result.Candidates[0].Height.Metres));
    }

    [Fact]
    public void InteriorTopologyDoesNotInventAnEndpointInt32ConversionGate()
    {
        var query = new RetailPathSurfaceQuery(ReadPath(Fixture([0, 0, 0, 0], [], [Grid(4, p => I32(p, 4, int.MaxValue - 1))])));
        var next = query.GetCardinalNeighbor(query.GetSurface(0, 0, 0).Target!, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Found, next.Status);
        Assert.Equal(RetailSurfaceHeightStatus.Resolved, query.ReadHeight(next.Target!).Status);
        Assert.Null(query.ReadHeight(next.Target!).ComparisonCentimetres);
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, query.EnumerateSurfaceCandidates(1, 0, 0).Status);
    }

    [Fact]
    public void FlatAndSimpleKeepTheirDifferentNoGroundThresholdRules()
    {
        var data = ReadPath(Fixture([0, 0, 0, 0], [], [Grid(0, p => I32(p, 0, -1024000)), Grid(4, p => I32(p, 132, -1024000))]));
        var candidates = new RetailPathSurfaceQuery(data).EnumerateSurfaceCandidates(1, 1, -1024000).Candidates;
        Assert.Equal(RetailSurfaceCandidateStatus.NoGround, candidates[0].Status);
        Assert.Equal(RetailSurfaceCandidateStatus.WithinTolerance, candidates[1].Status);
        Assert.Equal(-10240f, candidates[1].Height.Metres);
    }

    [Fact]
    public void SparseMatchesRetainFileOrderAmbiguityAndUnorderedInputStatus()
    {
        var data = ReadPath(Fixture(Blob(Record(0, 1, 1), Record(100, 1, 1)), [], [Sparse(4, 2)]));
        var query = new RetailPathSurfaceQuery(data);
        var result = query.EnumerateSurfaceCandidates(1, 1, 50);
        Assert.Equal(RetailSurfaceQueryStatus.Ambiguous, result.Status);
        Assert.Equal(new[] { 4, 13 }, result.Candidates.Select(c => c.Surface.SparseRecord!.BlobOffset));
        Assert.All(result.Candidates, c => Assert.Equal(RetailSurfaceCandidateStatus.WithinTolerance, c.Status));
        Assert.Equal(RetailSurfaceQueryStatus.Complete, query.EnumerateSurfaceCandidates(1, 1, -69).Status);
        var unsorted = ReadPath(Fixture(Blob(Record(0, 2, 1), Record(0, 1, 1)), [], [Sparse(4, 2)]));
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, new RetailPathSurfaceQuery(unsorted).EnumerateSurfaceCandidates(1, 1, 0).Status);
    }

    [Fact]
    public void ExplicitBudgetsRetainCandidatePrefixAndLeaveGridQueriesAvailable()
    {
        var data = ReadPath(Fixture(Blob(Record(0, 1, 1)), [], [Grid(0), Sparse(4, 1)]));
        var query = new RetailPathSurfaceQuery(data, options: new() { MaxIndexedSparseRecords = 0, MaxCandidates = 1 });
        Assert.False(query.SparseIndexAvailable);
        var result = query.EnumerateSurfaceCandidates(1, 1, 0);
        Assert.Equal(RetailSurfaceQueryStatus.ResourceLimit, result.Status); Assert.Single(result.Candidates);
        Assert.Equal(RetailSurfaceQueryStatus.ResourceLimit, query.GetSurface(1, 1, 0, 4).Status);
        Assert.Equal(RetailSurfaceQueryStatus.Found, query.GetSurface(1, 1, 1).Status);
    }

    [Fact]
    public void WorldQuantizationRejectsNonfiniteOutsideAndSignedNarrowingDomains()
    {
        var query = new RetailPathSurfaceQuery(ReadPath(Fixture([0, 0, 0, 0], [], [Grid(0)])));
        var result = query.EnumerateSurfaceCandidates(0.999f, 0.749f, 0.699f);
        Assert.Equal(1, result.CellX); Assert.Equal(1, result.CellY); Assert.Equal(69, result.QueryCentimetres);
        Assert.Equal(RetailSurfaceCandidateStatus.WithinTolerance, Assert.Single(result.Candidates).Status);
        Assert.Equal(RetailSurfaceQueryStatus.OutsideWorld, query.EnumerateSurfaceCandidates(-0.001f, 0f, 0f).Status);
        Assert.Equal(RetailSurfaceQueryStatus.OutsideWorld, query.EnumerateSurfaceCandidates(16f, 0f, 0f).Status);
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, query.EnumerateSurfaceCandidates(float.NaN, 0f, 0f).Status);
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, query.EnumerateSurfaceCandidates(0f, 0f, float.MaxValue).Status);
        // An explicitly huge world can be structurally represented with empty sectors, but short cell wrapping is unsupported.
        var sectors = Enumerable.Range(0, 1025).Select(_ => Array.Empty<byte[]>()).ToArray();
        var wide = new RetailPathSurfaceQuery(ReadPath(Fixture([0, 0, 0, 0], [], sectors), 16400));
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, wide.EnumerateSurfaceCandidates(16384f, 0f, 0f).Status);
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, wide.EnumerateSurfaceCandidates(32768, 0, 0).Status);
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, wide.GetSurface(32768, 0, 0).Status);
    }

    [Fact]
    public void InvalidOwnershipDirectionsAndArithmeticAreExplicitResults()
    {
        byte[] bytes = Fixture([0, 0, 0, 0], [], [Grid(0)]);
        var query = new RetailPathSurfaceQuery(ReadPath(bytes));
        var other = new RetailPathSurfaceQuery(ReadPath(bytes));
        var source = other.GetSurface(1, 1, 0).Target!;
        Assert.Equal(RetailSurfaceQueryStatus.InvalidIdentity, query.GetCardinalNeighbor(source, 0).Status);
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, other.GetCardinalNeighbor(source, 4).Status);
        Assert.Equal(RetailSurfaceQueryStatus.InvalidIdentity, query.GetSurface(1, 1, 1).Status);
        Assert.Equal(RetailSurfaceQueryStatus.InvalidIdentity, query.GetSurface(1, 1, 0, 4).Status);
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, query.EnumerateSurfaceCandidates(1, 1, int.MinValue).Status);
        var sparse = new RetailPathSurfaceQuery(ReadPath(Fixture(Blob(Record(int.MinValue, 1, 1)), [], [Sparse(4, 1)])));
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, sparse.EnumerateSurfaceCandidates(1, 1, int.MinValue).Status);
    }

    [Theory]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)]
    public void SimpleNoGroundBlocksInteriorButBoundaryTargetRemainsRawData(int type)
    {
        var hole = Grid((byte)type, p => SetNoGround((byte)type, p));
        var data = ReadPath(Fixture([0, 0, 0, 0], [], [hole]));
        var query = new RetailPathSurfaceQuery(data);
        Assert.Equal(RetailSurfaceCandidateStatus.NoGround, Assert.Single(query.EnumerateSurfaceCandidates(0, 0, 0).Candidates).Status);
        var source = query.GetSurface(1, 0, 0).Target!;
        Assert.Equal(RetailSurfaceQueryStatus.NoLink, query.GetCardinalNeighbor(source, 2).Status);
        var boundary = Grid(1, exit: 0, edges: [0xfffffffe, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var across = new RetailPathSurfaceQuery(ReadPath(Fixture([0, 0, 0, 0], [], [boundary], [hole]), 32));
        var next = across.GetCardinalNeighbor(across.GetSurface(31, 0, 0).Target!, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Found, next.Status);
        Assert.Equal(RetailSurfaceHeightStatus.NoGround, across.ReadHeight(next.Target!).Status);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(5)] [InlineData(7)]
    [InlineData(9)] [InlineData(11)] [InlineData(13)] [InlineData(15)]
    public void EveryOddFamilyBypassesZeroExitNibbleOnlyOnAllFourBoundaries(int type)
    {
        for (int direction = 0; direction < 4; direction++)
        {
            int x = direction == 0 ? 31 : direction == 2 ? 32 : 5;
            int y = direction == 1 ? 31 : direction == 3 ? 32 : 5;
            var edges = new uint[] { uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue };
            edges[direction] &= ~(1u << 5);
            byte[][][] sectors = [[Grid(0)], [Grid(0)], [Grid(0)], [Grid(0)]];
            sectors[(y / 32) * 2 + x / 32] = [Grid((byte)type, exit: 0, edges: edges)];
            var query = new RetailPathSurfaceQuery(ReadPath(Fixture([0, 0, 0, 0], [], sectors), 32, 32));
            var source = query.GetSurface(x, y, 0).Target!;
            var next = query.GetCardinalNeighbor(source, direction);
            Assert.Equal(RetailSurfaceQueryStatus.Found, next.Status);
            Assert.Equal(RetailPathLinkKind.EdgeMask, next.Provenance.Kind);
            Assert.Equal(0, next.Target!.Layer);
            Assert.Equal(RetailSurfaceQueryStatus.NoLink, query.GetCardinalNeighbor(next.Target, (direction + 2) % 4).Status);
            int insideX = x + (direction == 0 ? -1 : direction == 2 ? 1 : 0);
            int insideY = y + (direction == 1 ? -1 : direction == 3 ? 1 : 0);
            Assert.Equal(RetailSurfaceQueryStatus.NoLink, query.GetCardinalNeighbor(query.GetSurface(insideX, insideY, 0).Target!, direction).Status);
        }
    }

    [Fact]
    public void PortalRetainsTargetLayerNoncanonicalGridOffsetAndZeroSentinel()
    {
        uint link = (123u << 7) | 1;
        var links = new uint[32]; links[0] = link;
        var sourceNode = Grid(1, exit: 0, mode: 14, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var query = new RetailPathSurfaceQuery(ReadPath(Fixture([0, 0, 0, 0], links, [sourceNode],
            [Grid(0, p => I32(p, 0, 200)), Grid(0, p => I32(p, 0, 300))]), 32));
        var next = query.GetCardinalNeighbor(query.GetSurface(31, 0, 0).Target!, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Found, next.Status);
        Assert.Equal(link, next.Provenance.DecodedValue); Assert.Equal(RetailPathLinkKind.EdgePortal, next.Provenance.Kind);
        Assert.Equal(1, next.Target!.Layer); Assert.True(next.Target.IsGridIdentity);
        Assert.Equal(2f, query.ReadHeight(next.Target).Metres);
        Assert.Equal(RetailSurfaceQueryStatus.NoLink, query.GetCardinalNeighbor(query.GetSurface(31, 1, 0).Target!, 0).Status);
    }

    [Fact]
    public void MalformedEdgesAreCheckedEvenWhenReaderReferenceValidationWasDisabled()
    {
        var source = Grid(0, mode: 14, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var badIndex = new RetailPathSurfaceQuery(ReadPath(Fixture([0, 0, 0, 0], [], [source], [Grid(0)]), 32));
        Assert.Equal(RetailSurfaceQueryStatus.Malformed, badIndex.GetCardinalNeighbor(badIndex.GetSurface(31, 0, 0).Target!, 0).Status);
        var links = new uint[32]; links[0] = 0x82;
        var badLayer = new RetailPathSurfaceQuery(ReadPath(Fixture([0, 0, 0, 0], links, [source], [Grid(0)]), 32));
        Assert.Equal(RetailSurfaceQueryStatus.Malformed, badLayer.GetCardinalNeighbor(badLayer.GetSurface(31, 0, 0).Target!, 0).Status);
        links[0] = 0x80;
        var badRecord = new RetailPathSurfaceQuery(ReadPath(Fixture([0, 0, 0, 0], links, [source], [Sparse(4, 0)]), 32));
        Assert.Equal(RetailSurfaceQueryStatus.Malformed, badRecord.GetCardinalNeighbor(badRecord.GetSurface(31, 0, 0).Target!, 0).Status);
        var outside = new RetailPathSurfaceQuery(ReadPath(Fixture([0, 0, 0, 0], [], [Grid(0, edges: [0xfffffffe, uint.MaxValue, uint.MaxValue, uint.MaxValue])])));
        Assert.Equal(RetailSurfaceQueryStatus.OutsideWorld, outside.GetCardinalNeighbor(outside.GetSurface(31, 0, 0).Target!, 0).Status);
    }

    [Fact]
    public void MaskToSparseLayerRetainsNativeGridIdentityAsExplicitlyUnsupported()
    {
        var source = Grid(0, edges: [0xfffffffe, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var query = new RetailPathSurfaceQuery(ReadPath(Fixture(Blob(Record(0, 32, 0)), [], [source], [Sparse(4, 1)]), 32));
        var result = query.GetCardinalNeighbor(query.GetSurface(31, 0, 0).Target!, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Unsupported, result.Status);
        Assert.True(result.Target!.IsGridIdentity); Assert.Equal(16, result.Target.Node.Type);
        Assert.Equal(RetailSurfaceHeightStatus.Unsupported, query.ReadHeight(result.Target).Status);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SparseLinksKeepPointerIdentitySameSectorLayerBitsAndDirection(bool shortEncoding)
    {
        byte[] linkBytes = shortEncoding ? U16Bytes(11) : U32Bytes((17u << 7) | 77);
        // Relative source occupies 11 bytes, so its next record is at 15, not 17.
        int targetOffset = shortEncoding ? 15 : 17;
        var blob = Blob(Record(0, 1, 1, (byte)(shortEncoding ? 1 : 2), linkBytes), Record(3000, 2, 1));
        var query = new RetailPathSurfaceQuery(ReadPath(Fixture(blob, [], [Sparse(4, 2)])));
        var source = query.GetSurface(1, 1, 0, 4).Target!;
        var next = query.GetCardinalNeighbor(source, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Found, next.Status);
        Assert.Equal(targetOffset, next.Target!.SparseRecord!.BlobOffset); Assert.Equal(0, next.Target.Layer);
        Assert.Equal(30f, query.ReadHeight(next.Target).Metres); // no invented max step/climb filter
        Assert.Equal(RetailSurfaceQueryStatus.NoLink, query.GetCardinalNeighbor(next.Target, 2).Status);
        Assert.Equal(shortEncoding ? RetailPathLinkKind.SparseRelative : RetailPathLinkKind.SparseFull, next.Provenance.Kind);
    }

    [Fact]
    public void ShortLinkRebasesAgainstTheActualConsumerNodeEvenForSharedRecordPointers()
    {
        var blob = Blob(Record(0, 1, 1, 2, U32Bytes(17u << 7)), Record(0, 2, 1, 1, U16Bytes(0)));
        var data = ReadPath(Fixture(blob, [], [Sparse(4, 2), Sparse(17, 1)]));
        var query = new RetailPathSurfaceQuery(data);
        var consumer17 = query.GetCardinalNeighbor(query.GetSurface(1, 1, 0, 17).Target!, 0);
        var consumer4 = query.GetCardinalNeighbor(query.GetSurface(1, 1, 1, 17).Target!, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Found, consumer17.Status); Assert.Equal(17, consumer17.Target!.SparseRecord!.BlobOffset);
        Assert.Equal(RetailSurfaceQueryStatus.Found, consumer4.Status); Assert.Equal(4, consumer4.Target!.SparseRecord!.BlobOffset);
        Assert.NotNull(consumer4.Detail); // target raw XY differs; the native adjacent cell and pointer are both retained
    }

    [Fact]
    public void ShortLinkEncodingUsesUint32WrapAndZeroRemainsNoLink()
    {
        // One bounded 32 MiB blob exercises the native shift wrapping to zero without fabricated model internals.
        const int nodeBase = (1 << 25) - 2;
        var blob = new byte[1 << 25]; Record(0, 1, 1, 1, U16Bytes(2)).CopyTo(blob, 4);
        var query = new RetailPathSurfaceQuery(ReadPath(Fixture(blob, [], [Sparse(4, 1), Sparse(nodeBase, 0)])));
        var result = query.GetCardinalNeighbor(query.GetSurface(1, 1, 0, 4).Target!, 0);
        Assert.Equal(RetailSurfaceQueryStatus.NoLink, result.Status);
        Assert.Equal(2u, result.Provenance.StoredValue); Assert.Equal(0u, result.Provenance.DecodedValue);
    }

    [Fact]
    public void SparseCrossingSelectsActualTargetTypeAndLayerWithoutOffsetOneConvention()
    {
        var blob = Blob(Record(0, 31, 0, 2, U32Bytes((9u << 7) | 1)));
        var query = new RetailPathSurfaceQuery(ReadPath(Fixture(blob, [], [Sparse(4, 1)], [Grid(0), Grid(0)]), 32));
        var next = query.GetCardinalNeighbor(query.GetSurface(31, 0, 0, 4).Target!, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Found, next.Status); Assert.True(next.Target!.IsGridIdentity);
        Assert.Equal(1, next.Target.Layer); Assert.Equal(32, next.Target.CellX);
        var invalid = new RetailPathSurfaceQuery(ReadPath(Fixture(Blob(Record(0, 1, 1, 2, U32Bytes(uint.MaxValue))), [], [Sparse(4, 1)])));
        var malformed = invalid.GetCardinalNeighbor(invalid.GetSurface(1, 1, 0, 4).Target!, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Malformed, malformed.Status); Assert.Equal(uint.MaxValue, malformed.Provenance.StoredValue);
    }

    [Theory]
    [InlineData(0)] [InlineData(3)]
    public void BothAbsentSparseEncodingsRemainNoLinkInEveryDirection(int encoding)
    {
        for (int direction = 0; direction < 4; direction++)
        {
            var query = new RetailPathSurfaceQuery(ReadPath(Fixture(Blob(Record(0, 1, 1, (byte)(encoding << (direction * 2)))), [], [Sparse(4, 1)])));
            var result = query.GetCardinalNeighbor(query.GetSurface(1, 1, 0, 4).Target!, direction);
            Assert.Equal(RetailSurfaceQueryStatus.NoLink, result.Status);
            Assert.Equal(RetailPathLinkKind.SparseAbsent, result.Provenance.Kind);
        }
        var zero = new RetailPathSurfaceQuery(ReadPath(Fixture(Blob(Record(0, 1, 1, 2, U32Bytes(0))), [], [Sparse(4, 1)])));
        Assert.Equal(RetailSurfaceQueryStatus.NoLink, zero.GetCardinalNeighbor(zero.GetSurface(1, 1, 0, 4).Target!, 0).Status);
    }

    [Fact]
    public void BothSparseAndGridPortalCrossingsCanNameTheExactSparseRecord()
    {
        var blob = Blob(Record(0, 31, 0, 2, U32Bytes((17u << 7) | 1)), Record(123456, 32, 0));
        var query = new RetailPathSurfaceQuery(ReadPath(Fixture(blob, [], [Sparse(4, 1)], [Sparse(17, 1), Grid(0)]), 32));
        var result = query.GetCardinalNeighbor(query.GetSurface(31, 0, 0, 4).Target!, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Found, result.Status);
        Assert.Equal(1, result.Target!.Layer); Assert.Equal(17, result.Target.SparseRecord!.BlobOffset);
        Assert.Equal(123456, query.ReadHeight(result.Target).ComparisonCentimetres);
        var links = new uint[32]; links[0] = 4u << 7;
        var grid = Grid(1, exit: 0, mode: 14, edges: [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var portal = new RetailPathSurfaceQuery(ReadPath(Fixture(Blob(Record(1101, 32, 0)), links, [grid], [Sparse(4, 1)]), 32));
        var next = portal.GetCardinalNeighbor(portal.GetSurface(31, 0, 0).Target!, 0);
        Assert.Equal(RetailSurfaceQueryStatus.Found, next.Status); Assert.Equal(4, next.Target!.SparseRecord!.BlobOffset);
        Assert.Equal(RetailSurfaceQueryStatus.NoLink, portal.GetCardinalNeighbor(next.Target, 2).Status);
    }

    [Fact]
    public void TerrainIdentityMismatchesCannotSilentlyProjectAnotherMap()
    {
        var data = ReadPath(Fixture([0, 0, 0, 0], [], [Grid(2)]));
        var noTerrain = new RetailPathSurfaceQuery(data);
        Assert.Equal(RetailSurfaceQueryStatus.TerrainUnavailable, noTerrain.EnumerateSurfaceCandidates(1, 1, 0).Status);
        // Terrain-only topology has no local destination-height gate; its raw neighbor is still exposed.
        Assert.Equal(RetailSurfaceQueryStatus.Found, noTerrain.GetCardinalNeighbor(noTerrain.GetSurface(1, 1, 0).Target!, 0).Status);
        var wrongSize = new RetailPathSurfaceQuery(data, Terrain(32, 16, 0));
        Assert.NotEmpty(wrongSize.CompatibilityDiagnostics);
        Assert.Equal(RetailSurfaceQueryStatus.TerrainUnavailable, wrongSize.EnumerateSurfaceCandidates(1, 1, 0).Status);
        var wrongGuid = RetailHeightMap40Reader.Read(RetailHeightMap40ReaderTests.Fixture(8, 8, new ushort[81], true,
            Guid.Parse("00000000-0000-0000-0000-000000000001"))).Data!;
        Assert.Equal(RetailSurfaceQueryStatus.TerrainUnavailable, new RetailPathSurfaceQuery(data, wrongGuid).EnumerateSurfaceCandidates(1, 1, 0).Status);
        var headerless = RetailHeightMap40Reader.Read(RetailHeightMap40ReaderTests.Fixture(8, 8, new ushort[81])).Data!;
        Assert.Equal(RetailSurfaceQueryStatus.TerrainUnavailable, new RetailPathSurfaceQuery(data, headerless).EnumerateSurfaceCandidates(1, 1, 0).Status);
        Assert.Equal(RetailSurfaceQueryStatus.Complete, new RetailPathSurfaceQuery(data, headerless, new() { AllowHeaderlessTerrain = true }).EnumerateSurfaceCandidates(1, 1, 0).Status);
    }

    [SkippableFact]
    public void HashBoundSkyTempleHasTwoPathFloorsFarAboveTheMatchedTerrain()
    {
        string directory = Environment.GetEnvironmentVariable("AION_RETAIL_SURFACE_PILOT_DIR")
            ?? @"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying";
        string pathName = System.IO.Path.Combine(directory, "IDDF2Flying-path.dat");
        string terrainName = System.IO.Path.Combine(directory, "HeightMap40.dat");
        Skip.IfNot(File.Exists(pathName) && File.Exists(terrainName), "Matched installed Sky Temple path/HeightMap40 inputs are absent.");
        byte[] pathBytes = File.ReadAllBytes(pathName), terrainBytes = File.ReadAllBytes(terrainName);
        Assert.Equal("e59d0a7df72c55edb4aa6575b38d8a5c9878eefd9a960b6fd25458d21e069be5", Sha(pathBytes));
        Assert.Equal("a6bd0f7a3f0d73b869b4e98c66146b1caf523e709bcb158dcc66ab6a9904d674", Sha(terrainBytes));
        var pathResult = RetailPathDataReader.Read(pathBytes, new(512, 512) { RequireExactEnd = true });
        var terrainResult = RetailHeightMap40Reader.Read(terrainBytes, new()
        { ExpectedGuid = Guid.Parse("ff47ba3c-ac6d-4fbf-a6b1-a7d0945a7045"), ExpectedWorldWidthMetres = 512, ExpectedWorldHeightMetres = 512, RequireExactEnd = true });
        Assert.True(pathResult.Success, pathResult.Error?.ToString());
        Assert.True(terrainResult.Success, terrainResult.Error?.ToString()); Assert.Empty(terrainResult.Diagnostics);
        var query = new RetailPathSurfaceQuery(pathResult.Data!, terrainResult.Data!);
        Assert.Empty(query.CompatibilityDiagnostics);
        var lower = query.EnumerateSurfaceCandidates(95, 267, 57526);
        Assert.Contains(lower.Candidates, c => c.Surface.Layer == 0 && c.Surface.Node.Type == 15 && c.Height.Metres == 575.26f && c.Status == RetailSurfaceCandidateStatus.WithinTolerance);
        var upper = query.EnumerateSurfaceCandidates(95, 267, 58275);
        Assert.Contains(upper.Candidates, c => c.Surface.Layer == 1 && c.Surface.Node.Type == 16 && c.Height.Metres == 582.75f && c.Status == RetailSurfaceCandidateStatus.WithinTolerance);
        Assert.Equal(43.8125f, terrainResult.Data!.SampleTerrain(23, 66, 47.75f, 133.75f).HeightMetres);
        // File-backed data access only: no native lookup, route, actor collision or gameplay is exercised.
    }

    private static RetailHeightMap40Data Terrain(int width, int height, ushort word) => RetailHeightMap40Reader.Read(
        RetailHeightMap40ReaderTests.Fixture(width / 2, height / 2, Enumerable.Repeat(word, (width / 2 + 1) * (height / 2 + 1)).ToArray(), true)).Data!;
    private static RetailPathData ReadPath(byte[] bytes, int width = 16, int height = 16)
    {
        var result = RetailPathDataReader.Read(bytes, new(width, height) { ValidateReferences = false });
        Assert.True(result.Success, result.Error?.ToString()); return result.Data!;
    }
    private static void SetNoGround(byte type, byte[] p)
    {
        switch (type & ~1)
        {
            case 4: I32(p, 0, int.MaxValue); break;
            case 6: p[0] = 1; break;
            case 8: p[8] = 3; break;
            case 10: p[56] = 15; break;
            case 12: p[4] = 255; break;
            case 14: BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(4), ushort.MaxValue); break;
        }
    }
    private static byte[] Grid(byte type, Action<byte[]>? fill = null, byte mode = 15, uint[]? edges = null, byte exit = 15)
    {
        int size = (type & ~1) switch { 0 => 4, 2 => 0, 4 => 4096, 6 => 128, 8 => 264, 10 => 568, 12 => 1028, 14 => 2052, _ => throw new ArgumentOutOfRangeException(nameof(type)) };
        var payload = new byte[size]; fill?.Invoke(payload);
        using var ms = new MemoryStream(); using var writer = new BinaryWriter(ms);
        writer.Write(type); writer.Write(mode);
        foreach (uint edge in edges ?? [uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue]) writer.Write(edge);
        writer.Write(payload);
        if ((type & 1) != 0) writer.Write(Enumerable.Repeat((byte)(exit | exit << 4), 512).ToArray());
        return ms.ToArray();
    }
    private static byte[] Sparse(int offset, ushort count)
    {
        using var ms = new MemoryStream(); using var writer = new BinaryWriter(ms);
        writer.Write((byte)16); writer.Write(offset); writer.Write(count); return ms.ToArray();
    }
    private static byte[] Record(int height, short x, short y, byte flags = 0, byte[]? links = null)
    {
        using var ms = new MemoryStream(); using var writer = new BinaryWriter(ms);
        writer.Write(height); writer.Write(x); writer.Write(y); writer.Write(flags); if (links is not null) writer.Write(links);
        return ms.ToArray();
    }
    private static byte[] Blob(params byte[][] records) => new byte[4].Concat(records.SelectMany(r => r)).ToArray();
    private static byte[] U16Bytes(ushort value) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, value); return b; }
    private static byte[] U32Bytes(uint value) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, value); return b; }
    private static void I32(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static byte[] Fixture(byte[] blob, uint[] links, params byte[][][] sectors)
    {
        using var ms = new MemoryStream(); using var writer = new BinaryWriter(ms);
        var header = new byte[128]; BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(16), 5); BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(18), 6);
        writer.Write(header); writer.Write(blob.Length); writer.Write(blob); writer.Write(links.Length); foreach (uint link in links) writer.Write(link);
        foreach (var nodes in sectors) { writer.Write((byte)nodes.Length); foreach (var node in nodes) writer.Write(node); }
        return ms.ToArray();
    }
}
