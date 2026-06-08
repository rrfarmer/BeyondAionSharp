namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Dash/move-effect status. Java parity: skillengine/model/DashStatus.</summary>
public enum DashStatus
{
    NONE = 0,
    RANDOMMOVELOC = 1,
    DASH = 2,
    BACKDASH = 3,
    MOVEBEHIND = 4,
    RANDOMMOVELOC_NEW = 6,
}

public static class DashStatusExtensions
{
    // Java parity: getId()
    public static int GetId(this DashStatus status) => (int)status;
}
