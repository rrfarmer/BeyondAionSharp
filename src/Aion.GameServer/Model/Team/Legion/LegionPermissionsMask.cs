namespace Aion.GameServer.Model.Team.Legion;

/// <summary>Java parity: model/team/legion/LegionPermissionsMask.</summary>
public enum LegionPermissionsMask
{
    EDIT = 0x200,
    INVITE = 0x8,
    KICK = 0x10,
    WH_WITHDRAWAL = 0x4,
    WH_DEPOSIT = 0x1000,
    ARTIFACT = 0x400,
    GUARDIAN_STONE = 0x800,
}

public static class LegionPermissionsMaskExtensions
{
    public static bool Can(this LegionPermissionsMask mask, int permission)
    {
        return ((int)mask & permission) != 0;
    }
}
