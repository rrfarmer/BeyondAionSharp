using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Stats.Container;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>
/// Java parity: model/gameobjects/player/Rates (antness, Neon).
/// Java enum with per-constant method bodies → class-enum (static readonly instances; nested subclasses override CalcResult).
/// </summary>
public abstract class Rates
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly string _name;

    protected Rates(string name)
    {
        _name = name;
    }

    // Java parity: Enum.name()
    public string Name()
    {
        return _name;
    }

    public abstract long CalcResult(Player player, long value);

    public int CalcResult(Player player, int value)
    {
        long result = CalcResult(player, (long)value);
        try
        {
            return checked((int)result);
        }
        catch (OverflowException e)
        {
            log.LogError(e, Name() + " result is too large for " + player + ": " + result);
            return value;
        }
    }

    /// <returns>The rate for the given player, selected by his current membership.</returns>
    public static float Get(Player player, float[] membershipRates)
    {
        if (membershipRates.Length == 0)
        {
            log.LogWarning(new System.InvalidOperationException(), "Missing rates");
            return 1;
        }
        int membershipLevel = player.GetAccount().GetMembership();
        return membershipRates[Math.Min(membershipRates.Length - 1, membershipLevel)];
    }

    private static float CalcXpRate(Player player, float[] membershipRates, StatEnum boostRate)
    {
        float endRate = Get(player, membershipRates);
        endRate *= player.GetGameStats().GetStat(boostRate, 100).GetCurrent() / 100f;
        if (player.IsLegionMember() && player.GetLegion().HasBonus())
            endRate *= 1.1f;
        return endRate;
    }

    public static readonly Rates XP_HUNTING = new XpHunting();
    public static readonly Rates XP_GROUP_HUNTING = new XpGroupHunting();
    public static readonly Rates XP_QUEST = new XpQuest();
    public static readonly Rates XP_GATHERING = new XpGathering();
    public static readonly Rates XP_CRAFTING = new XpCrafting();
    public static readonly Rates XP_PVP = new XpPvp();
    public static readonly Rates SKILL_XP_GATHERING = new SkillXpGathering();
    public static readonly Rates SKILL_XP_CRAFTING = new SkillXpCrafting();
    public static readonly Rates AP_PVP = new ApPvp();
    public static readonly Rates AP_PVP_LOST = new ApPvpLost();
    public static readonly Rates AP_PVE = new ApPve();
    public static readonly Rates AP_QUEST = new ApQuest();
    public static readonly Rates AP_DREDGION = new ApDredgion();
    public static readonly Rates GP = new Gp();
    public static readonly Rates DP_PVE = new DpPve();
    public static readonly Rates DP_PVP = new DpPvp();
    public static readonly Rates QUEST_KINAH = new QuestKinah();
    public static readonly Rates GATHERING_COUNT = new GatheringCount();
    public static readonly Rates SELL_LIMIT = new SellLimit();

    private sealed class XpHunting : Rates
    {
        public XpHunting() : base("XP_HUNTING") { }
        public override long CalcResult(Player player, long xp) =>
            (long)Math.Min(xp * CalcXpRate(player, RatesConfig.XP_SOLO_RATES, StatEnum.BOOST_HUNTING_XP_RATE), player.GetCommonData().GetExpNeed() * 0.2f);
    }

    private sealed class XpGroupHunting : Rates
    {
        public XpGroupHunting() : base("XP_GROUP_HUNTING") { }
        public override long CalcResult(Player player, long xp) =>
            (long)Math.Min(xp * CalcXpRate(player, RatesConfig.XP_GROUP_RATES, StatEnum.BOOST_GROUP_HUNTING_XP_RATE), player.GetCommonData().GetExpNeed() * 0.2f);
    }

    private sealed class XpQuest : Rates
    {
        public XpQuest() : base("XP_QUEST") { }
        public override long CalcResult(Player player, long xp) =>
            (long)(xp * CalcXpRate(player, RatesConfig.XP_QUEST_RATES, StatEnum.BOOST_QUEST_XP_RATE));
    }

    private sealed class XpGathering : Rates
    {
        public XpGathering() : base("XP_GATHERING") { }
        public override long CalcResult(Player player, long xp) =>
            (long)(xp * CalcXpRate(player, RatesConfig.XP_GATHERING_RATES, StatEnum.BOOST_GATHERING_XP_RATE));
    }

    private sealed class XpCrafting : Rates
    {
        public XpCrafting() : base("XP_CRAFTING") { }
        public override long CalcResult(Player player, long xp) =>
            (long)(xp * CalcXpRate(player, RatesConfig.XP_CRAFTING_RATES, StatEnum.BOOST_CRAFTING_XP_RATE));
    }

    private sealed class XpPvp : Rates
    {
        public XpPvp() : base("XP_PVP") { }
        public override long CalcResult(Player player, long xp) => (long)(xp * Get(player, RatesConfig.XP_PVP_RATES));
    }

    private sealed class SkillXpGathering : Rates
    {
        public SkillXpGathering() : base("SKILL_XP_GATHERING") { }
        public override long CalcResult(Player player, long skillXp) => (long)(skillXp * Get(player, RatesConfig.SKILL_XP_GATHERING_RATES));
    }

    private sealed class SkillXpCrafting : Rates
    {
        public SkillXpCrafting() : base("SKILL_XP_CRAFTING") { }
        public override long CalcResult(Player player, long skillXp) => (long)(skillXp * Get(player, RatesConfig.SKILL_XP_CRAFTING_RATES));
    }

    private sealed class ApPvp : Rates
    {
        public ApPvp() : base("AP_PVP") { }
        public override long CalcResult(Player player, long ap)
        {
            float statRate = player.GetGameStats().GetStat(StatEnum.AP_BOOST, 100).GetCurrent() / 100f;
            return (long)(ap * Get(player, RatesConfig.AP_PVP_RATES) * statRate);
        }
    }

    private sealed class ApPvpLost : Rates
    {
        public ApPvpLost() : base("AP_PVP_LOST") { }
        public override long CalcResult(Player player, long ap) => (long)(ap * Get(player, RatesConfig.AP_PVP_LOSS_RATES));
    }

    private sealed class ApPve : Rates
    {
        public ApPve() : base("AP_PVE") { }
        public override long CalcResult(Player player, long ap)
        {
            float statRate = player.GetGameStats().GetStat(StatEnum.AP_BOOST, 100).GetCurrent() / 100f;
            return (long)(ap * Get(player, RatesConfig.AP_PVE_RATES) * statRate);
        }
    }

    private sealed class ApQuest : Rates
    {
        public ApQuest() : base("AP_QUEST") { }
        public override long CalcResult(Player player, long ap) => (long)(ap * Get(player, RatesConfig.AP_QUEST_RATES));
    }

    private sealed class ApDredgion : Rates
    {
        public ApDredgion() : base("AP_DREDGION") { }
        public override long CalcResult(Player player, long ap) => (long)(ap * Get(player, RatesConfig.AP_DREDGION_RATES));
    }

    private sealed class Gp : Rates
    {
        public Gp() : base("GP") { }
        public override long CalcResult(Player player, long gp) => (long)(gp * Get(player, RatesConfig.GP_RATES));
    }

    private sealed class DpPve : Rates
    {
        public DpPve() : base("DP_PVE") { }
        public override long CalcResult(Player player, long dp) => (long)(dp * Get(player, RatesConfig.DP_PVE_RATES));
    }

    private sealed class DpPvp : Rates
    {
        public DpPvp() : base("DP_PVP") { }
        public override long CalcResult(Player player, long dp) => (long)(dp * Get(player, RatesConfig.DP_PVP_RATES));
    }

    private sealed class QuestKinah : Rates
    {
        public QuestKinah() : base("QUEST_KINAH") { }
        public override long CalcResult(Player player, long kinah) => (long)(kinah * Get(player, RatesConfig.QUEST_KINAH_RATES));
    }

    private sealed class GatheringCount : Rates
    {
        public GatheringCount() : base("GATHERING_COUNT") { }
        public override long CalcResult(Player player, long gatherCount) => (long)(gatherCount * Get(player, RatesConfig.GATHERING_COUNT_RATES));
    }

    private sealed class SellLimit : Rates
    {
        public SellLimit() : base("SELL_LIMIT") { }
        public override long CalcResult(Player player, long sellLimit) => (long)(sellLimit * Get(player, RatesConfig.SELL_LIMIT_RATES));
    }
}
