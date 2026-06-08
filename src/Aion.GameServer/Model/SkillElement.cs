using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model;

/// <summary>
/// Elemental attack/resistance types and their backing resistance stat.
/// Java parity: model/SkillElement.
/// </summary>
public enum SkillElement
{
    NONE,
    FIRE,
    WATER,
    WIND,
    EARTH,
    LIGHT,
    DARK,
}

public static class SkillElementExtensions
{
    // Java parity: getStatForElement() — NONE maps to no stat (null).
    public static StatEnum? GetStatForElement(this SkillElement element) => element switch
    {
        SkillElement.FIRE => StatEnum.FIRE_RESISTANCE,
        SkillElement.WATER => StatEnum.WATER_RESISTANCE,
        SkillElement.WIND => StatEnum.WIND_RESISTANCE,
        SkillElement.EARTH => StatEnum.EARTH_RESISTANCE,
        SkillElement.LIGHT => StatEnum.LIGHT_RESISTANCE,
        SkillElement.DARK => StatEnum.DARK_RESISTANCE,
        _ => null,
    };
}
