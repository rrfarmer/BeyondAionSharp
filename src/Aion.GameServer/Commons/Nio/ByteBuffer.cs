using System;

namespace Aion.GameServer.Commons.Nio;

/// <summary>
/// A byte buffer. Faithful minimal port of java.nio.ByteBuffer (the subset used by the geoEngine
/// .geo loader): allocate/wrap, relative/absolute get/put, getShort/getInt/getFloat honoring
/// <see cref="Order"/>, slice, and asFloatBuffer/asShortBuffer views. Default order is BIG_ENDIAN
/// (java.nio default; .geo files are big-endian). Uses C# unsigned <c>byte</c> for single-byte
/// access — <c>&amp; 0xFF</c> sites are identical; callers needing Java's signed byte cast to sbyte.
/// </summary>
public abstract class ByteBuffer : Buffer
{
    internal readonly byte[]? hb; // non-null only for heap buffers
    internal readonly int offset;
    internal bool bigEndian = true; // java.nio default

    internal ByteBuffer(int mark, int pos, int lim, int cap, byte[]? hb, int offset)
        : base(mark, pos, lim, cap)
    {
        this.hb = hb;
        this.offset = offset;
    }

    internal ByteBuffer(int mark, int pos, int lim, int cap)
        : this(mark, pos, lim, cap, null, 0)
    {
    }

    /// <summary>Java parity: allocate(int capacity).</summary>
    public static ByteBuffer Allocate(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentException("capacity < 0: " + capacity);
        return new HeapByteBuffer(capacity, capacity);
    }

    /// <summary>Java parity: wrap(byte[] array, int offset, int length).</summary>
    public static ByteBuffer Wrap(byte[] array, int offset, int length)
    {
        return new HeapByteBuffer(array, offset, length);
    }

    /// <summary>Java parity: wrap(byte[] array).</summary>
    public static ByteBuffer Wrap(byte[] array)
    {
        return Wrap(array, 0, array.Length);
    }

    // --- single-byte relative/absolute get/put (abstract; HeapByteBuffer implements) ---
    public abstract byte Get();

    public abstract byte Get(int index);

    public abstract ByteBuffer Put(byte b);

    public abstract ByteBuffer Put(int index, byte b);

    /// <summary>Java parity: get(byte[] dst, int offset, int length).</summary>
    public ByteBuffer Get(byte[] dst, int offset, int length)
    {
        if (length > Remaining())
            throw new InvalidOperationException("BufferUnderflow");
        int end = offset + length;
        for (int i = offset; i < end; i++)
            dst[i] = Get();
        return this;
    }

    /// <summary>Java parity: get(byte[] dst).</summary>
    public ByteBuffer Get(byte[] dst)
    {
        return Get(dst, 0, dst.Length);
    }

    /// <summary>Java parity: put(byte[] src, int offset, int length).</summary>
    public ByteBuffer Put(byte[] src, int offset, int length)
    {
        if (length > Remaining())
            throw new InvalidOperationException("BufferOverflow");
        int end = offset + length;
        for (int i = offset; i < end; i++)
            Put(src[i]);
        return this;
    }

    /// <summary>Java parity: put(byte[] src).</summary>
    public ByteBuffer Put(byte[] src)
    {
        return Put(src, 0, src.Length);
    }

    // --- multi-byte accessors (honor byte order) ---
    public short GetShort()
    {
        return GetShort(NextGetIndex(2));
    }

    public short GetShort(int index)
    {
        int b0 = Get(index) & 0xFF;
        int b1 = Get(index + 1) & 0xFF;
        return bigEndian ? (short)((b0 << 8) | b1) : (short)((b1 << 8) | b0);
    }

    public int GetInt()
    {
        return GetInt(NextGetIndex(4));
    }

    public int GetInt(int index)
    {
        int b0 = Get(index) & 0xFF;
        int b1 = Get(index + 1) & 0xFF;
        int b2 = Get(index + 2) & 0xFF;
        int b3 = Get(index + 3) & 0xFF;
        return bigEndian
            ? (b0 << 24) | (b1 << 16) | (b2 << 8) | b3
            : (b3 << 24) | (b2 << 16) | (b1 << 8) | b0;
    }

    public float GetFloat()
    {
        return BitConverter.Int32BitsToSingle(GetInt());
    }

    public float GetFloat(int index)
    {
        return BitConverter.Int32BitsToSingle(GetInt(index));
    }

    /// <summary>Java parity: order().</summary>
    public ByteOrder Order()
    {
        return bigEndian ? ByteOrder.BIG_ENDIAN : ByteOrder.LITTLE_ENDIAN;
    }

    /// <summary>Java parity: order(ByteOrder bo).</summary>
    public ByteBuffer Order(ByteOrder bo)
    {
        bigEndian = bo == ByteOrder.BIG_ENDIAN;
        return this;
    }

    /// <summary>Java parity: slice() — shares content from this buffer's position to its limit.</summary>
    public abstract ByteBuffer Slice();

    /// <summary>Java parity (JDK 13+): slice(int index, int length) — absolute subsequence view.</summary>
    public abstract ByteBuffer Slice(int index, int length);

    /// <summary>Java parity: asFloatBuffer() — a float view of the remaining bytes (honors order).</summary>
    public FloatBuffer AsFloatBuffer()
    {
        int off = Position();
        int rem = Remaining();
        int size = rem >> 2;
        return new ByteBufferAsFloatBuffer(this, -1, 0, size, size, off, bigEndian);
    }

    /// <summary>Java parity: asShortBuffer() — a short view of the remaining bytes (honors order).</summary>
    public ShortBuffer AsShortBuffer()
    {
        int off = Position();
        int rem = Remaining();
        int size = rem >> 1;
        return new ByteBufferAsShortBuffer(this, -1, 0, size, size, off, bigEndian);
    }
}
