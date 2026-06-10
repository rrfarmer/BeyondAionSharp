using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Services.Abyss;

/// <summary>Java parity: services/abyss/AbyssSkillService (ATracer). Recomputes a player's abyss skills on rank change: removes all of their race's abyss skills, then (if rank >= XFORM_MIN_RANK) re-learns the temporary skills for their current rank. AbyssRank/AbyssRankEnum/SkillLearnService red-tolerated.</summary>
public class AbyssSkillService
{
    public static void UpdateSkills(Player player)
    {
        AbyssRank abyssRank = player.GetAbyssRank();
        if (abyssRank == null)
        {
            return;
        }
        AbyssRankEnum rankEnum = abyssRank.GetRank();
        // remove all abyss skills first
        foreach (AbyssSkills abyssSkill in AbyssSkills.Values())
        {
            if (abyssSkill.GetRace() == player.GetRace())
            {
                foreach (int skillId in abyssSkill.GetSkills())
                    SkillLearnService.RemoveSkill(player, skillId);
            }
        }
        if (RankingConfig.XFORM_MIN_RANK != null)
        {
            // add new skills
            if (abyssRank.GetRank().GetId() >= RankingConfig.XFORM_MIN_RANK.GetId())
            {
                foreach (int skillId in AbyssSkills.GetSkills(player.GetRace(), rankEnum))
                    SkillLearnService.LearnTemporarySkill(player, skillId, 1);
            }
        }
    }
}

/// <summary>Java parity: services/abyss/AbyssSkills (package-private value-carrying enum). Race + AbyssRankEnum -> abyss skill id set. Java enum + varargs ctor -> sealed class with static SCREAMING_SNAKE instances + Values(); int... skills -> params int[]; getSkills(race,rank) static lookup logs + empty on miss. Race/AbyssRankEnum red-tolerated.</summary>
public sealed class AbyssSkills
{
    public static readonly AbyssSkills SUPREME_COMMANDER = new AbyssSkills(Race.ELYOS, AbyssRankEnum.SUPREME_COMMANDER, 11889, 11898, 11900, 11903, 11904, 11905, 11906);
    public static readonly AbyssSkills COMMANDER = new AbyssSkills(Race.ELYOS, AbyssRankEnum.COMMANDER, 11888, 11898, 11900, 11903, 11904);
    public static readonly AbyssSkills GREAT_GENERAL = new AbyssSkills(Race.ELYOS, AbyssRankEnum.GREAT_GENERAL, 11887, 11897, 11899, 11903);
    public static readonly AbyssSkills GENERAL = new AbyssSkills(Race.ELYOS, AbyssRankEnum.GENERAL, 11886, 11896, 11899);
    public static readonly AbyssSkills STAR5_OFFICER = new AbyssSkills(Race.ELYOS, AbyssRankEnum.STAR5_OFFICER, 11885, 11895);
    public static readonly AbyssSkills SUPREME_COMMANDER_A = new AbyssSkills(Race.ASMODIANS, AbyssRankEnum.SUPREME_COMMANDER, 11894, 11898, 11902, 11903, 11904, 11905, 11906);
    public static readonly AbyssSkills COMMANDER_A = new AbyssSkills(Race.ASMODIANS, AbyssRankEnum.COMMANDER, 11893, 11898, 11902, 11903, 11904);
    public static readonly AbyssSkills GREAT_GENERAL_A = new AbyssSkills(Race.ASMODIANS, AbyssRankEnum.GREAT_GENERAL, 11892, 11897, 11901, 11903);
    public static readonly AbyssSkills GENERAL_A = new AbyssSkills(Race.ASMODIANS, AbyssRankEnum.GENERAL, 11891, 11896, 11901);
    public static readonly AbyssSkills STAR5_OFFICER_A = new AbyssSkills(Race.ASMODIANS, AbyssRankEnum.STAR5_OFFICER, 11890, 11895);

    private static readonly AbyssSkills[] _values =
    {
        SUPREME_COMMANDER, COMMANDER, GREAT_GENERAL, GENERAL, STAR5_OFFICER,
        SUPREME_COMMANDER_A, COMMANDER_A, GREAT_GENERAL_A, GENERAL_A, STAR5_OFFICER_A
    };

    private readonly int[] skills;
    private readonly AbyssRankEnum rankenum;
    private readonly Race race;

    private AbyssSkills(Race race, AbyssRankEnum rankEnum, params int[] skills)
    {
        this.race = race;
        this.rankenum = rankEnum;
        this.skills = skills;
    }

    public static AbyssSkills[] Values()
    {
        return _values;
    }

    public Race GetRace()
    {
        return race;
    }

    public int[] GetSkills()
    {
        return skills;
    }

    public static int[] GetSkills(Race race, AbyssRankEnum rank)
    {
        foreach (AbyssSkills aSkills in Values())
        {
            if (aSkills.race == race && aSkills.rankenum == rank)
            {
                return aSkills.skills;
            }
        }
        NullLoggerFactory.Instance.CreateLogger(nameof(AbyssSkills)).LogWarning("No abyss skills for: " + race + " " + rank);
        return new int[0];
    }
}
