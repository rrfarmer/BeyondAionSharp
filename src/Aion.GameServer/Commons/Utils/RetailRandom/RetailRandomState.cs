using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Aion.GameServer.Commons.Utils.RetailRandom;

/// <summary>Complete immutable5008-byte state. No implicit seed, padding, mask or BSS initialization.</summary>
public sealed class RetailRandomState
{
    public const int ByteLength = 5008;
    public const int WordCount = 624;
    public const int MaskOffset = 5000;
    private readonly ImmutableArray<byte> _bytes;

    private RetailRandomState(ReadOnlySpan<byte> bytes)
    {
        _bytes = ImmutableArray.CreateRange(bytes.ToArray());
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(_bytes.AsSpan()));
    }

    public ImmutableArray<byte> Bytes => _bytes;
    public string Sha256 { get; }
    public uint Index => BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan());
    public ulong TemperingMask => BinaryPrimitives.ReadUInt64LittleEndian(_bytes.AsSpan(MaskOffset..));
    public uint PaddingWord => BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(4..));

    public ulong Word(int index)
    {
        if ((uint)index >= WordCount) throw new ArgumentOutOfRangeException(nameof(index));
        return BinaryPrimitives.ReadUInt64LittleEndian(_bytes.AsSpan((8 + index * 8)..));
    }

    /// <summary>Pure byte admission. Hash validation does not verify current-memory provenance.</summary>
    public static bool TryCreate(ReadOnlySpan<byte> bytes, string? expectedSha256,
        out RetailRandomState? state, out RetailRandomStatus status)
    {
        state = null;
        if (bytes.Length != ByteLength) { status = RetailRandomStatus.StateSizeMismatch; return false; }
        if (!IsSha256(expectedSha256)) { status = RetailRandomStatus.StateHashMismatch; return false; }
        // Hash the captured immutable bytes, not a second read of the caller's span.
        var captured = new RetailRandomState(bytes);
        if (!string.Equals(captured.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        { status = RetailRandomStatus.StateHashMismatch; return false; }
        state = captured;
        status = RetailRandomStatus.Completed;
        return true;
    }

    internal static RetailRandomState FromOwnedEngineBytes(ReadOnlySpan<byte> bytes) => new(bytes);
    internal byte[] CopyBytes() => _bytes.ToArray();
    internal static bool IsSha256(string? text) => text is { Length: 64 } && text.All(Uri.IsHexDigit);
}
