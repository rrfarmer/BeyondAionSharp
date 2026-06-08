using System;

namespace Aion.GameServer;

/// <summary>
/// Superclass of GameServer errors.
/// Java parity: GameServerError (Aquanox). Java extends Error → C# Exception.
/// </summary>
public class GameServerError : Exception
{
    /// <summary>Constructs a new error with null as its detail message.</summary>
    public GameServerError()
    {
    }

    /// <summary>
    /// Constructs a new error with the specified cause and a detail message of
    /// <c>(cause==null ? null : cause.ToString())</c>. Useful for wrappers around other throwables.
    /// </summary>
    public GameServerError(Exception? cause)
        : base(cause?.ToString(), cause)
    {
    }

    /// <summary>Constructs a new error with the specified detail message.</summary>
    public GameServerError(string message)
        : base(message)
    {
    }

    /// <summary>Constructs a new error with the specified detail message and cause.</summary>
    public GameServerError(string message, Exception? cause)
        : base(message, cause)
    {
    }
}
