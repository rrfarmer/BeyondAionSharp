namespace Aion.GameServer.Commons.Nio;

/// <summary>
/// A heap-backed (float[]) float buffer. Faithful minimal port of java.nio.HeapFloatBuffer.
/// </summary>
public sealed class HeapFloatBuffer : FloatBuffer
{
    internal HeapFloatBuffer(float[] buf, int off, int len)
        : base(-1, off, off + len, buf.Length, buf, 0)
    {
    }

    private int Ix(int i)
    {
        return i + offset;
    }

    public override float Get()
    {
        return hb![Ix(NextGetIndex())];
    }

    public override float Get(int index)
    {
        return hb![Ix(CheckIndex(index))];
    }

    public override FloatBuffer Put(float f)
    {
        hb![Ix(NextPutIndex())] = f;
        return this;
    }

    public override FloatBuffer Put(int index, float f)
    {
        hb![Ix(CheckIndex(index))] = f;
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
