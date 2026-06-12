using System.Collections.Generic;
using Aion.GameServer.Utils.Stats;
using Quartz;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/RankingConfig (Sarynth). CronExpression (Quartz) + @Properties keyPattern maps→Dictionary&lt;AbyssRankEnum,int&gt; (populated by config loader). XFORM_MIN_RANK default STAR5_OFFICER. Referenced by AbyssRankEnumExtensions.GetGpLossPerDay/GetQuota.</summary>
public static class RankingConfig
{
    /// <summary>Key: gameserver.topranking.updaterule (default "0 0 0 ? * *")</summary>
    public static CronExpression TOP_RANKING_UPDATE_RULE;

    /// <summary>Key: gameserver.topranking.daily.gploss.time (default "0 0 12 ? * *")</summary>
    public static CronExpression TOP_RANKING_DAILY_GP_LOSS_TIME;

    /// <summary>Key: gameserver.topranking.legion_limit (default 50)</summary>
    public static int RANKING_LIST_LEGION_LIMIT = 50;

    /// <summary>Key: gameserver.topranking.max.offline.days (default 0)</summary>
    public static int TOP_RANKING_MAX_OFFLINE_DAYS = 0;

    /// <summary>Key: gameserver.topranking.xform.min_rank (default STAR5_OFFICER)</summary>
    public static AbyssRankEnum XFORM_MIN_RANK = AbyssRankEnum.STAR5_OFFICER;

    /// <summary>@Properties keyPattern ^gameserver\.topranking\.quota\.(.+)</summary>
    public static Dictionary<AbyssRankEnum, int> TOP_RANKING_QUOTA;

    /// <summary>@Properties keyPattern ^gameserver\.topranking\.gp_loss\.(.+)</summary>
    public static Dictionary<AbyssRankEnum, int> TOP_RANKING_GP_LOSS;
}
