using System;
using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Broker.Filter;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Broker;

/// <summary>Java parity: model/broker/BrokerItemMask (kosyachok, Simple, ATracer). Enum w/(typeId,BrokerFilter,parent,childrenExist) per member→enum + BrokerItemMaskExtensions (static MaskData dict, self-referencing parent). Broker*Filter classes + Item/PlayerClass red-tolerated.</summary>
public enum BrokerItemMask
{
    WEAPON, WEAPON_SWORD, WEAPON_MACE, WEAPON_DAGGER, WEAPON_ORB, WEAPON_SPELLBOOK, WEAPON_GREATSWORD,
    WEAPON_POLEARM, WEAPON_STAFF, WEAPON_BOW, WEAPON_PISTOL, WEAPON_AETHERCANNON, WEAPON_GARP, WEAPON_KEYBLADE,
    ARMOR, ARMOR_CLOTHING, ARMOR_CLOTHING_JACKET, ARMOR_CLOTHING_GLOVES, ARMOR_CLOTHING_PAULDRONS,
    ARMOR_CLOTHING_PANTS, ARMOR_CLOTHING_SHOES, ARMOR_CLOTH, ARMOR_CLOTH_JACKET, ARMOR_CLOTH_GLOVES,
    ARMOR_CLOTH_PAULDRONS, ARMOR_CLOTH_PANTS, ARMOR_CLOTH_SHOES, ARMOR_LEATHER, ARMOR_LEATHER_JACKET,
    ARMOR_LEATHER_GLOVES, ARMOR_LEATHER_PAULDRONS, ARMOR_LEATHER_PANTS, ARMOR_LEATHER_SHOES, ARMOR_CHAIN,
    ARMOR_CHAIN_JACKET, ARMOR_CHAIN_GLOVES, ARMOR_CHAIN_PAULDRONS, ARMOR_CHAIN_PANTS, ARMOR_CHAIN_SHOES,
    ARMOR_PLATE, ARMOR_PLATE_JACKET, ARMOR_PLATE_GLOVES, ARMOR_PLATE_PAULDRONS, ARMOR_PLATE_PANTS,
    ARMOR_PLATE_SHOES, ARMOR_SHIELD,
    ACCESSORY, ACCESSORY_EARRINGS, ACCESSORY_NECKLACE, ACCESSORY_RING, ACCESSORY_BELT, ACCESSORY_HEADGEAR,
    ACCESSORY_PLUME,
    SKILL_RELATED, SKILL_RELATED_STIGMA, SKILL_RELATED_STIGMA_GLADIATOR, SKILL_RELATED_STIGMA_TEMPLAR,
    SKILL_RELATED_STIGMA_ASSASSIN, SKILL_RELATED_STIGMA_RANGER, SKILL_RELATED_STIGMA_SORCERER,
    SKILL_RELATED_STIGMA_SPIRITMASTER, SKILL_RELATED_STIGMA_CLERIC, SKILL_RELATED_STIGMA_CHANTER,
    SKILL_RELATED_STIGMA_GUNSLINGER, SKILL_RELATED_STIGMA_SONGWEAVER, SKILL_RELATED_STIGMA_RIDER,
    SKILL_RELATED_SKILL_MANUAL, SKILL_RELATED_SKILL_MANUAL_GLADIATOR, SKILL_RELATED_SKILL_MANUAL_TEMPLAR,
    SKILL_RELATED_SKILL_MANUAL_ASSASSIN, SKILL_RELATED_SKILL_MANUAL_RANGER, SKILL_RELATED_SKILL_MANUAL_SORCERER,
    SKILL_RELATED_SKILL_MANUAL_SPIRITMASTER, SKILL_RELATED_SKILL_MANUAL_CLERIC, SKILL_RELATED_SKILL_MANUAL_CHANTER,
    SKILL_RELATED_SKILL_MANUAL_GUNSLINGER, SKILL_RELATED_SKILL_MANUAL_SONGWEAVER, SKILL_RELATED_SKILL_MANUAL_RIDER,
    HOME_DECOR, HOME_DECOR_OUT_DOOR, HOME_DECOR_IN_DOOR,
    FURNITURE, FURNITURE_OUT_DOOR, FURNITURE_IN_DOOR, FURNITURE_IN_DOOR_WALL_MOUNTED,
    FURNITURE_IN_DOOR_FREE_STANDING, FURNITURE_IN_DOOR_RUGS, FURNITURE_IN_DOOR_OUT_DOOR,
    CRAFT, CRAFT_MATERIALS, CRAFT_MATERIALS_GATHERED, CRAFT_MATERIALS_LOOTED, CRAFT_MATERIALS_COMPONENTS,
    CRAFT_DESIGN, CRAFT_DESIGN_WEAPONSMITHING, CRAFT_DESIGN_ARMORSMITHING, CRAFT_DESIGN_TAILORING,
    CRAFT_DESIGN_HANDICRAFTING, CRAFT_DESIGN_ALCHEMY, CRAFT_DESIGN_COOKING, CRAFT_DESIGN_CONSTRUCTION,
    CONSUMABLES, CONSUMABLES_FOOD, CONSUMABLES_POTION, CONSUMABLES_SCROLL, CONSUMABLES_MODIFY,
    CONSUMABLES_MODIFY_ENCHANTMENT_STONE, CONSUMABLES_MODIFY_MANASTONE, CONSUMABLES_MODIFY_TEMPERING_SOLUTION,
    CONSUMABLES_MODIFY_GODSTONE, CONSUMABLES_MODIFY_DYE, CONSUMABLES_MODIFY_PAIN,
    CONSUMABLES_MODIFY_AMPLIFICATION_STONE, CONSUMABLES_MODIFY_OTHER, CONSUMABLES_OTHER,
    OTHER,
    UNKNOWN
}

