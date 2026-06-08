namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Random-item bag category (enchantment/manastone grades, ancient items, chunks, etc.).
/// Java parity: model/templates/item/RandomType (@XmlEnum).
/// </summary>
/// <remarks>Per-constant <c>level</c> (default 0) lives in <see cref="RandomTypeExtensions"/>.</remarks>
public enum RandomType
{
    ENCHANTMENT,
    MANASTONE,
    MANASTONE_COMMON_GRADE_10,
    MANASTONE_COMMON_GRADE_20,
    MANASTONE_COMMON_GRADE_30,
    MANASTONE_COMMON_GRADE_40,
    MANASTONE_COMMON_GRADE_50,
    MANASTONE_COMMON_GRADE_60,
    MANASTONE_COMMON_GRADE_70,
    MANASTONE_RARE_GRADE_10,
    MANASTONE_RARE_GRADE_20,
    MANASTONE_RARE_GRADE_30,
    MANASTONE_RARE_GRADE_40,
    MANASTONE_RARE_GRADE_50,
    MANASTONE_RARE_GRADE_60,
    MANASTONE_RARE_GRADE_70,
    MANASTONE_LEGEND_GRADE_10,
    MANASTONE_LEGEND_GRADE_20,
    MANASTONE_LEGEND_GRADE_30,
    MANASTONE_LEGEND_GRADE_40,
    MANASTONE_LEGEND_GRADE_50,
    MANASTONE_LEGEND_GRADE_60,
    MANASTONE_LEGEND_GRADE_70,
    SPECIAL_MANASTONE_RARE_GRADE,
    SPECIAL_MANASTONE_LEGEND_GRADE,
    SPECIAL_MANASTONE_UNIQUE_GRADE,
    SPECIAL_MANASTONE_EPIC_GRADE,
    ANCIENTITEMS,
    ANCIENT_CROWN,
    ANCIENT_GOBLET,
    ANCIENT_SEAL,
    ANCIENT_ICON,
    CHUNK_EARTH,
    CHUNK_ROCK,
    CHUNK_SAND,
    CHUNK_GEMSTONE,
    SCROLLS,
    POTION,
    LESSER_POTIONS,
    POTION_50,
    ILLUSION_GODSTONE,
    PREMIUM_OPHIDAN_RECIPE,
}

public static class RandomTypeExtensions
{
    // Java parity: per-constant level (constants not listed default to 0).
    private static readonly Dictionary<RandomType, int> Levels = new()
    {
        [RandomType.MANASTONE_COMMON_GRADE_10] = 10,
        [RandomType.MANASTONE_COMMON_GRADE_20] = 20,
        [RandomType.MANASTONE_COMMON_GRADE_30] = 30,
        [RandomType.MANASTONE_COMMON_GRADE_40] = 40,
        [RandomType.MANASTONE_COMMON_GRADE_50] = 50,
        [RandomType.MANASTONE_COMMON_GRADE_60] = 60,
        [RandomType.MANASTONE_COMMON_GRADE_70] = 70,
        [RandomType.MANASTONE_RARE_GRADE_10] = 10,
        [RandomType.MANASTONE_RARE_GRADE_20] = 20,
        [RandomType.MANASTONE_RARE_GRADE_30] = 30,
        [RandomType.MANASTONE_RARE_GRADE_40] = 40,
        [RandomType.MANASTONE_RARE_GRADE_50] = 50,
        [RandomType.MANASTONE_RARE_GRADE_60] = 60,
        [RandomType.MANASTONE_RARE_GRADE_70] = 70,
        [RandomType.MANASTONE_LEGEND_GRADE_10] = 10,
        [RandomType.MANASTONE_LEGEND_GRADE_20] = 20,
        [RandomType.MANASTONE_LEGEND_GRADE_30] = 30,
        [RandomType.MANASTONE_LEGEND_GRADE_40] = 40,
        [RandomType.MANASTONE_LEGEND_GRADE_50] = 50,
        [RandomType.MANASTONE_LEGEND_GRADE_60] = 60,
        [RandomType.MANASTONE_LEGEND_GRADE_70] = 70,
        [RandomType.SPECIAL_MANASTONE_RARE_GRADE] = 70,
        [RandomType.SPECIAL_MANASTONE_LEGEND_GRADE] = 70,
        [RandomType.SPECIAL_MANASTONE_UNIQUE_GRADE] = 70,
        [RandomType.SPECIAL_MANASTONE_EPIC_GRADE] = 70,
    };

    // Java parity: getLevel()
    public static int GetLevel(this RandomType type) => Levels.TryGetValue(type, out int lvl) ? lvl : 0;
}
