using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Model.Templates.Items.Enums;

/// <summary>
/// Item group (weapon/armor/accessory class + misc), each carrying valid equip slots,
/// sub-type/armor-type and required gathering/crafting skills.
/// Java parity: model/templates/item/enums/ItemGroup (@XmlType("item_group") @XmlEnum).
/// </summary>
/// <remarks>
/// Per-constant data lives in <see cref="ItemGroupExtensions"/>. Constants built with the no-arg
/// constructor (validEquipmentSlots=0, itemSubType=NONE, armorType=null, no skills) are omitted
/// from the data table; the getters fall back to those exact defaults.
/// </remarks>
public enum ItemGroup
{
    NONE,
    NOWEAPON,
    SWORD,
    GREATSWORD,
    EXTRACT_SWORD,
    DAGGER,
    MACE,
    ORB,
    SPELLBOOK,
    POLEARM,
    STAFF,
    BOW,
    HARP,
    GUN,
    CANNON,
    KEYBLADE,
    SHIELD,

    TORSO,
    GLOVE,
    SHOULDER,
    PANTS,
    SHOES,
    RB_TORSO,
    RB_GLOVE,
    RB_SHOULDER,
    RB_PANTS,
    RB_SHOES,
    CL_TORSO,
    CL_GLOVE,
    CL_SHOULDER,
    CL_PANTS,
    CL_SHOES,
    LT_TORSO,
    LT_GLOVE,
    LT_SHOULDER,
    LT_PANTS,
    LT_SHOES,
    CH_TORSO,
    CH_GLOVE,
    CH_SHOULDER,
    CH_PANTS,
    CH_SHOES,
    PL_TORSO,
    PL_GLOVE,
    PL_SHOULDER,
    PL_PANTS,
    PL_SHOES,

    EARRING,
    RING,
    NECKLACE,
    BELT,
    WING,
    PLUME,

    HEAD,
    LT_HEADS,
    CL_HEADS,
    CL_MULTISLOT,
    CL_SHIELD,

    POWER_SHARDS,
    STIGMA,
    // other
    ARROW,
    NPC_MACE, // keep it above TOOLHOES, for search picking it up
    TOOLRODS,
    TOOLHOES,
    TOOLPICKS,
    // non equip
    MANASTONE,
    SPECIAL_MANASTONE,
    RECIPE,
    ENCHANTMENT,
    PACK_SCROLL,
    FLUX,
    BALIC_EMOTION,
    BALIC_MATERIAL,
    RAWHIDE,
    SOULSTONE,
    GATHERABLE,
    GATHERABLE_BONUS,
    DROP_MATERIAL,
    COINS,
    MEDALS,
    QUEST,
    KEY,
    CRAFT_BOOST,
    TAMPERING,
    COMBINATION,
    SKILLBOOK,
    GODSTONE,
    STIGMA_SHARD,
}

public static class ItemGroupExtensions
{
    private readonly record struct Data(long Slots, ItemSubType Sub, ArmorType? Armor, int[] Skills);

    private static long Slot(ItemSlot s) => s.GetSlotIdMask();

    private static readonly Dictionary<ItemGroup, Data> Table = BuildTable();

