using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// Physical vs magical attack type of a weapon/item, with its element.
/// Java parity: model/templates/item/ItemAttackType (@XmlEnum).
/// </summary>
public enum ItemAttackType
{
    PHYSICAL,
    MAGICAL_EARTH,
    MAGICAL_WATER,
    MAGICAL_WIND,
    MAGICAL_FIRE,
}

public static class ItemAttackTypeExtensions
{
    // Java parity: isMagical()
    public static bool IsMagical(this ItemAttackType type) => type switch
    {
        ItemAttackType.PHYSICAL => false,
        _ => true,
    };

    // Java parity: getMagicalElement()
    public static SkillElement GetMagicalElement(this ItemAttackType type) => type switch
    {
        ItemAttackType.MAGICAL_EARTH => SkillElement.EARTH,
        ItemAttackType.MAGICAL_WATER => SkillElement.WATER,
        ItemAttackType.MAGICAL_WIND => SkillElement.WIND,
        ItemAttackType.MAGICAL_FIRE => SkillElement.FIRE,
        _ => SkillElement.NONE,
    };
}
