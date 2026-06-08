namespace Aion.GameServer.Model.Templates.Item.Enums;

/// <summary>
/// Item sub-type, each carrying its armor and equip classification.
/// Java parity: model/templates/item/enums/ItemSubType (@XmlEnum).
/// </summary>
/// <remarks>
/// Java carries per-constant <c>armorType</c> (nullable) and <c>equipType</c>: the
/// ArmorType ctor sets equipType=ARMOR; the EquipType ctor leaves armorType=null.
/// Those live in <see cref="ItemSubTypeExtensions"/>.
/// </remarks>
public enum ItemSubType
{
    ALL_ARMOR,
    NONE,
    CHAIN,
    CLOTHES,
    LEATHER,
    PLATE,
    ROBE,
    SHIELD,
    ARROW,
    WING,
    ONE_HAND,
    TWO_HAND,
    STIGMA,
    PLUME,
}

public static class ItemSubTypeExtensions
{
    // Java parity: armorType ctor sets equipType = ARMOR; these constants are the ArmorType-backed ones.
    private static readonly Dictionary<ItemSubType, ArmorType> ArmorTypes = new()
    {
        [ItemSubType.ALL_ARMOR] = ArmorType.GENERAL,
        [ItemSubType.CHAIN] = ArmorType.GENERAL,
        [ItemSubType.CLOTHES] = ArmorType.GENERAL,
        [ItemSubType.LEATHER] = ArmorType.GENERAL,
        [ItemSubType.PLATE] = ArmorType.GENERAL,
        [ItemSubType.ROBE] = ArmorType.GENERAL,
        [ItemSubType.SHIELD] = ArmorType.GENERAL,
        [ItemSubType.WING] = ArmorType.GENERAL,
    };

    // Java parity: per-constant equipType.
    private static readonly Dictionary<ItemSubType, EquipType> EquipTypes = new()
    {
        [ItemSubType.NONE] = EquipType.NONE,
        [ItemSubType.ARROW] = EquipType.NONE,
        [ItemSubType.ONE_HAND] = EquipType.WEAPON,
        [ItemSubType.TWO_HAND] = EquipType.WEAPON,
        [ItemSubType.STIGMA] = EquipType.STIGMA,
        [ItemSubType.PLUME] = EquipType.PLUME,
    };

    // Java parity: getArmorType() — null for the EquipType-backed constants.
    public static ArmorType? GetArmorType(this ItemSubType subType) =>
        ArmorTypes.TryGetValue(subType, out var armor) ? armor : null;

    // Java parity: protected getEquipType() — ArmorType-backed constants are ARMOR.
    internal static EquipType GetEquipType(this ItemSubType subType) =>
        EquipTypes.TryGetValue(subType, out var equip) ? equip : EquipType.ARMOR;
}
