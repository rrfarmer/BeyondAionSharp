namespace Aion.GameServer.QuestEngine.Handlers;

/// <summary>Java parity: questEngine/handlers/HandlerResult.</summary>
public enum HandlerResult
{
    UNKNOWN, // allow other handlers to process
    SUCCESS,
    FAILED
}

public static class HandlerResultExtensions
{
    // Java parity: static fromBoolean(Boolean) — nullable.
    public static HandlerResult FromBoolean(bool? value)
    {
        if (value == null)
            return HandlerResult.UNKNOWN;
        else if (value.Value)
            return HandlerResult.SUCCESS;
        return HandlerResult.FAILED;
    }
}
