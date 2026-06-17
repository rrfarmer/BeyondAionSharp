using System.Collections.Generic;
using Aion.Commons.Configuration;
using Aion.GameServer.Utils.Stats;
using Quartz;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/RankingConfig (Sarynth). CronExpression (Quartz) + @Properties keyPattern maps→Dictionary&lt;AbyssRankEnum,int&gt; (populated by config loader). XFORM_MIN_RANK default STAR5_OFFICER. Referenced by AbyssRankEnumExtensions.GetGpLossPerDay/GetQuota.</summary>
public static class RankingConfig
{
    /// <summary>Key: gameserver.topranking.updaterule (default "0 0 0 ? * *"). Initialized from the Java @Property
    /// defaultValue (== config/main/ranking.properties) via CronExpressions.GetOrCreate — same shape as the
    /// SiegeConfig.MOLTENUS_SPAWN_SCHEDULE fix — so AbyssRankUpdateService.ScheduleUpdate doesn't pass a null
    /// CronExpression to CronService.Schedule (the CronJobService null-cron failure mode). No invented value.</summary>
    [Property(key: "gameserver.topranking.updaterule", defaultValue: "0 0 0 ? * *")]
    public static CronExpression TOP_RANKING_UPDATE_RULE = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 0 ? * *");

    /// <summary>Key: gameserver.topranking.daily.gploss.time (default "0 0 12 ? * *"). Initialized from the Java
    /// @Property defaultValue via CronExpressions.GetOrCreate (no invented value).</summary>
    [Property(key: "gameserver.topranking.daily.gploss.time", defaultValue: "0 0 12 ? * *")]
    public static CronExpression TOP_RANKING_DAILY_GP_LOSS_TIME = Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 12 ? * *");

    /// <summary>Key: gameserver.topranking.legion_limit (default 50)</summary>
    [Property(key: "gameserver.topranking.legion_limit", defaultValue: "50")]
    public static int RANKING_LIST_LEGION_LIMIT = 50;

    /// <summary>Key: gameserver.topranking.max.offline.days (default 0)</summary>
    [Property(key: "gameserver.topranking.max.offline.days", defaultValue: "0")]
    public static int TOP_RANKING_MAX_OFFLINE_DAYS = 0;

    /// <summary>Key: gameserver.topranking.xform.min_rank (default STAR5_OFFICER)</summary>
    [Property(key: "gameserver.topranking.xform.min_rank", defaultValue: "STAR5_OFFICER")]
    public static AbyssRankEnum XFORM_MIN_RANK = AbyssRankEnum.STAR5_OFFICER;

    /// <summary>@Properties keyPattern ^gameserver\.topranking\.quota\.(.+)</summary>
    [Properties(keyPattern: "^gameserver\\.topranking\\.quota\\.(.+)")]
    public static Dictionary<AbyssRankEnum, int> TOP_RANKING_QUOTA;

    /// <summary>@Properties keyPattern ^gameserver\.topranking\.gp_loss\.(.+)</summary>
    [Properties(keyPattern: "^gameserver\\.topranking\\.gp_loss\\.(.+)")]
    public static Dictionary<AbyssRankEnum, int> TOP_RANKING_GP_LOSS;
}
