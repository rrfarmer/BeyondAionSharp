using System;
using Aion.GameServer.Commons.Nio;
using Aion.GameServer.GeoEngine.Utils;

namespace Aion.GameServer.GeoEngine.Scene.mesh;

/// <summary>
/// Java parity: geoEngine/scene/mesh/IndexShortArray.
/// </summary>
public record IndexShortArray(short[] buf) : IndexArray
{
    public IndexShortArray(ShortBuffer buf)
        : this(new short[buf.Limit()])
    {
        buf.Get(this.buf);
    }

    public int Get(int i)
    {
        return buf[i] & 0xFFFF;
    }

    public int Size()
    {
        return buf.Length;
    }

    public void Swap(int i1, int i2)
    {
        int p1 = i1 * 3;
        int p2 = i2 * 3;
        TempVars vars = TempVars.Get();
        // store p1 in tmp
        Array.Copy(buf, p1, vars.bihSwapTmpShort, 0, 3);

        // copy p2 to p1
        Array.Copy(buf, p2, buf, p1, 3);

        // copy tmp to p2
        Array.Copy(vars.bihSwapTmpShort, 0, buf, p2, 3);
        vars.Release();
    }
}
