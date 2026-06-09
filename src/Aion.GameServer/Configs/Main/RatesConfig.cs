namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/RatesConfig (Neon). SCREAMING_SNAKE field names + @Property defaults.
/// Each float[] is a {regular, premium} pair parsed from a comma-separated @Property default.
/// </summary>
public static class RatesConfig
{
    // Success rates
    /// <summary>Key: gameserver.rates.crafting.crit_chances</summary>
    public static float[] CRAFT_CRIT_CHANCES = { 15.0f, 30.0f };

    /// <summary>Key: gameserver.rates.crafting.combo_crit_chances</summary>
    public static float[] CRAFT_COMBO_CHANCES = { 25.0f, 50.0f };

    /// <summary>Key: gameserver.rates.manastone_chances</summary>
    public static float[] MANASTONE_CHANCES = { 75.0f, 75.0f };

    /// <summary>Key: gameserver.rates.enchantment_stone.base_chances</summary>
    public static float[] ENCHANTMENT_STONE_BASE_CHANCES = { 65.0f, 65.0f };

    /// <summary>Key: gameserver.rates.enchantment_stone.amplified_chances</summary>
    public static float[] ENCHANTMENT_STONE_AMPLIFIED_CHANCES = { 50.0f, 50.0f };

    /// <summary>Key: gameserver.rates.tampering_chances</summary>
    public static float[] TEMPERING_CHANCES = { 65.0f, 65.0f };

    // Modifiers
    /// <summary>Key: gameserver.rates.xp.solo</summary>
    public static float[] XP_SOLO_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.xp.group</summary>
    public static float[] XP_GROUP_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.xp.quest</summary>
    public static float[] XP_QUEST_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.xp.gathering</summary>
    public static float[] XP_GATHERING_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.xp.crafting</summary>
    public static float[] XP_CRAFTING_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.xp.pvp</summary>
    public static float[] XP_PVP_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.skill_xp.gathering</summary>
    public static float[] SKILL_XP_GATHERING_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.skill_xp.crafting</summary>
    public static float[] SKILL_XP_CRAFTING_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.ap.pvp.gain</summary>
    public static float[] AP_PVP_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.ap.pvp.loss</summary>
    public static float[] AP_PVP_LOSS_RATES = { 1.0f, 1.0f };

    /// <summary>Key: gameserver.rates.ap.pve</summary>
    public static float[] AP_PVE_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.ap.quest</summary>
    public static float[] AP_QUEST_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.ap.dredgion</summary>
    public static float[] AP_DREDGION_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.gp.gain</summary>
    public static float[] GP_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.dp.pve</summary>
    public static float[] DP_PVE_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.dp.pvp</summary>
    public static float[] DP_PVP_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.kinah.quest</summary>
    public static float[] QUEST_KINAH_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.drop</summary>
    public static float[] DROP_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.gathering.count</summary>
    public static float[] GATHERING_COUNT_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.pvparena.discipline</summary>
    public static float[] PVP_ARENA_DISCIPLINE_REWARD_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.pvparena.chaos</summary>
    public static float[] PVP_ARENA_CHAOS_REWARD_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.pvparena.harmony</summary>
    public static float[] PVP_ARENA_HARMONY_REWARD_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.pvparena.glory</summary>
    public static float[] PVP_ARENA_GLORY_REWARD_RATES = { 1.0f, 2.0f };

    /// <summary>Key: gameserver.rates.sell_limit</summary>
    public static float[] SELL_LIMIT_RATES = { 1.0f, 2.0f };
}
