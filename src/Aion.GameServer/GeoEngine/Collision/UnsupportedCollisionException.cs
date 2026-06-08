using System;

namespace Aion.GameServer.GeoEngine.Collision;

/// <summary>
/// Java parity: geoEngine/collision/UnsupportedCollisionException (Kirill).
/// Java extends UnsupportedOperationException → C# NotSupportedException.
/// </summary>
public class UnsupportedCollisionException : NotSupportedException
{
    public UnsupportedCollisionException(Exception arg0)
        : base(null, arg0)
    {
    }

    public UnsupportedCollisionException(string arg0, Exception arg1)
        : base(arg0, arg1)
    {
    }

    public UnsupportedCollisionException(string arg0)
        : base(arg0)
    {
    }

    public UnsupportedCollisionException()
    {
    }
}
