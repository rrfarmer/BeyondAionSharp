using System;

namespace Aion.Commons.Nio;

/// <summary>
/// A float buffer. Faithful minimal port of java.nio.FloatBuffer (the subset used by the
/// geoEngine: wrap/get/put/get(float[])); created either heap-backed (<see cref="Wrap"/>) or as a
/// view over a <see cref="ByteBuffer"/> (ByteBuffer.AsFloatBuffer).
/// </summary>
public abstract class FloatBuffer : Buffer
{
    internal readonly float[]? hb; // non-null only for heap buffers
    internal readonly int offset;

    internal FloatBuffer(int mark, int pos, int lim, int cap, float[]? hb, int offset)
        : base(mark, pos, lim, cap)
    {
        this.hb = hb;
        this.offset = offset;
    }

    internal FloatBuffer(int mark, int pos, int lim, int cap)
        : this(mark, pos, lim, cap, null, 0)
    {
    }

    /// <summary>Java parity: wrap(float[] array, int offset, int length).</summary>
    public static FloatBuffer Wrap(float[] array, int offset, int length)
    {
        return new HeapFloatBuffer(array, offset, length);
    }

    /// <summary>Java parity: wrap(float[] array).</summary>
    public static FloatBuffer Wrap(float[] array)
    {
        return Wrap(array, 0, array.Length);
    }

    // relative get/put
    public abstract float Get();

    public abstract FloatBuffer Put(float f);

    // absolute get/put
    public abstract float Get(int index);

    public abstract FloatBuffer Put(int index, float f);

    /// <summary>Java parity: get(float[] dst, int offset, int length).</summary>
    public FloatBuffer Get(float[] dst, int offset, int length)
    {
        if (length > Remaining())
            throw new InvalidOperationException("BufferUnderflow");
        int end = offset + length;
        for (int i = offset; i < end; i++)
            dst[i] = Get();
        return this;
    }

    /// <summary>Java parity: get(float[] dst).</summary>
    public FloatBuffer Get(float[] dst)
    {
        return Get(dst, 0, dst.Length);
    }

    /// <summary>Java parity: put(float[] src, int offset, int length).</summary>
    public FloatBuffer Put(float[] src, int offset, int length)
    {
        if (length > Remaining())
            throw new InvalidOperationException("BufferOverflow");
        int end = offset + length;
        for (int i = offset; i < end; i++)
            Put(src[i]);
        return this;
    }

    /// <summary>Java parity: put(float[] src).</summary>
    public FloatBuffer Put(float[] src)
    {
        return Put(src, 0, src.Length);
    }

    /// <summary>Java parity: order() — the byte order of this buffer.</summary>
    public abstract ByteOrder Order();
}
