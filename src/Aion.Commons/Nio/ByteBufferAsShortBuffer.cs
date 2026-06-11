namespace Aion.Commons.Nio;

/// <summary>
/// A short view over a <see cref="ByteBuffer"/>'s bytes, decoding with a fixed byte order.
/// Faithful minimal port of java.nio.ByteBufferAsShortBufferB/L (created by ByteBuffer.asShortBuffer).
/// </summary>
public sealed class ByteBufferAsShortBuffer : ShortBuffer
{
    private readonly ByteBuffer _bb;
    private readonly int _byteOffset;
    private readonly bool _bigEndian;

    internal ByteBufferAsShortBuffer(ByteBuffer bb, int mark, int pos, int lim, int cap, int off, bool bigEndian)
        : base(mark, pos, lim, cap)
    {
        _bb = bb;
        _byteOffset = off;
        _bigEndian = bigEndian;
    }

    private int Ix(int i)
    {
        return _byteOffset + (i << 1);
    }

    private short GetImpl(int bi)
    {
        int b0 = _bb.Get(bi) & 0xFF;
        int b1 = _bb.Get(bi + 1) & 0xFF;
        return _bigEndian ? (short)((b0 << 8) | b1) : (short)((b1 << 8) | b0);
    }

    private void PutImpl(int bi, short s)
    {
        if (_bigEndian)
        {
            _bb.Put(bi, (byte)(s >> 8));
            _bb.Put(bi + 1, (byte)s);
        }
        else
        {
            _bb.Put(bi, (byte)s);
            _bb.Put(bi + 1, (byte)(s >> 8));
        }
    }

    public override short Get()
    {
        return GetImpl(Ix(NextGetIndex()));
    }

    public override short Get(int index)
    {
        return GetImpl(Ix(CheckIndex(index)));
    }

    public override ShortBuffer Put(short s)
    {
        PutImpl(Ix(NextPutIndex()), s);
        return this;
    }

    public override ShortBuffer Put(int index, short s)
    {
        PutImpl(Ix(CheckIndex(index)), s);
        return this;
    }

    public override ByteOrder Order()
    {
        return _bigEndian ? ByteOrder.BIG_ENDIAN : ByteOrder.LITTLE_ENDIAN;
    }

    public override bool IsReadOnly()
    {
        return _bb.IsReadOnly();
    }
}
