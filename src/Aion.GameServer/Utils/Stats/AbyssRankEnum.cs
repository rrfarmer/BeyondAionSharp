using System;
using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Utils.Stats;

/// <summary>Java parity: utils/stats/AbyssRankEnum (ATracer, Sarynth, Imaginary). @XmlEnum enum with per-instance fields→enum + AbyssRankEnumExtensions (static data dict + accessor/static methods). ordinal()→(int)rank; values()→Values(); IllegalArgumentException→ArgumentException. RankingConfig/ChatUtil/player.GetAbyssRank red-tolerated.</summary>
public enum AbyssRankEnum
{
    GRADE9_SOLDIER,
    GRADE8_SOLDIER,
    GRADE7_SOLDIER,
    GRADE6_SOLDIER,
    GRADE5_SOLDIER,
    GRADE4_SOLDIER,
    GRADE3_SOLDIER,
    GRADE2_SOLDIER,
    GRADE1_SOLDIER,
    STAR1_OFFICER,
    STAR2_OFFICER,
    STAR3_OFFICER,
    STAR4_OFFICER,
    STAR5_OFFICER,
    GENERAL,
    GREAT_GENERAL,
    COMMANDER,
    SUPREME_COMMANDER
}

public static class AbyssRankEnumExtensions
{
    private readonly struct RankData
    {
        public readonly int Id;
        public readonly int PointsGained;
        public readonly int PointsLost;
        public readonly int RequiredAP;
        public readonly int RequiredGP;

        public RankData(int id, int pointsGained, int pointsLost, int required, int gloryPointsRequired)
        {
            Id = id;
            PointsGained = pointsGained;
            PointsLost = pointsLost;
            RequiredAP = required;
            RequiredGP = gloryPointsRequired;
        }
    }

    private static readonly Dictionary<AbyssRankEnum, RankData> data = new()
    {
        [AbyssRankEnum.GRADE9_SOLDIER] = new RankData(1, 300, 90, 0, 0),
        [AbyssRankEnum.GRADE8_SOLDIER] = new RankData(2, 345, 103, 1200, 0),
        [AbyssRankEnum.GRADE7_SOLDIER] = new RankData(3, 396, 118, 4220, 0),
        [AbyssRankEnum.GRADE6_SOLDIER] = new RankData(4, 455, 136, 10990, 0),
        [AbyssRankEnum.GRADE5_SOLDIER] = new RankData(5, 523, 156, 23500, 0),
        [AbyssRankEnum.GRADE4_SOLDIER] = new RankData(6, 601, 180, 42780, 0),
        [AbyssRankEnum.GRADE3_SOLDIER] = new RankData(7, 721, 216, 69700, 0),
        [AbyssRankEnum.GRADE2_SOLDIER] = new RankData(8, 865, 259, 105600, 0),
        [AbyssRankEnum.GRADE1_SOLDIER] = new RankData(9, 1038, 311, 150800, 0),
        [AbyssRankEnum.STAR1_OFFICER] = new RankData(10, 1557, 467, 0, 1244),
        [AbyssRankEnum.STAR2_OFFICER] = new RankData(11, 1868, 560, 0, 1368),
        [AbyssRankEnum.STAR3_OFFICER] = new RankData(12, 2148, 644, 0, 1915),
        [AbyssRankEnum.STAR4_OFFICER] = new RankData(13, 2470, 741, 0, 3064),
        [AbyssRankEnum.STAR5_OFFICER] = new RankData(14, 3705, 1482, 0, 5210),
        [AbyssRankEnum.GENERAL] = new RankData(15, 4075, 1630, 0, 8335),
        [AbyssRankEnum.GREAT_GENERAL] = new RankData(16, 4482, 1792, 0, 10002),
        [AbyssRankEnum.COMMANDER] = new RankData(17, 4930, 1972, 0, 11503),
        [AbyssRankEnum.SUPREME_COMMANDER] = new RankData(18, 5916, 2366, 0, 12437),
    };

    public static AbyssRankEnum[] Values()
    {
        return Enum.GetValues<AbyssRankEnum>();
    }

    /// <returns>the id</returns>
    public static int GetId(this AbyssRankEnum self)
    {
        return data[self].Id;
    }

    /// <returns>the pointsLost</returns>
    public static int GetPointsLost(this AbyssRankEnum self)
    {
        return data[self].PointsLost;
    }

    /// <returns>the pointsGained</returns>
    public static int GetPointsGained(this AbyssRankEnum self)
    {
        return data[self].PointsGained;
    }

    /// <returns>AP required for Rank</returns>
    public static int GetRequiredAP(this AbyssRankEnum self)
    {
        return data[self].RequiredAP;
    }

    public static int GetRequiredGP(this AbyssRankEnum self)
    {
        return data[self].RequiredGP;
    }

    public static int GetGpLossPerDay(this AbyssRankEnum self)
    {
        return RankingConfig.TOP_RANKING_GP_LOSS.GetValueOrDefault(self, 0);
    }

    /// <returns>The quota is the maximum number of allowed player to have the rank</returns>
    public static int GetQuota(this AbyssRankEnum self)
    {
        return RankingConfig.TOP_RANKING_QUOTA.GetValueOrDefault(self, 0);
    }

    public static string GetRankL10n(Player player)
    {
        return player.GetAbyssRank().GetRank().GetRankL10n(player.GetRace());
    }

    public static string GetRankL10n(Race race, int rankId)
    {
        return GetRankById(rankId).GetRankL10n(race);
    }

    public static string GetRankL10n(this AbyssRankEnum self, Race race)
    {
        int rank9L10nId = race == Race.ELYOS ? 901215 : 901233;
        int rankL10nId = rank9L10nId + (int)self;
        return ChatUtil.L10n(rankL10nId);
    }

    public static AbyssRankEnum GetRankById(int id)
    {
        foreach (AbyssRankEnum rank in Values())
        {
            if (rank.GetId() == id)
                return rank;
        }
        throw new ArgumentException("Invalid abyss rank provided " + id);
    }

    public static AbyssRankEnum GetRankForPoints(int ap, int gp)
    {
        AbyssRankEnum r = AbyssRankEnum.GRADE9_SOLDIER;
        foreach (AbyssRankEnum rank in Values())
        {
            if (rank.GetRequiredAP() <= ap && rank.GetRequiredGP() <= gp)
                r = rank;
        }
        return r;
    }
}
