using System.Buffers.Binary;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Bounded HeightMap40 decoder. The caller supplies the format identity; the 24/32 fallback/conversion is deliberately unsupported.</summary>
public static class RetailHeightMap40Reader
{
    public static RetailHeightMap40ReadResult Read(ReadOnlySpan<byte> bytes, RetailHeightMap40ReadOptions? options = null)
    {
        options ??= new();
        if (options.MaxInputBytes < 8 || options.MaxSamples < 1 ||
            options.ExpectedWorldWidthMetres is <= 0 || options.ExpectedWorldHeightMetres is <= 0)
            return Fail(RetailHeightMap40ErrorCode.InvalidOptions, 0, "Byte/sample limits and any expected dimensions must be positive.");
        if (bytes.Length > options.MaxInputBytes)
            return Fail(RetailHeightMap40ErrorCode.InputLimitExceeded, 0, "Input exceeds its explicit byte budget.");
        if (bytes.Length < 4) return Fail(RetailHeightMap40ErrorCode.Truncated, bytes.Length, "Missing header/dimension prefix.");
        int header = BinaryPrimitives.ReadUInt32LittleEndian(bytes) == 0xaa55aa55 ? 128 : 0;
        if (bytes.Length < header + 8)
            return Fail(RetailHeightMap40ErrorCode.Truncated, bytes.Length, "Missing complete optional header and dimensions.");
        // Native first reads height intervals into +0x1c, then width intervals into +0x18. Sample stride is width+1.
        int height = BinaryPrimitives.ReadInt32LittleEndian(bytes[header..]);
        int width = BinaryPrimitives.ReadInt32LittleEndian(bytes[(header + 4)..]);
        if (height <= 0 || height > int.MaxValue / 2)
            return Fail(RetailHeightMap40ErrorCode.InvalidDimensions, header, "Height intervals are outside positive int32 metre support.");
        if (width <= 0 || width > int.MaxValue / 2)
            return Fail(RetailHeightMap40ErrorCode.InvalidDimensions, header + 4, "Width intervals are outside positive int32 metre support.");
        long samples = ((long)width + 1) * ((long)height + 1);
        if (samples > options.MaxSamples || samples > (int.MaxValue - header - 8) / 4)
            return Fail(RetailHeightMap40ErrorCode.AllocationLimitExceeded, header, "Sample count/layout exceeds its defensive support budget.");
        int count = (int)samples, heightStart = header + 8;
        // Diagnose the start of the first incomplete plane, not an invented successfully-read byte count.
        if (bytes.Length < heightStart + count * 2)
            return Fail(RetailHeightMap40ErrorCode.Truncated, heightStart, "Incomplete uint16 height plane.");
        if (bytes.Length < heightStart + count * 3)
            return Fail(RetailHeightMap40ErrorCode.Truncated, heightStart + count * 2, "Incomplete first raw byte plane.");
        int parsed = heightStart + count * 4;
        if (bytes.Length < parsed)
            return Fail(RetailHeightMap40ErrorCode.Truncated, heightStart + count * 3, "Incomplete second raw byte plane (24/32 are not interpreted as 40).");
        if (options.RequireExactEnd && parsed != bytes.Length)
            return Fail(RetailHeightMap40ErrorCode.TrailingBytes, parsed, "Trailing bytes are forbidden by the requested exact-end policy.");
        var diagnostics = new List<RetailHeightMap40Diagnostic>();
        if (options.ExpectedGuid is Guid expected)
        {
            if (header == 0) diagnostics.Add(new("MissingGuid", 0, "Headerless input has no GUID; compatibility with the expected identity is unverified."));
            else if (new Guid(bytes.Slice(4, 16)) != expected)
                diagnostics.Add(new("GuidMismatch", 4, "Raw heightmap GUID differs from the explicitly expected source identity."));
        }
        if (options.ExpectedWorldWidthMetres is int expectedWidth && expectedWidth != width * 2)
            diagnostics.Add(new("WidthMismatch", header + 4, "Native width differs from the declared world width; parsing does not establish compatibility."));
        if (options.ExpectedWorldHeightMetres is int expectedHeight && expectedHeight != height * 2)
            diagnostics.Add(new("HeightMismatch", header, "Native height differs from the declared world height; parsing does not establish compatibility."));
        if (parsed != bytes.Length) diagnostics.Add(new("TrailingBytes", parsed, "Bytes after the complete 40 layout remain preserved without interpretation."));
        // One owned input copy; planes are slices, not expanded arrays. Limits do not include the caller's buffer or promise total process memory.
        return new(new(bytes.ToArray(), header, width, height, count, parsed), null, diagnostics.AsReadOnly());
    }

    private static RetailHeightMap40ReadResult Fail(RetailHeightMap40ErrorCode code, int offset, string message) =>
        new(null, new(code, offset, message), Array.Empty<RetailHeightMap40Diagnostic>());
}
