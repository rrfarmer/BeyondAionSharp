using System.Buffers.Binary;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailHeightMap40ErrorCode { InvalidOptions, InputLimitExceeded, Truncated, InvalidDimensions, AllocationLimitExceeded, TrailingBytes }
public sealed record RetailHeightMap40Error(RetailHeightMap40ErrorCode Code, int FileOffset, string Message);
public sealed record RetailHeightMap40Diagnostic(string Code, int FileOffset, string Message);

/// <summary>Defensive support limits, not recovered native allocation defaults. Input is explicitly HeightMap40, never auto-detected 24/32.</summary>
public sealed record RetailHeightMap40ReadOptions
{
    public int MaxInputBytes { get; init; } = 128 * 1024 * 1024;
    public int MaxSamples { get; init; } = 32_000_000;
    public int? ExpectedWorldWidthMetres { get; init; }
    public int? ExpectedWorldHeightMetres { get; init; }
    public Guid? ExpectedGuid { get; init; }
    public bool RequireExactEnd { get; init; }
}

public sealed record RetailHeightMap40ReadResult(RetailHeightMap40Data? Data, RetailHeightMap40Error? Error,
    IReadOnlyList<RetailHeightMap40Diagnostic> Diagnostics)
{
    public bool Success => Data is not null && Error is null;
}

public enum RetailTerrainSampleStatus { Sampled, NonFinite, OutsideSupportedQuad }

/// <summary>Four raw words and their source offsets accompany the float32 metre result. Low bits and byte planes have no inferred walkability meaning.</summary>
public readonly record struct RetailTerrainSample(RetailTerrainSampleStatus Status, float HeightMetres,
    int TopLeftFileOffset, int TopRightFileOffset, int BottomLeftFileOffset, int BottomRightFileOffset,
    ushort TopLeft, ushort TopRight, ushort BottomLeft, ushort BottomRight);

/// <summary>Owned, raw-backed HeightMap40 data; native loader 0x140298620 and float32 sampler 0x14021e3a0. Inactive: no GeoMap integration.</summary>
public sealed class RetailHeightMap40Data
{
    internal RetailHeightMap40Data(byte[] bytes, int headerBytes, int width, int height, int samples, int parsedBytes)
    {
        RawFile = bytes; HeaderBytes = headerBytes; WidthIntervals = width; HeightIntervals = height;
        SampleCount = samples; ParsedBytes = parsedBytes;
    }

    public ReadOnlyMemory<byte> RawFile { get; }
    public int HeaderBytes { get; }
    public ReadOnlyMemory<byte> Header => RawFile[..HeaderBytes];
    public ReadOnlyMemory<byte> GuidBytes => HeaderBytes == 128 ? RawFile.Slice(4, 16) : ReadOnlyMemory<byte>.Empty;
    public Guid? BuildGuid => HeaderBytes == 128 ? new Guid(GuidBytes.Span) : null;
    public int WidthIntervals { get; }
    public int HeightIntervals { get; }
    public int Stride => WidthIntervals + 1;
    public int WorldWidthMetres => WidthIntervals * 2;
    public int WorldHeightMetres => HeightIntervals * 2;
    public int SampleCount { get; }
    public int ParsedBytes { get; }
    public int HeightWordsFileOffset => HeaderBytes + 8;
    public ReadOnlyMemory<byte> HeightWordBytes => RawFile.Slice(HeightWordsFileOffset, SampleCount * 2);
    public ReadOnlyMemory<byte> BytePlane1 => RawFile.Slice(HeightWordsFileOffset + SampleCount * 2, SampleCount);
    public ReadOnlyMemory<byte> BytePlane2 => RawFile.Slice(HeightWordsFileOffset + SampleCount * 3, SampleCount);
    public ReadOnlyMemory<byte> TrailingBytes => RawFile[ParsedBytes..];

    public ushort GetRawWord(int x, int y)
    {
        if ((uint)x > (uint)WidthIntervals || (uint)y > (uint)HeightIntervals)
            throw new ArgumentOutOfRangeException(nameof(x));
        return BinaryPrimitives.ReadUInt16LittleEndian(HeightWordBytes.Span[((y * Stride + x) * 2)..]);
    }

    /// <summary>Explicit quad support includes its four corners. Extrapolation/nonfinite inputs are unsupported, rather than native unchecked memory access.</summary>
    public RetailTerrainSample SampleTerrain(int gridX, int gridY, float worldX, float worldY)
    {
        if (!float.IsFinite(worldX) || !float.IsFinite(worldY)) return Invalid(RetailTerrainSampleStatus.NonFinite);
        if ((uint)gridX >= (uint)WidthIntervals || (uint)gridY >= (uint)HeightIntervals)
            return Invalid(RetailTerrainSampleStatus.OutsideSupportedQuad);
        float x = worldX * 0.5f;
        float y = worldY * 0.5f;
        x -= gridX; y -= gridY;
        if (x < 0 || x > 1 || y < 0 || y > 1) return Invalid(RetailTerrainSampleStatus.OutsideSupportedQuad);
        int a = HeightWordsFileOffset + (gridY * Stride + gridX) * 2;
        ushort h00 = GetRawWord(gridX, gridY), h10 = GetRawWord(gridX + 1, gridY);
        ushort h01 = GetRawWord(gridX, gridY + 1), h11 = GetRawWord(gridX + 1, gridY + 1);
        // Preserve MULSS/ADDSS order from the checked 239-byte native window. Do not use FMA or a double interpolation.
        float inverseX = 1f - x, inverseY = 1f - y;
        float topLeft = (h00 & 0xfffc) * inverseX;
        float topRight = (h10 & 0xfffc) * x;
        float top = topLeft + topRight;
        float bottomLeft = (h01 & 0xfffc) * inverseX;
        top *= inverseY;
        float bottomRight = (h11 & 0xfffc) * x;
        float bottom = bottomLeft + bottomRight;
        bottom *= y;
        float sum = top + bottom;
        float metres = sum * 0.03125f;
        return new(RetailTerrainSampleStatus.Sampled, metres, a, a + 2, a + Stride * 2, a + Stride * 2 + 2, h00, h10, h01, h11);
    }

    private static RetailTerrainSample Invalid(RetailTerrainSampleStatus status) => new(status, float.NaN, -1, -1, -1, -1, 0, 0, 0, 0);
}
