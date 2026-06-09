using System;

namespace Aion.GameServer.Utils.Idfactory;

/// <summary>
/// Java parity: utils/idfactory/IDFactoryError (SoulKeeper). Java <c>extends Error</c> (serious, uncatchable-by-convention) →
/// C# : Exception (no Error analog); 4-ctor pattern, ctor(Throwable cause)→base(null, cause); serialVersionUID dropped.
/// </summary>
public class IDFactoryError : Exception
{
    public IDFactoryError(string message) : base(message)
    {
    }

    public IDFactoryError(string message, Exception cause) : base(message, cause)
    {
    }

    public IDFactoryError(Exception cause) : base(null, cause)
    {
    }
}
