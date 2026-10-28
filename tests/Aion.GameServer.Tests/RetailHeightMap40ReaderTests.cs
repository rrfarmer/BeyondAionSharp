using System.Buffers.Binary;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailHeightMap40ReaderTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RetainsOwnedRectangularLayoutHeaderGuidAndEveryRawPlane(bool headered)
    {
        var guid = Guid.Parse("ff47ba3c-ac6d-4fbf-a6b1-a7d0945a7045");
        var file = Fixture(2, 1, [0, 33, 66, 99, 128, 161], headered, guid);
        var result = RetailHeightMap40Reader.Read(file);
        Assert.True(result.Success, result.Error?.ToString());
        var data = result.Data!;
        Assert.Equal(2, data.WidthIntervals); Assert.Equal(1, data.HeightIntervals);
        Assert.Equal(4, data.WorldWidthMetres); Assert.Equal(2, data.WorldHeightMetres);
        Assert.Equal(3, data.Stride); Assert.Equal(6, data.SampleCount);
        Assert.Equal(headered ? 128 : 0, data.HeaderBytes);
        Assert.Equal(headered ? guid : (Guid?)null, data.BuildGuid);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, data.BytePlane1.ToArray());
        Assert.Equal(new byte[] { 21, 22, 23, 24, 25, 26 }, data.BytePlane2.ToArray());
        Assert.Equal(161, data.GetRawWord(2, 1));
        Assert.Equal(file, data.RawFile.ToArray());
        file[data.HeightWordsFileOffset] = 255;
        Assert.Equal(0, data.GetRawWord(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => data.GetRawWord(3, 1));
    }

    [Theory]
    [InlineData(0, 0, 0)] [InlineData(2, 0, 1)] [InlineData(0, 2, 2)] [InlineData(2, 2, 8)]
    [InlineData(1, 1, 2.75f)]
    public void SamplesEveryCornerAndBilinearInteriorUsingMaskedRawWords(float x, float y, float expected)
    {
        var data = RetailHeightMap40Reader.Read(Fixture(1, 1, [3, 33, 66, 259])).Data!;
        var result = data.SampleTerrain(0, 0, x, y);
        Assert.Equal(RetailTerrainSampleStatus.Sampled, result.Status);
        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(result.HeightMetres));
        Assert.Equal(3, result.TopLeft); Assert.Equal(33, result.TopRight);
        Assert.Equal(66, result.BottomLeft); Assert.Equal(259, result.BottomRight);
        Assert.Equal(data.HeightWordsFileOffset, result.TopLeftFileOffset);
        Assert.Equal(data.HeightWordsFileOffset + 2, result.TopRightFileOffset);
        Assert.Equal(data.HeightWordsFileOffset + 4, result.BottomLeftFileOffset);
        Assert.Equal(data.HeightWordsFileOffset + 6, result.BottomRightFileOffset);
    }

    [Fact]
    public void NonplanarQuadUsesBilinearRatherThanTriangleDiagonalSupport()
    {
        var data = RetailHeightMap40Reader.Read(Fixture(1, 1, [0, 0, 0, 256])).Data!;
        Assert.Equal(2f, data.SampleTerrain(0, 0, 1, 1).HeightMetres);
        // The triangle through the other three zero corners would have height zero at this diagonal point.
        Assert.NotEqual(0f, data.SampleTerrain(0, 0, 1, 1).HeightMetres);
    }

    [Fact]
    public void RectangularStrideSelectsTheSecondQuadAndRejectsExtrapolation()
    {
        var data = RetailHeightMap40Reader.Read(Fixture(2, 1, [0, 32, 64, 96, 128, 160])).Data!;
        var sample = data.SampleTerrain(1, 0, 3, 1);
        Assert.Equal(3f, sample.HeightMetres);
        Assert.Equal(data.HeightWordsFileOffset + 2, sample.TopLeftFileOffset);
        Assert.Equal(RetailTerrainSampleStatus.OutsideSupportedQuad, data.SampleTerrain(1, 0, 1, 1).Status);
        Assert.Equal(RetailTerrainSampleStatus.OutsideSupportedQuad, data.SampleTerrain(2, 0, 4, 1).Status);
        Assert.Equal(RetailTerrainSampleStatus.NonFinite, data.SampleTerrain(0, 0, float.NaN, 0).Status);
        Assert.Equal(RetailTerrainSampleStatus.NonFinite, data.SampleTerrain(0, 0, 0, float.PositiveInfinity).Status);
    }

    [Fact]
    public void CompatibilityDiagnosticsKeepGuidAndDimensionsDistinctFromParsing()
    {
        var file = Fixture(2, 1, new ushort[6], true, Guid.Empty).Concat(new byte[] { 77 }).ToArray();
        var result = RetailHeightMap40Reader.Read(file, new()
        { ExpectedGuid = Guid.Parse("00000000-0000-0000-0000-000000000001"), ExpectedWorldWidthMetres = 8, ExpectedWorldHeightMetres = 6 });
        Assert.True(result.Success);
        Assert.Equal(new[] { "GuidMismatch", "WidthMismatch", "HeightMismatch", "TrailingBytes" }, result.Diagnostics.Select(d => d.Code));
        Assert.Equal(new byte[] { 77 }, result.Data!.TrailingBytes.ToArray());
        var missing = RetailHeightMap40Reader.Read(Fixture(1, 1, new ushort[4]), new() { ExpectedGuid = Guid.Empty });
        Assert.Equal("MissingGuid", Assert.Single(missing.Diagnostics).Code);
    }

    [Fact]
    public void ReportsTheStartOfEachIncompletePlaneAndNeverInfersOtherLayouts()
    {
        var file = Fixture(1, 1, new ushort[4], true);
        Error(file[..135], RetailHeightMap40ErrorCode.Truncated, 135);
        Error(file[..143], RetailHeightMap40ErrorCode.Truncated, 136);
        Error(file[..147], RetailHeightMap40ErrorCode.Truncated, 144);
        Error(file[..151], RetailHeightMap40ErrorCode.Truncated, 148);
        // A 32 layout's one byte plane cannot become a 40 layout through a guessed fallback.
        Error(file[..148], RetailHeightMap40ErrorCode.Truncated, 148);
        var trailing = file.Concat(new byte[] { 1 }).ToArray();
        var result = RetailHeightMap40Reader.Read(trailing, new() { RequireExactEnd = true });
        Assert.Equal(RetailHeightMap40ErrorCode.TrailingBytes, result.Error!.Code);
        Assert.Equal(file.Length, result.Error.FileOffset);
    }

    [Fact]
    public void ValidatesDimensionsAndBudgetsBeforeOwningInput()
    {
        var file = Fixture(1, 1, new ushort[4]);
        Assert.Equal(RetailHeightMap40ErrorCode.InvalidOptions, RetailHeightMap40Reader.Read(file, new() { MaxSamples = 0 }).Error!.Code);
        Assert.Equal(RetailHeightMap40ErrorCode.InputLimitExceeded, RetailHeightMap40Reader.Read(file, new() { MaxInputBytes = 8 }).Error!.Code);
        Assert.Equal(RetailHeightMap40ErrorCode.AllocationLimitExceeded, RetailHeightMap40Reader.Read(file, new() { MaxSamples = 3 }).Error!.Code);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(0), -1);
        Error(file, RetailHeightMap40ErrorCode.InvalidDimensions, 0);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(0), int.MaxValue / 2);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(4), int.MaxValue / 2);
        Error(file, RetailHeightMap40ErrorCode.AllocationLimitExceeded, 0);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(4), int.MaxValue);
        Error(file, RetailHeightMap40ErrorCode.InvalidDimensions, 4);
    }

    internal static byte[] Fixture(int width, int height, ushort[] words, bool headered = false, Guid? guid = null)
    {
        Assert.Equal((width + 1) * (height + 1), words.Length);
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
        if (headered)
        {
            var header = new byte[128]; BinaryPrimitives.WriteUInt32LittleEndian(header, 0xaa55aa55);
            (guid ?? Guid.Empty).ToByteArray().CopyTo(header, 4); writer.Write(header);
        }
        writer.Write(height); writer.Write(width);
        foreach (ushort word in words) writer.Write(word);
        for (int i = 0; i < words.Length; i++) writer.Write((byte)(i + 1));
        for (int i = 0; i < words.Length; i++) writer.Write((byte)(i + 21));
        return memory.ToArray();
    }

    private static void Error(byte[] file, RetailHeightMap40ErrorCode code, int offset)
    {
        var result = RetailHeightMap40Reader.Read(file);
        Assert.False(result.Success); Assert.Null(result.Data);
        Assert.Equal(code, result.Error!.Code); Assert.Equal(offset, result.Error.FileOffset);
    }
}
