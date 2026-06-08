namespace Aion.GameServer.Commons.Nio;

/// <summary>
/// A heap-backed (byte[]) byte buffer. Faithful minimal port of java.nio.HeapByteBuffer.
/// </summary>
public sealed class HeapByteBuffer : ByteBuffer
{
    internal HeapByteBuffer(int cap, int lim)
        : base(-1, 0, lim, cap, new byte[cap], 0)
    {
    }

    internal HeapByteBuffer(byte[] buf, int off, int len)
        : base(-1, off, off + len, buf.Length, buf, 0)
    {
    }

    internal HeapByteBuffer(byte[] buf, int mark, int pos, int lim, int cap, int off)
        : base(mark, pos, lim, cap, buf, off)
    {
    }

    private int Ix(int i)
    {
        return i + offset;
    }

    public override byte Get()
    {
        return hb![Ix(NextGetIndex())];
    }

    public override byte Get(int index)
    {
        return hb![Ix(CheckIndex(index))];
    }

    public override ByteBuffer Put(byte b)
    {
        hb![Ix(NextPutIndex())] = b;
        return this;
    }

    public override ByteBuffer Put(int index, byte b)
    {
        hb![Ix(CheckIndex(index))] = b;
        return this;
    }

    public override ByteBuffer Slice()
    {
        int rem = Remaining();
        var b = new HeapByteBuffer(hb!, -1, 0, rem, rem, Position() + offset);
        b.bigEndian = bigEndian;
        return b;
    }

    public override ByteBuffer Slice(int index, int length)
    {
        var b = new HeapByteBuffer(hb!, -1, 0, length, length, index + offset);
        b.bigEndian = bigEndian;
        return b;
    }

    public override bool IsReadOnly()
    {
        return false;
    }
}
