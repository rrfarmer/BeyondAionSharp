namespace Aion.GameServer.Model.Summons;

/// <summary>Java parity: model/summons/SummonMode (xTz). Id-bearing enum (ids non-sequential).</summary>
public enum SummonMode
{
    ATTACK = 0,
    GUARD = 1,
    REST = 2,
    RELEASE = 3,
    UNK = 5,
}

public static class SummonModeExtensions
{
    /// <summary>Java parity: getId() — backing value equals the Java per-constant id.</summary>
    public static int GetId(this SummonMode mode) => (int)mode;

    public static SummonMode? GetSummonModeById(int id)
    {
        foreach (SummonMode mode in System.Enum.GetValues<SummonMode>())
        {
            if (mode.GetId() == id)
            {
                return mode;
            }
        }
        return null;
    }
}