    private static Dictionary<ItemGroup, Data> BuildTable()
    {
        // weapons share MAIN_OR_SUB unless noted
        long mainOrSub = Slot(ItemSlot.MAIN_OR_SUB);
        var t = new Dictionary<ItemGroup, Data>
        {
            [ItemGroup.NOWEAPON] = new(mainOrSub, ItemSubType.TWO_HAND, null, Array.Empty<int>()),
            [ItemGroup.SWORD] = new(mainOrSub, ItemSubType.ONE_HAND, null, new[] { 37, 44 }),
            [ItemGroup.GREATSWORD] = new(mainOrSub, ItemSubType.TWO_HAND, null, new[] { 51 }),
            [ItemGroup.DAGGER] = new(mainOrSub, ItemSubType.ONE_HAND, null, new[] { 66, 45 }),
            [ItemGroup.MACE] = new(mainOrSub, ItemSubType.ONE_HAND, null, new[] { 39, 46 }),
            [ItemGroup.ORB] = new(mainOrSub, ItemSubType.TWO_HAND, null, new[] { 111 }),
            [ItemGroup.SPELLBOOK] = new(mainOrSub, ItemSubType.TWO_HAND, null, new[] { 100 }),
            [ItemGroup.POLEARM] = new(mainOrSub, ItemSubType.TWO_HAND, null, new[] { 52 }),
            [ItemGroup.STAFF] = new(mainOrSub, ItemSubType.TWO_HAND, null, new[] { 89 }),
            [ItemGroup.BOW] = new(mainOrSub, ItemSubType.TWO_HAND, null, new[] { 53 }),
            [ItemGroup.HARP] = new(mainOrSub, ItemSubType.TWO_HAND, null, new[] { 124, 114 }),
            [ItemGroup.GUN] = new(mainOrSub, ItemSubType.ONE_HAND, null, new[] { 117, 112 }),
            [ItemGroup.CANNON] = new(mainOrSub, ItemSubType.TWO_HAND, null, new[] { 113 }),
            [ItemGroup.KEYBLADE] = new(mainOrSub, ItemSubType.TWO_HAND, null, new[] { 112, 115 }),
            [ItemGroup.SHIELD] = new(Slot(ItemSlot.SUB_HAND), ItemSubType.SHIELD, null, new[] { 43, 50 }),
        };

        // armor: { 103, 106 } for general/robe, { 40 } clothes, { 41, 48 } leather, { 42, 49 } chain, { 54 } plate
        AddArmor(t, ItemSubType.ALL_ARMOR, new[] { 103, 106 }, ItemGroup.TORSO, ItemGroup.GLOVE, ItemGroup.SHOULDER, ItemGroup.PANTS, ItemGroup.SHOES);
        AddArmor(t, ItemSubType.ROBE, new[] { 103, 106 }, ItemGroup.RB_TORSO, ItemGroup.RB_GLOVE, ItemGroup.RB_SHOULDER, ItemGroup.RB_PANTS, ItemGroup.RB_SHOES);
        AddArmor(t, ItemSubType.CLOTHES, new[] { 40 }, ItemGroup.CL_TORSO, ItemGroup.CL_GLOVE, ItemGroup.CL_SHOULDER, ItemGroup.CL_PANTS, ItemGroup.CL_SHOES);
        AddArmor(t, ItemSubType.LEATHER, new[] { 41, 48 }, ItemGroup.LT_TORSO, ItemGroup.LT_GLOVE, ItemGroup.LT_SHOULDER, ItemGroup.LT_PANTS, ItemGroup.LT_SHOES);
        AddArmor(t, ItemSubType.CHAIN, new[] { 42, 49 }, ItemGroup.CH_TORSO, ItemGroup.CH_GLOVE, ItemGroup.CH_SHOULDER, ItemGroup.CH_PANTS, ItemGroup.CH_SHOES);
        AddArmor(t, ItemSubType.PLATE, new[] { 54 }, ItemGroup.PL_TORSO, ItemGroup.PL_GLOVE, ItemGroup.PL_SHOULDER, ItemGroup.PL_PANTS, ItemGroup.PL_SHOES);

        // accessories (armorType = ACCESSORY, itemSubType = NONE)
        t[ItemGroup.EARRING] = new(Slot(ItemSlot.EARRINGS_LEFT) | Slot(ItemSlot.EARRINGS_RIGHT), ItemSubType.NONE, ArmorType.ACCESSORY, Array.Empty<int>());
        t[ItemGroup.RING] = new(Slot(ItemSlot.RING_LEFT) | Slot(ItemSlot.RING_RIGHT), ItemSubType.NONE, ArmorType.ACCESSORY, Array.Empty<int>());
        t[ItemGroup.NECKLACE] = new(Slot(ItemSlot.NECKLACE), ItemSubType.NONE, ArmorType.ACCESSORY, Array.Empty<int>());
        t[ItemGroup.BELT] = new(Slot(ItemSlot.WAIST), ItemSubType.NONE, ArmorType.ACCESSORY, Array.Empty<int>());
        t[ItemGroup.WING] = new(Slot(ItemSlot.WINGS), ItemSubType.WING, null, Array.Empty<int>());
        t[ItemGroup.PLUME] = new(Slot(ItemSlot.PLUME), ItemSubType.PLUME, null, Array.Empty<int>());

        t[ItemGroup.HEAD] = new(Slot(ItemSlot.HELMET), ItemSubType.NONE, ArmorType.ACCESSORY, Array.Empty<int>());
        t[ItemGroup.LT_HEADS] = new(Slot(ItemSlot.HELMET), ItemSubType.LEATHER, null, Array.Empty<int>());
        t[ItemGroup.CL_HEADS] = new(Slot(ItemSlot.HELMET), ItemSubType.CLOTHES, null, Array.Empty<int>());
        t[ItemGroup.CL_MULTISLOT] = new(Slot(ItemSlot.TORSO) | Slot(ItemSlot.PANTS), ItemSubType.CLOTHES, null, Array.Empty<int>());
        t[ItemGroup.CL_SHIELD] = new(Slot(ItemSlot.SUB_HAND), ItemSubType.NONE, ArmorType.ACCESSORY, Array.Empty<int>());

        t[ItemGroup.POWER_SHARDS] = new(Slot(ItemSlot.POWER_SHARD_RIGHT) | Slot(ItemSlot.POWER_SHARD_LEFT), ItemSubType.NONE, ArmorType.ACCESSORY, Array.Empty<int>());
        t[ItemGroup.STIGMA] = new(Slot(ItemSlot.ALL_STIGMA), ItemSubType.STIGMA, null, Array.Empty<int>());
        // other (ARROW carries sub=ARROW with slots 0; the rest are tools)
        t[ItemGroup.ARROW] = new(0, ItemSubType.ARROW, null, Array.Empty<int>());
        t[ItemGroup.NPC_MACE] = new(Slot(ItemSlot.MAIN_HAND), ItemSubType.ONE_HAND, null, Array.Empty<int>());
        t[ItemGroup.TOOLRODS] = new(mainOrSub, ItemSubType.TWO_HAND, null, Array.Empty<int>());
        t[ItemGroup.TOOLHOES] = new(Slot(ItemSlot.MAIN_HAND), ItemSubType.ONE_HAND, null, Array.Empty<int>());
        t[ItemGroup.TOOLPICKS] = new(mainOrSub, ItemSubType.TWO_HAND, null, Array.Empty<int>());
        return t;
    }

