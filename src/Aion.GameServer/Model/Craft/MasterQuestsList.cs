using System;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Craft;

/// <summary>
/// Java parity: model/craft/MasterQuestsList. Java enum with per-instance questIds[]/race/craftSkillId +
/// static getQuestIds → C# enum + extension accessors + static lookup. IllegalArgumentException → ArgumentException.
/// </summary>
public enum MasterQuestsList
{
    COOKING_ELYOS,
    COOKING_ASMODIANS,
    WEAPONSMITHING_ELYOS,
    WEAPONSMITHING_ASMODIANS,
    ARMORSMITHING_ELYOS,
    ARMORSMITHING_ASMODIANS,
    TAILORING_ELYOS,
    TAILORING_ASMODIANS,
    ALCHEMY_ELYOS,
    ALCHEMY_ASMODIANS,
    HANDICRAFTING_ELYOS,
    HANDICRAFTING_ASMODIANS,
    MENUSIER_ELYOS,
    MENUSIER_ASMODIANS
}

public static class MasterQuestsListExtensions
{
    public static int[] GetQuestIds(this MasterQuestsList e) => e switch
    {
        MasterQuestsList.COOKING_ELYOS => new[] { 19039, 19038 },
        MasterQuestsList.COOKING_ASMODIANS => new[] { 29039, 29038 },
        MasterQuestsList.WEAPONSMITHING_ELYOS => new[] { 19009, 19008 },
        MasterQuestsList.WEAPONSMITHING_ASMODIANS => new[] { 29009, 29008 },
        MasterQuestsList.ARMORSMITHING_ELYOS => new[] { 19015, 19014 },
        MasterQuestsList.ARMORSMITHING_ASMODIANS => new[] { 29015, 29014 },
        MasterQuestsList.TAILORING_ELYOS => new[] { 19021, 19020 },
        MasterQuestsList.TAILORING_ASMODIANS => new[] { 29021, 29020 },
        MasterQuestsList.ALCHEMY_ELYOS => new[] { 19033, 19032 },
        MasterQuestsList.ALCHEMY_ASMODIANS => new[] { 29033, 29032 },
        MasterQuestsList.HANDICRAFTING_ELYOS => new[] { 19027, 19026 },
        MasterQuestsList.HANDICRAFTING_ASMODIANS => new[] { 29027, 29026 },
        MasterQuestsList.MENUSIER_ELYOS => new[] { 19058, 19057 },
        MasterQuestsList.MENUSIER_ASMODIANS => new[] { 29058, 29057 },
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static Race GetRace(this MasterQuestsList e) => e switch
    {
        MasterQuestsList.COOKING_ELYOS or MasterQuestsList.WEAPONSMITHING_ELYOS or MasterQuestsList.ARMORSMITHING_ELYOS
            or MasterQuestsList.TAILORING_ELYOS or MasterQuestsList.ALCHEMY_ELYOS or MasterQuestsList.HANDICRAFTING_ELYOS
            or MasterQuestsList.MENUSIER_ELYOS => Race.ELYOS,
        _ => Race.ASMODIANS,
    };

    public static int GetCraftSkillId(this MasterQuestsList e) => e switch
    {
        MasterQuestsList.COOKING_ELYOS or MasterQuestsList.COOKING_ASMODIANS => 40001,
        MasterQuestsList.WEAPONSMITHING_ELYOS or MasterQuestsList.WEAPONSMITHING_ASMODIANS => 40002,
        MasterQuestsList.ARMORSMITHING_ELYOS or MasterQuestsList.ARMORSMITHING_ASMODIANS => 40003,
        MasterQuestsList.TAILORING_ELYOS or MasterQuestsList.TAILORING_ASMODIANS => 40004,
        MasterQuestsList.ALCHEMY_ELYOS or MasterQuestsList.ALCHEMY_ASMODIANS => 40007,
        MasterQuestsList.HANDICRAFTING_ELYOS or MasterQuestsList.HANDICRAFTING_ASMODIANS => 40008,
        MasterQuestsList.MENUSIER_ELYOS or MasterQuestsList.MENUSIER_ASMODIANS => 40010,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static int[] GetQuestIds(int craftSkillId, Race race)
    {
        foreach (MasterQuestsList mql in Enum.GetValues(typeof(MasterQuestsList)))
        {
            if (race == mql.GetRace() && craftSkillId == mql.GetCraftSkillId())
                return mql.GetQuestIds();
        }
        throw new ArgumentException("Invalid craftSkillId: " + craftSkillId + " or race: " + race);
    }
}
