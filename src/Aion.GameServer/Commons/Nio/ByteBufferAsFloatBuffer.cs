using System;

namespace Aion.GameServer.Commons.Nio;

/// <summary>
/// A float view over a <see cref="ByteBuffer"/>'s bytes, decoding with a fixed byte order.
/// Faithful minimal port of java.nio.ByteBufferAsFloatBufferB/L (created by ByteBuffer.asFloatBuffer).
/// </summary>
public sealed class ByteBufferAsFloatBuffer : FloatBuffer
{
    private readonly ByteBuffer _bb;
    private readonly int _byteOffset;
    private readonly bool _bigEndian;

    internal ByteBufferAsFloatBuffer(ByteBuffer bb, int mark, int pos, int lim, int cap, int off, bool bigEndian)
        : base(mark, pos, lim, cap)
    {
        _bb = bb;
        _byteOffset = off;
        _bigEndian = bigEndian;
    }

    private int Ix(int i)
    {
        return _byteOffset + (i << 2);
    }

    private float GetImpl(int bi)
    {
        int b0 = _bb.Get(bi) & 0xFF;
        int b1 = _bb.Get(bi + 1) & 0xFF;
        int b2 = _bb.Get(bi + 2) & 0xFF;
        int b3 = _bb.Get(bi + 3) & 0xFF;
        int bits = _bigEndian
            ? (b0 << 24) | (b1 << 16) | (b2 << 8) | b3
            : (b3 << 24) | (b2 << 16) | (b1 << 8) | b0;
        return BitConverter.Int32BitsToSingle(bits);
    }

    private void PutImpl(int bi, float f)
    {
        int bits = BitConverter.SingleToInt32Bits(f);
        if (_bigEndian)
        {
            _bb.Put(bi, (byte)(bits >> 24));
            _bb.Put(bi + 1, (byte)(bits >> 16));
            _bb.Put(bi + 2, (byte)(bits >> 8));
            _bb.Put(bi + 3, (byte)bits);
        }
        else
        {
            _bb.Put(bi, (byte)bits);
            _bb.Put(bi + 1, (byte)(bits >> 8));
            _bb.Put(bi + 2, (byte)(bits >> 16));
            _bb.Put(bi + 3, (byte)(bits >> 24));
        }
    }

    public override float Get()
    {
        return GetImpl(Ix(NextGetIndex()));
    }

    public override float Get(int index)
    {
        return GetImpl(Ix(CheckIndex(index)));
    }

    public override FloatBuffer Put(float f)
    {
        PutImpl(Ix(NextPutIndex()), f);
        return this;
    }

    public override FloatBuffer Put(int index, float f)
    {
        PutImpl(Ix(CheckIndex(index)), f);
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
