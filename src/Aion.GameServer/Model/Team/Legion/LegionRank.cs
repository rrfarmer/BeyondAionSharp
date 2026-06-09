namespace Aion.GameServer.Model.Team.Legion;

/// <summary>Java parity: model/team/legion/LegionRank (id-bearing enum).</summary>
public enum LegionRank : byte
{
    /// <summary>All Legion Ranks</summary>
    BRIGADE_GENERAL = 0,
    DEPUTY = 1,
    CENTURION = 2,
    LEGIONARY = 3,
    VOLUNTEER = 4,
}

public static class LegionRankExtensions
{
    /// <summary>Returns client-side id for this.</summary>
    public static byte GetRankId(this LegionRank rank)
    {
        return (byte)rank;
    }
}