public static class BrokerItemMaskExtensions
{
    private readonly struct MaskData
    {
        public readonly int TypeId;
        public readonly BrokerFilter Filter;
        public readonly BrokerItemMask? Parent;
        public readonly bool ChildrenExist;

        public MaskData(int typeId, BrokerFilter filter, BrokerItemMask? parent, bool childrenExist)
        {
            TypeId = typeId;
            Filter = filter;
            Parent = parent;
            ChildrenExist = childrenExist;
        }
    }

    private static readonly Dictionary<BrokerItemMask, MaskData> data = new()
    {
        // Weapon Section + sub categories
        [BrokerItemMask.WEAPON] = new MaskData(9010, new BrokerMinMaxFilter(1000, 1021), null, true),
        [BrokerItemMask.WEAPON_SWORD] = new MaskData(1000, new BrokerContainsFilter(1000), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_MACE] = new MaskData(1001, new BrokerContainsFilter(1001), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_DAGGER] = new MaskData(1002, new BrokerContainsFilter(1002), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_ORB] = new MaskData(1005, new BrokerContainsFilter(1005), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_SPELLBOOK] = new MaskData(1006, new BrokerContainsFilter(1006), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_GREATSWORD] = new MaskData(1009, new BrokerContainsFilter(1009), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_POLEARM] = new MaskData(1013, new BrokerContainsFilter(1013), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_STAFF] = new MaskData(1015, new BrokerContainsFilter(1015), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_BOW] = new MaskData(1017, new BrokerContainsFilter(1017), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_PISTOL] = new MaskData(1018, new BrokerContainsFilter(1018), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_AETHERCANNON] = new MaskData(1019, new BrokerContainsFilter(1019), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_GARP] = new MaskData(1020, new BrokerContainsFilter(1020), BrokerItemMask.WEAPON, false),
        [BrokerItemMask.WEAPON_KEYBLADE] = new MaskData(1021, new BrokerContainsFilter(1021), BrokerItemMask.WEAPON, false),
        // Armor Section + sub categories
        [BrokerItemMask.ARMOR] = new MaskData(9020, new BrokerMinMaxFilter(1100, 1150), null, true),
        [BrokerItemMask.ARMOR_CLOTHING] = new MaskData(8010, new BrokerContainsFilter(1100, 1110, 1120, 1130, 1140), BrokerItemMask.ARMOR, true),
        [BrokerItemMask.ARMOR_CLOTHING_JACKET] = new MaskData(1100, new BrokerContainsFilter(1100), BrokerItemMask.ARMOR_CLOTHING, false),
        [BrokerItemMask.ARMOR_CLOTHING_GLOVES] = new MaskData(1110, new BrokerContainsFilter(1110), BrokerItemMask.ARMOR_CLOTHING, false),
        [BrokerItemMask.ARMOR_CLOTHING_PAULDRONS] = new MaskData(1120, new BrokerContainsFilter(1120), BrokerItemMask.ARMOR_CLOTHING, false),
        [BrokerItemMask.ARMOR_CLOTHING_PANTS] = new MaskData(1130, new BrokerContainsFilter(1130), BrokerItemMask.ARMOR_CLOTHING, false),
        [BrokerItemMask.ARMOR_CLOTHING_SHOES] = new MaskData(1140, new BrokerContainsFilter(1140), BrokerItemMask.ARMOR_CLOTHING, false),
        [BrokerItemMask.ARMOR_CLOTH] = new MaskData(8020, new BrokerContainsFilter(1101, 1111, 1121, 1131, 1141), BrokerItemMask.ARMOR, true),
        [BrokerItemMask.ARMOR_CLOTH_JACKET] = new MaskData(1101, new BrokerContainsFilter(1101), BrokerItemMask.ARMOR_CLOTH, false),
        [BrokerItemMask.ARMOR_CLOTH_GLOVES] = new MaskData(1111, new BrokerContainsFilter(1111), BrokerItemMask.ARMOR_CLOTH, false),
        [BrokerItemMask.ARMOR_CLOTH_PAULDRONS] = new MaskData(1121, new BrokerContainsFilter(1121), BrokerItemMask.ARMOR_CLOTH, false),
        [BrokerItemMask.ARMOR_CLOTH_PANTS] = new MaskData(1131, new BrokerContainsFilter(1131), BrokerItemMask.ARMOR_CLOTH, false),
        [BrokerItemMask.ARMOR_CLOTH_SHOES] = new MaskData(1141, new BrokerContainsFilter(1141), BrokerItemMask.ARMOR_CLOTH, false),
        [BrokerItemMask.ARMOR_LEATHER] = new MaskData(8030, new BrokerContainsFilter(1103, 1113, 1123, 1133, 1143), BrokerItemMask.ARMOR, true),
        [BrokerItemMask.ARMOR_LEATHER_JACKET] = new MaskData(1103, new BrokerContainsFilter(1103), BrokerItemMask.ARMOR_LEATHER, false),
        [BrokerItemMask.ARMOR_LEATHER_GLOVES] = new MaskData(1113, new BrokerContainsFilter(1113), BrokerItemMask.ARMOR_LEATHER, false),
        [BrokerItemMask.ARMOR_LEATHER_PAULDRONS] = new MaskData(1123, new BrokerContainsFilter(1123), BrokerItemMask.ARMOR_LEATHER, false),
        [BrokerItemMask.ARMOR_LEATHER_PANTS] = new MaskData(1133, new BrokerContainsFilter(1133), BrokerItemMask.ARMOR_LEATHER, false),
        [BrokerItemMask.ARMOR_LEATHER_SHOES] = new MaskData(1143, new BrokerContainsFilter(1143), BrokerItemMask.ARMOR_LEATHER, false),
        [BrokerItemMask.ARMOR_CHAIN] = new MaskData(8040, new BrokerContainsFilter(1105, 1115, 1125, 1135, 1145), BrokerItemMask.ARMOR, true),
        [BrokerItemMask.ARMOR_CHAIN_JACKET] = new MaskData(1105, new BrokerContainsFilter(1105), BrokerItemMask.ARMOR_CHAIN, false),
        [BrokerItemMask.ARMOR_CHAIN_GLOVES] = new MaskData(1115, new BrokerContainsFilter(1115), BrokerItemMask.ARMOR_CHAIN, false),
        [BrokerItemMask.ARMOR_CHAIN_PAULDRONS] = new MaskData(1125, new BrokerContainsFilter(1125), BrokerItemMask.ARMOR_CHAIN, false),
        [BrokerItemMask.ARMOR_CHAIN_PANTS] = new MaskData(1135, new BrokerContainsFilter(1135), BrokerItemMask.ARMOR_CHAIN, false),
        [BrokerItemMask.ARMOR_CHAIN_SHOES] = new MaskData(1145, new BrokerContainsFilter(1145), BrokerItemMask.ARMOR_CHAIN, false),
        [BrokerItemMask.ARMOR_PLATE] = new MaskData(8050, new BrokerContainsFilter(1106, 1116, 1126, 1136, 1146), BrokerItemMask.ARMOR, true),
        [BrokerItemMask.ARMOR_PLATE_JACKET] = new MaskData(1106, new BrokerContainsFilter(1106), BrokerItemMask.ARMOR_PLATE, false),
        [BrokerItemMask.ARMOR_PLATE_GLOVES] = new MaskData(1116, new BrokerContainsFilter(1116), BrokerItemMask.ARMOR_PLATE, false),
        [BrokerItemMask.ARMOR_PLATE_PAULDRONS] = new MaskData(1126, new BrokerContainsFilter(1126), BrokerItemMask.ARMOR_PLATE, false),
        [BrokerItemMask.ARMOR_PLATE_PANTS] = new MaskData(1136, new BrokerContainsFilter(1136), BrokerItemMask.ARMOR_PLATE, false),
        [BrokerItemMask.ARMOR_PLATE_SHOES] = new MaskData(1146, new BrokerContainsFilter(1146), BrokerItemMask.ARMOR_PLATE, false),
        [BrokerItemMask.ARMOR_SHIELD] = new MaskData(1150, new BrokerContainsFilter(1150), BrokerItemMask.ARMOR, false),
        // Accessory Section + sub categories
        [BrokerItemMask.ACCESSORY] = new MaskData(9030, new BrokerContainsFilter(1200, 1210, 1220, 1230, 1250, 1871), null, true),
        [BrokerItemMask.ACCESSORY_EARRINGS] = new MaskData(1200, new BrokerContainsFilter(1200), BrokerItemMask.ACCESSORY, false),
        [BrokerItemMask.ACCESSORY_NECKLACE] = new MaskData(1210, new BrokerContainsFilter(1210), BrokerItemMask.ACCESSORY, false),
        [BrokerItemMask.ACCESSORY_RING] = new MaskData(1220, new BrokerContainsFilter(1220), BrokerItemMask.ACCESSORY, false),
        [BrokerItemMask.ACCESSORY_BELT] = new MaskData(1230, new BrokerContainsFilter(1230), BrokerItemMask.ACCESSORY, false),
        [BrokerItemMask.ACCESSORY_HEADGEAR] = new MaskData(7030, new BrokerContainsFilter(1250), BrokerItemMask.ACCESSORY, false),
        [BrokerItemMask.ACCESSORY_PLUME] = new MaskData(1871, new BrokerContainsFilter(1871), BrokerItemMask.ACCESSORY, false),
        // Skill related Section + sub categories
        [BrokerItemMask.SKILL_RELATED] = new MaskData(9040, new BrokerContainsFilter(1400, 1695), null, true),
        [BrokerItemMask.SKILL_RELATED_STIGMA] = new MaskData(1400, new BrokerContainsFilter(1400), BrokerItemMask.SKILL_RELATED, true),
        [BrokerItemMask.SKILL_RELATED_STIGMA_GLADIATOR] = new MaskData(6010, new BrokerPlayerClassExtraFilter(1400, PlayerClass.GLADIATOR), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_STIGMA_TEMPLAR] = new MaskData(6011, new BrokerPlayerClassExtraFilter(1400, PlayerClass.TEMPLAR), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_STIGMA_ASSASSIN] = new MaskData(6012, new BrokerPlayerClassExtraFilter(1400, PlayerClass.ASSASSIN), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_STIGMA_RANGER] = new MaskData(6013, new BrokerPlayerClassExtraFilter(1400, PlayerClass.RANGER), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_STIGMA_SORCERER] = new MaskData(6014, new BrokerPlayerClassExtraFilter(1400, PlayerClass.SORCERER), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_STIGMA_SPIRITMASTER] = new MaskData(6015, new BrokerPlayerClassExtraFilter(1400, PlayerClass.SPIRIT_MASTER), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_STIGMA_CLERIC] = new MaskData(6016, new BrokerPlayerClassExtraFilter(1400, PlayerClass.CLERIC), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_STIGMA_CHANTER] = new MaskData(6017, new BrokerPlayerClassExtraFilter(1400, PlayerClass.CHANTER), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_STIGMA_GUNSLINGER] = new MaskData(6018, new BrokerPlayerClassExtraFilter(1400, PlayerClass.GUNNER), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_STIGMA_SONGWEAVER] = new MaskData(6019, new BrokerPlayerClassExtraFilter(1400, PlayerClass.BARD), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_STIGMA_RIDER] = new MaskData(6048, new BrokerPlayerClassExtraFilter(1400, PlayerClass.RIDER), BrokerItemMask.SKILL_RELATED_STIGMA, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL] = new MaskData(1695, new BrokerContainsFilter(1695), BrokerItemMask.SKILL_RELATED, true),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_GLADIATOR] = new MaskData(6020, new BrokerPlayerClassExtraFilter(1695, PlayerClass.GLADIATOR), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_TEMPLAR] = new MaskData(6021, new BrokerPlayerClassExtraFilter(1695, PlayerClass.TEMPLAR), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_ASSASSIN] = new MaskData(6022, new BrokerPlayerClassExtraFilter(1695, PlayerClass.ASSASSIN), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_RANGER] = new MaskData(6023, new BrokerPlayerClassExtraFilter(1695, PlayerClass.RANGER), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_SORCERER] = new MaskData(6024, new BrokerPlayerClassExtraFilter(1695, PlayerClass.SORCERER), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_SPIRITMASTER] = new MaskData(6025, new BrokerPlayerClassExtraFilter(1695, PlayerClass.SPIRIT_MASTER), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_CLERIC] = new MaskData(6026, new BrokerPlayerClassExtraFilter(1695, PlayerClass.CLERIC), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_CHANTER] = new MaskData(6027, new BrokerPlayerClassExtraFilter(1695, PlayerClass.CHANTER), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_GUNSLINGER] = new MaskData(6028, new BrokerPlayerClassExtraFilter(1695, PlayerClass.GUNNER), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_SONGWEAVER] = new MaskData(6029, new BrokerPlayerClassExtraFilter(1695, PlayerClass.BARD), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        [BrokerItemMask.SKILL_RELATED_SKILL_MANUAL_RIDER] = new MaskData(6049, new BrokerPlayerClassExtraFilter(1695, PlayerClass.RIDER), BrokerItemMask.SKILL_RELATED_SKILL_MANUAL, false),
        // Home Decor Section + sub categories
        [BrokerItemMask.HOME_DECOR] = new MaskData(9070, new BrokerContainsFilter(1710, 1711), null, true),
        [BrokerItemMask.HOME_DECOR_OUT_DOOR] = new MaskData(1710, new BrokerContainsFilter(1710), BrokerItemMask.HOME_DECOR, false),
        [BrokerItemMask.HOME_DECOR_IN_DOOR] = new MaskData(1711, new BrokerContainsFilter(1711), BrokerItemMask.HOME_DECOR, false),
        // Furniture Section + sub categories
        [BrokerItemMask.FURNITURE] = new MaskData(9080, new BrokerContainsFilter(1700, 1701, 1702, 1703, 1704), null, true),
        [BrokerItemMask.FURNITURE_OUT_DOOR] = new MaskData(1703, new BrokerContainsFilter(1703), BrokerItemMask.FURNITURE, false),
        [BrokerItemMask.FURNITURE_IN_DOOR] = new MaskData(8070, new BrokerContainsFilter(1700, 1701, 1702), BrokerItemMask.FURNITURE, true),
        [BrokerItemMask.FURNITURE_IN_DOOR_WALL_MOUNTED] = new MaskData(1700, new BrokerContainsFilter(1700), BrokerItemMask.FURNITURE_IN_DOOR, false),
        [BrokerItemMask.FURNITURE_IN_DOOR_FREE_STANDING] = new MaskData(1701, new BrokerContainsFilter(1701), BrokerItemMask.FURNITURE_IN_DOOR, false),
        [BrokerItemMask.FURNITURE_IN_DOOR_RUGS] = new MaskData(1702, new BrokerContainsFilter(1702), BrokerItemMask.FURNITURE_IN_DOOR, false),
        [BrokerItemMask.FURNITURE_IN_DOOR_OUT_DOOR] = new MaskData(1704, new BrokerContainsFilter(1704), BrokerItemMask.FURNITURE, false),
        // Craft Section + sub categories
        [BrokerItemMask.CRAFT] = new MaskData(9050, new BrokerContainsFilter(1520, 1522), null, true),
        [BrokerItemMask.CRAFT_MATERIALS] = new MaskData(1520, new BrokerContainsFilter(1520), BrokerItemMask.CRAFT, true),
        [BrokerItemMask.CRAFT_MATERIALS_GATHERED] = new MaskData(6030, new BrokerContainsExtraFilter(15200), BrokerItemMask.CRAFT_MATERIALS, false),
        [BrokerItemMask.CRAFT_MATERIALS_LOOTED] = new MaskData(6031, new BrokerContainsExtraFilter(15201), BrokerItemMask.CRAFT_MATERIALS, false),
        [BrokerItemMask.CRAFT_MATERIALS_COMPONENTS] = new MaskData(6032, new BrokerContainsExtraFilter(15202), BrokerItemMask.CRAFT_MATERIALS, false),
        [BrokerItemMask.CRAFT_DESIGN] = new MaskData(1522, new BrokerContainsFilter(1522), BrokerItemMask.CRAFT, true),
        [BrokerItemMask.CRAFT_DESIGN_WEAPONSMITHING] = new MaskData(6040, new BrokerRecipeFilter(40002, 1522), BrokerItemMask.CRAFT_DESIGN, false),
        [BrokerItemMask.CRAFT_DESIGN_ARMORSMITHING] = new MaskData(6041, new BrokerRecipeFilter(40003, 1522), BrokerItemMask.CRAFT_DESIGN, false),
        [BrokerItemMask.CRAFT_DESIGN_TAILORING] = new MaskData(6042, new BrokerRecipeFilter(40004, 1522), BrokerItemMask.CRAFT_DESIGN, false),
        [BrokerItemMask.CRAFT_DESIGN_HANDICRAFTING] = new MaskData(6043, new BrokerRecipeFilter(40008, 1522), BrokerItemMask.CRAFT_DESIGN, false),
        [BrokerItemMask.CRAFT_DESIGN_ALCHEMY] = new MaskData(6044, new BrokerRecipeFilter(40007, 1522), BrokerItemMask.CRAFT_DESIGN, false),
        [BrokerItemMask.CRAFT_DESIGN_COOKING] = new MaskData(6045, new BrokerRecipeFilter(40001, 1522), BrokerItemMask.CRAFT_DESIGN, false),
        [BrokerItemMask.CRAFT_DESIGN_CONSTRUCTION] = new MaskData(6046, new BrokerRecipeFilter(40010, 1522), BrokerItemMask.CRAFT_DESIGN, false),
        // Consumables Section + sub categories
        [BrokerItemMask.CONSUMABLES] = new MaskData(9060, new BrokerContainsFilter(1410, 1600, 1620, 1640, 1660, 1661, 1665, 1670, 1680, 1690, 1692, 1693, 1694, 1696), null, true),
        [BrokerItemMask.CONSUMABLES_FOOD] = new MaskData(1600, new BrokerContainsFilter(1600), BrokerItemMask.CONSUMABLES, false),
        [BrokerItemMask.CONSUMABLES_POTION] = new MaskData(1620, new BrokerContainsFilter(1620), BrokerItemMask.CONSUMABLES, false),
        [BrokerItemMask.CONSUMABLES_SCROLL] = new MaskData(7060, new BrokerContainsFilter(1640), BrokerItemMask.CONSUMABLES, false),
        [BrokerItemMask.CONSUMABLES_MODIFY] = new MaskData(8060, new BrokerContainsFilter(1660, 1665, 1670, 1680, 1692, 1691), BrokerItemMask.CONSUMABLES, true),
        [BrokerItemMask.CONSUMABLES_MODIFY_ENCHANTMENT_STONE] = new MaskData(1660, new BrokerContainsExtraFilter(16600, 16602), BrokerItemMask.CONSUMABLES_MODIFY, false),
        [BrokerItemMask.CONSUMABLES_MODIFY_MANASTONE] = new MaskData(1670, new BrokerContainsFilter(1670), BrokerItemMask.CONSUMABLES_MODIFY, false),
        [BrokerItemMask.CONSUMABLES_MODIFY_TEMPERING_SOLUTION] = new MaskData(7065, new BrokerContainsExtraFilter(16603), BrokerItemMask.CONSUMABLES_MODIFY, false),
        [BrokerItemMask.CONSUMABLES_MODIFY_GODSTONE] = new MaskData(1680, new BrokerContainsFilter(1680), BrokerItemMask.CONSUMABLES_MODIFY, false),
        [BrokerItemMask.CONSUMABLES_MODIFY_DYE] = new MaskData(7061, new BrokerContainsFilter(1692), BrokerItemMask.CONSUMABLES_MODIFY, false),
        [BrokerItemMask.CONSUMABLES_MODIFY_PAIN] = new MaskData(7064, new BrokerContainsFilter(1691), BrokerItemMask.CONSUMABLES_MODIFY, false),
        [BrokerItemMask.CONSUMABLES_MODIFY_AMPLIFICATION_STONE] = new MaskData(1665, new BrokerContainsFilter(1665), BrokerItemMask.CONSUMABLES_MODIFY, false),
        [BrokerItemMask.CONSUMABLES_MODIFY_OTHER] = new MaskData(7063, new BrokerContainsFilter(1661), BrokerItemMask.CONSUMABLES_MODIFY, false),
        [BrokerItemMask.CONSUMABLES_OTHER] = new MaskData(7062, new BrokerContainsFilter(1410, 1690, 1693, 1694, 1696), BrokerItemMask.CONSUMABLES, false),
        // Other Section
        [BrokerItemMask.OTHER] = new MaskData(7070, new BrokerContainsFilter(1850, 1860, 1870, 1880, 1881, 1887), null, false),
        [BrokerItemMask.UNKNOWN] = new MaskData(1, new BrokerContainsFilter(0), null, false),
    };

    public static int GetId(this BrokerItemMask self)
    {
        return data[self].TypeId;
    }

    public static bool IsMatches(this BrokerItemMask self, Item item)
    {
        return data[self].Filter.Accept(item.GetItemTemplate());
    }

    public static bool IsChildrenMask(this BrokerItemMask self, int maskId)
    {
        for (BrokerItemMask? p = data[self].Parent; p != null; p = data[p.Value].Parent)
        {
            if (data[p.Value].TypeId == maskId)
                return true;
        }
        return false;
    }

    public static BrokerItemMask GetBrokerMaskById(int id)
    {
        foreach (BrokerItemMask mt in Enum.GetValues<BrokerItemMask>())
        {
            if (data[mt].TypeId == id)
                return mt;
        }
        return BrokerItemMask.UNKNOWN;
    }

    public static bool HasChildren(this BrokerItemMask self)
    {
        return data[self].ChildrenExist;
    }
}
