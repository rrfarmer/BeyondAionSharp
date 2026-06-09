using System;

namespace Aion.GameServer.Model.Base;

/// <summary>Java parity: model/base/BaseException (Estrayl).</summary>
public class BaseException : Exception
{
    public BaseException()
    {
    }

    public BaseException(string message)
        : base(message)
    {
    }

    public BaseException(string message, Exception cause)
        : base(message, cause)
    {
    }

    public BaseException(Exception cause)
        : base(null, cause)
    {
    }
}
