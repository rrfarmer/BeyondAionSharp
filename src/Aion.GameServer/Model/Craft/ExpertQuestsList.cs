using System;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Craft;

/// <summary>
/// Java parity: model/craft/ExpertQuestsList. Java enum with per-instance questIds[]/race/craftSkillId +
/// static getQuestIds → C# enum + extension accessors + static lookup. IllegalArgumentException → ArgumentException.
/// </summary>
public enum ExpertQuestsList
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

public static class ExpertQuestsListExtensions
{
    public static int[] GetQuestIds(this ExpertQuestsList e) => e switch
    {
        ExpertQuestsList.COOKING_ELYOS => new[] { 1944, 1979, 1978, 3952, 3951, 3950 },
        ExpertQuestsList.COOKING_ASMODIANS => new[] { 2934, 2979, 2978, 4956, 4955, 4954 },
        ExpertQuestsList.WEAPONSMITHING_ELYOS => new[] { 1941, 1973, 1972, 3943, 3942, 3941 },
        ExpertQuestsList.WEAPONSMITHING_ASMODIANS => new[] { 2931, 2973, 2972, 4947, 4946, 4945 },
        ExpertQuestsList.ARMORSMITHING_ELYOS => new[] { 1942, 1975, 1974, 3946, 3945, 3944 },
        ExpertQuestsList.ARMORSMITHING_ASMODIANS => new[] { 2912, 2975, 2974, 4950, 4949, 4948 },
        ExpertQuestsList.TAILORING_ELYOS => new[] { 1946, 1983, 1982, 3958, 3957, 3956 },
        ExpertQuestsList.TAILORING_ASMODIANS => new[] { 2936, 2983, 2982, 4962, 4961, 4960 },
        ExpertQuestsList.ALCHEMY_ELYOS => new[] { 1945, 1981, 1980, 3955, 3954, 3953 },
        ExpertQuestsList.ALCHEMY_ASMODIANS => new[] { 2935, 2981, 2980, 4959, 4958, 4957 },
        ExpertQuestsList.HANDICRAFTING_ELYOS => new[] { 1943, 1977, 1976, 3949, 3948, 3947 },
        ExpertQuestsList.HANDICRAFTING_ASMODIANS => new[] { 2933, 2977, 2976, 4953, 4952, 4951 },
        ExpertQuestsList.MENUSIER_ELYOS => new[] { 19050, 19053, 19052, 19056, 19055, 19054 },
        ExpertQuestsList.MENUSIER_ASMODIANS => new[] { 29050, 29053, 29052, 29056, 29055, 29054 },
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static Race GetRace(this ExpertQuestsList e) => e switch
    {
        ExpertQuestsList.COOKING_ELYOS or ExpertQuestsList.WEAPONSMITHING_ELYOS or ExpertQuestsList.ARMORSMITHING_ELYOS
            or ExpertQuestsList.TAILORING_ELYOS or ExpertQuestsList.ALCHEMY_ELYOS or ExpertQuestsList.HANDICRAFTING_ELYOS
            or ExpertQuestsList.MENUSIER_ELYOS => Race.ELYOS,
        _ => Race.ASMODIANS,
    };

    public static int GetCraftSkillId(this ExpertQuestsList e) => e switch
    {
        ExpertQuestsList.COOKING_ELYOS or ExpertQuestsList.COOKING_ASMODIANS => 40001,
        ExpertQuestsList.WEAPONSMITHING_ELYOS or ExpertQuestsList.WEAPONSMITHING_ASMODIANS => 40002,
        ExpertQuestsList.ARMORSMITHING_ELYOS or ExpertQuestsList.ARMORSMITHING_ASMODIANS => 40003,
        ExpertQuestsList.TAILORING_ELYOS or ExpertQuestsList.TAILORING_ASMODIANS => 40004,
        ExpertQuestsList.ALCHEMY_ELYOS or ExpertQuestsList.ALCHEMY_ASMODIANS => 40007,
        ExpertQuestsList.HANDICRAFTING_ELYOS or ExpertQuestsList.HANDICRAFTING_ASMODIANS => 40008,
        ExpertQuestsList.MENUSIER_ELYOS or ExpertQuestsList.MENUSIER_ASMODIANS => 40010,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static int[] GetQuestIds(int craftSkillId, Race race)
    {
        foreach (ExpertQuestsList eql in Enum.GetValues(typeof(ExpertQuestsList)))
        {
            if (race == eql.GetRace() && craftSkillId == eql.GetCraftSkillId())
                return eql.GetQuestIds();
        }
        throw new ArgumentException("Invalid craftSkillId: " + craftSkillId + " or race: " + race);
    }
}
