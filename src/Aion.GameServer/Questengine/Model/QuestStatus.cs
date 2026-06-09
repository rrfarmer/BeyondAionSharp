namespace Aion.GameServer.Questengine.Model;

/// <summary>Java parity: questEngine/model/QuestStatus (MrPoke). Id-bearing enum (backing value == Java id).</summary>
public enum QuestStatus
{
    START = 3, // Accepted quests
    REWARD = 4, // The quests, that are finished. "Go and get your reward"
    COMPLETE = 5, // Completed quests
    LOCKED = 6, // Not (yet) available quests
}

public static class QuestStatusExtensions
{
    /// <summary>Java parity: value() — backing value equals the Java per-constant id.</summary>
    public static int Value(this QuestStatus status) => (int)status;
}
