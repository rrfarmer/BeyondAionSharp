namespace Aion.Commons.Nio;

/// <summary>
/// A heap-backed (short[]) short buffer. Faithful minimal port of java.nio.HeapShortBuffer.
/// </summary>
public sealed class HeapShortBuffer : ShortBuffer
{
    internal HeapShortBuffer(short[] buf, int off, int len)
        : base(-1, off, off + len, buf.Length, buf, 0)
    {
    }

    private int Ix(int i)
    {
        return i + offset;
    }

    public override short Get()
    {
        return hb![Ix(NextGetIndex())];
    }

    public override short Get(int index)
    {
        return hb![Ix(CheckIndex(index))];
    }

    public override ShortBuffer Put(short s)
    {
        hb![Ix(NextPutIndex())] = s;
        return this;
    }

    public override ShortBuffer Put(int index, short s)
    {
        hb![Ix(CheckIndex(index))] = s;
        return this;
    }

    public override ByteOrder Order()
    {
        return ByteOrder.NativeOrder();
    }

    public override bool IsReadOnly()
    {
        return false;
    }
}
