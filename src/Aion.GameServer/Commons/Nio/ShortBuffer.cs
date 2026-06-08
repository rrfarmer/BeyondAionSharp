using System;

namespace Aion.GameServer.Commons.Nio;

/// <summary>
/// A short buffer. Faithful minimal port of java.nio.ShortBuffer (the subset used by the
/// geoEngine: get/get(short[])); created either heap-backed (<see cref="Wrap"/>) or as a view
/// over a <see cref="ByteBuffer"/> (ByteBuffer.AsShortBuffer).
/// </summary>
public abstract class ShortBuffer : Buffer
{
    internal readonly short[]? hb; // non-null only for heap buffers
    internal readonly int offset;

    internal ShortBuffer(int mark, int pos, int lim, int cap, short[]? hb, int offset)
        : base(mark, pos, lim, cap)
    {
        this.hb = hb;
        this.offset = offset;
    }

    internal ShortBuffer(int mark, int pos, int lim, int cap)
        : this(mark, pos, lim, cap, null, 0)
    {
    }

    /// <summary>Java parity: wrap(short[] array, int offset, int length).</summary>
    public static ShortBuffer Wrap(short[] array, int offset, int length)
    {
        return new HeapShortBuffer(array, offset, length);
    }

    /// <summary>Java parity: wrap(short[] array).</summary>
    public static ShortBuffer Wrap(short[] array)
    {
        return Wrap(array, 0, array.Length);
    }

    public abstract short Get();

    public abstract ShortBuffer Put(short s);

    public abstract short Get(int index);

    public abstract ShortBuffer Put(int index, short s);

    /// <summary>Java parity: get(short[] dst, int offset, int length).</summary>
    public ShortBuffer Get(short[] dst, int offset, int length)
    {
        if (length > Remaining())
            throw new InvalidOperationException("BufferUnderflow");
        int end = offset + length;
        for (int i = offset; i < end; i++)
            dst[i] = Get();
        return this;
    }

    /// <summary>Java parity: get(short[] dst).</summary>
    public ShortBuffer Get(short[] dst)
    {
        return Get(dst, 0, dst.Length);
    }

    /// <summary>Java parity: put(short[] src, int offset, int length).</summary>
    public ShortBuffer Put(short[] src, int offset, int length)
    {
        if (length > Remaining())
            throw new InvalidOperationException("BufferOverflow");
        int end = offset + length;
        for (int i = offset; i < end; i++)
            Put(src[i]);
        return this;
    }

    /// <summary>Java parity: put(short[] src).</summary>
    public ShortBuffer Put(short[] src)
    {
        return Put(src, 0, src.Length);
    }

    /// <summary>Java parity: order().</summary>
    public abstract ByteOrder Order();
}
