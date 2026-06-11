using System;

namespace Aion.Commons.Nio;

/// <summary>
/// Byte order (endianness). Faithful minimal port of java.nio.ByteOrder, used by the geoEngine
/// buffer types that the .geo loader relies on.
/// </summary>
public sealed class ByteOrder
{
    private readonly string _name;

    private ByteOrder(string name)
    {
        _name = name;
    }

    public static readonly ByteOrder BIG_ENDIAN = new("BIG_ENDIAN");
    public static readonly ByteOrder LITTLE_ENDIAN = new("LITTLE_ENDIAN");

    /// <summary>Java parity: nativeOrder() — the byte order of the underlying platform.</summary>
    public static ByteOrder NativeOrder()
    {
        return System.BitConverter.IsLittleEndian ? LITTLE_ENDIAN : BIG_ENDIAN;
    }

    public override string ToString()
    {
        return _name;
    }
}
