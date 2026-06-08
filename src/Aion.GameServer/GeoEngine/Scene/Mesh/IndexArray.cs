using System;
using Aion.GameServer.Commons.Nio;

namespace Aion.GameServer.GeoEngine.Scene.mesh;

/// <summary>
/// Java parity: geoEngine/scene/mesh/IndexArray.
/// </summary>
public interface IndexArray
{
    int Get(int i);

    int Size();

    void Swap(int i1, int i2);

    static IndexArray From(Buffer buffer)
    {
        return buffer switch
        {
            ByteBuffer buf => new IndexByteArray(buf),
            ShortBuffer buf => new IndexShortArray(buf),
            _ => throw new ArgumentException(buffer.GetType().Name + " is not supported"),
        };
    }
}
