namespace Aion.GameServer.Model.Stats.Calc;

/// <summary>
/// Base-stat formulas for a player class at a given level.
/// Java parity: model/stats/calc/PlayerStatCalculator.
/// </summary>
public static class PlayerStatCalculator
{
    // Java parity: calculateMaxHp(PlayerClass, int)
    public static int CalculateMaxHp(Model.PlayerClass playerClass, int level)
    {
        int @base = playerClass.GetHealthMultiplier() / 2;
        float mod1 = 0.1075f * playerClass.GetHealthMultiplier();
        float mod2 = 0.002875f * playerClass.GetHealthMultiplier();
        return (int)(@base + level * mod1 + level * level * mod2);
    }

    // Java parity: calculateMaxMp(PlayerClass, int)
    public static int CalculateMaxMp(Model.PlayerClass playerClass, int level)
    {
        float @base = playerClass.GetWillMultiplier() * 0.35f;
        float mod1 = level * @base / 2f;
        float mod2 = level * level * playerClass.GetWillMultiplier() * 0.125f / 10000;
        return (int)(@base + mod1 + mod2);
    }

    // Java parity: calculateBlockEvasionOrParry(int)
    public static int CalculateBlockEvasionOrParry(int level) => (int)(62 + 12.4f * level);

    // Java parity: calculateMagicalAccuracy(int)
    public static int CalculateMagicalAccuracy(int level) => (int)(14.26f * level);

    // Java parity: calculatePhysicalAccuracy(int)
    public static int CalculatePhysicalAccuracy(int level) => 190 + 8 * level;

    // Java parity: calculateStrikeResist(int)
    public static int CalculateStrikeResist(int level) => level > 50 ? 6 * (level - 50) : 0;
}
