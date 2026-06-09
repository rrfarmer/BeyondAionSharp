namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>Java parity: model/gameobjects/player/DeniedStatus (id-bearing enum).</summary>
public enum DeniedStatus
{
    VIEW_DETAILS = 1,
    TRADE = 2,
    GROUP = 4,
    GUILD = 8,
    FRIEND = 16,
    DUEL = 32,
}

public static class DeniedStatusExtensions
{
    /// <summary>Java parity: DeniedStatus.getId()</summary>
    public static int GetId(this DeniedStatus status)
    {
        return (int)status;
    }
}