    private static void AddArmor(Dictionary<ItemGroup, Data> t, ItemSubType sub, int[] skills, params ItemGroup[] groups)
    {
        long torso = Slot(ItemSlot.TORSO), gloves = Slot(ItemSlot.GLOVES), shoulder = Slot(ItemSlot.SHOULDER),
             pants = Slot(ItemSlot.PANTS), boots = Slot(ItemSlot.BOOTS);
        long[] slots = { torso, gloves, shoulder, pants, boots }; // groups are passed TORSO,GLOVE,SHOULDER,PANTS,SHOES
        for (int i = 0; i < groups.Length; i++)
            t[groups[i]] = new(slots[i], sub, null, skills);
    }

    // Java parity: getValidEquipmentSlots()
    public static long GetValidEquipmentSlots(this ItemGroup group) =>
        Table.TryGetValue(group, out var d) ? d.Slots : 0;

    // Java parity: getItemSubType()
    public static ItemSubType GetItemSubType(this ItemGroup group) =>
        Table.TryGetValue(group, out var d) ? d.Sub : ItemSubType.NONE;

    // Java parity: getArmorType()
    public static ArmorType? GetArmorType(this ItemGroup group) =>
        Table.TryGetValue(group, out var d) ? d.Armor : null;

    // Java parity: getRequiredSkills()
    public static int[] GetRequiredSkills(this ItemGroup group) =>
        Table.TryGetValue(group, out var d) ? d.Skills : Array.Empty<int>();

    // Java parity: getEquipType() — ARMOR if armorType set, else delegates to itemSubType.
    public static EquipType GetEquipType(this ItemGroup group)
    {
        if (group.GetArmorType() != null)
            return EquipType.ARMOR;
        return group.GetItemSubType().GetEquipType();
    }
}
