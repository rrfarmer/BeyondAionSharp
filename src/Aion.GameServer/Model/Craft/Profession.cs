using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Craft;

/// <summary>Java parity: model/craft/Profession (Neon). Enum w/ skillId per member→enum + ProfessionExtensions; getUpgradeCost Integer→int?; getBySkillId null→Profession?. DataManager.SKILL_DATA/ChatUtil red-tolerated.</summary>
public enum Profession
{
    ESSENCETAPPING,
    AETHERTAPPING,
    COOKING,
    WEAPONSMITHING,
    ARMORSMITHING,
    TAILORING,
    ALCHEMY,
    HANDICRAFTING,
    CONSTRUCTION
}

public static class ProfessionExtensions
{
    private static readonly Dictionary<Profession, int> skillIds = new()
    {
        [Profession.ESSENCETAPPING] = 30002,
        [Profession.AETHERTAPPING] = 30003,
        [Profession.COOKING] = 40001,
        [Profession.WEAPONSMITHING] = 40002,
        [Profession.ARMORSMITHING] = 40003,
        [Profession.TAILORING] = 40004,
        // LEATHERWORK = 40005,
        // CARPENTRY = 40006,
        [Profession.ALCHEMY] = 40007,
        [Profession.HANDICRAFTING] = 40008,
        [Profession.CONSTRUCTION] = 40010,
    };

    public static int GetSkillId(this Profession self)
    {
        return skillIds[self];
    }

    public static bool IsCrafting(this Profession self)
    {
        int skillId = self.GetSkillId();
        return skillId >= 40001 && skillId <= 40010;
    }

    public static int? GetUpgradeCost(this Profession self, int skillLevel)
    {
        switch (skillLevel)
        {
            case 0:
                return 3500;
            case 99:
                return 17000;
            case 199:
                return 115000;
            case 299:
                return 460000;
            case 449:
                return self.IsCrafting() ? 6004900 : null; // essence- and aethertapping have no artisan grade between expert and master
        }
        return null;
    }

    public static int GetMaxUpgradableLevel(this Profession self)
    {
        return self.IsCrafting() ? 499 : 399;
    }

    public static string GetClientName(this Profession self)
    {
        return DataManager.SKILL_DATA.GetSkillTemplate(self.GetSkillId()).GetL10n();
    }

    public static string GetClientName(this Profession self, int skillLevel)
    {
        return GetSkillGrade(self, skillLevel) + " " + self.GetClientName();
    }

    private static string GetSkillGrade(Profession self, int skillLevel)
    {
        if (skillLevel <= 99)
            return ChatUtil.L10n(900797); // Amateur
        if (skillLevel <= 199)
            return ChatUtil.L10n(900798); // Novice
        if (skillLevel <= 299)
            return ChatUtil.L10n(900799); // Apprentice
        if (skillLevel <= 399)
            return ChatUtil.L10n(900800); // Journeyman
        if (skillLevel <= 449)
            return ChatUtil.L10n(900801); // Expert
        if (self.IsCrafting() && skillLevel <= 499)
            return ChatUtil.L10n(902027); // Artisan
        return ChatUtil.L10n(902028); // Master
    }

    public static Profession? GetBySkillId(int skillId)
    {
        foreach (Profession profession in System.Enum.GetValues<Profession>())
        {
            if (profession.GetSkillId() == skillId)
                return profession;
        }
        return null;
    }
}
