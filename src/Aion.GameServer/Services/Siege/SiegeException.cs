using System;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/SiegeException (RuntimeException→Exception).</summary>
public class SiegeException : Exception
{
    public SiegeException()
    {
    }

    public SiegeException(string message)
        : base(message)
    {
    }

    public SiegeException(string message, Exception cause)
        : base(message, cause)
    {
    }

    // Java parity: RuntimeException(Throwable cause) → base(null, cause).
    public SiegeException(Exception cause)
        : base(null, cause)
    {
    }
}
