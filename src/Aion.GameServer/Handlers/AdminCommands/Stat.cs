using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Enchants;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Stat (MrPoke). Shows and modifies any stats.</summary>
public class Stat : AdminCommand
{
    public Stat()
        : base("stat", "Shows and modifies any stats.")
    {
        SetSyntaxInfo(
            "list - Lists all stats.",
            "<stat> - Shows your target's active stat functions for the given stat.",
            "<stat> <value> - Sets your target's stat to the given value.",
            "abs <stat set ID> - Applies fixed stats of the given stats_set ID from absolute_stats.xml to your target.",
            "cancel - Cancels all active stat overrides for your target.",
            "Stat parameters accept lowercase and abbreviated formats, such as flytime or flyt instead of FLY_TIME.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        VisibleObject target = admin.GetTarget() == null ? admin : admin.GetTarget();
        if (!(target is Creature creature))
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        if (paramsArr.Length == 1 && "list".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            ListStats(admin);
        }
        else if (paramsArr.Length == 1 && "cancel".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            CancelStatOverrides(admin, creature);
        }
        else if (paramsArr.Length == 1)
        {
            ShowStatFunctions(admin, creature, paramsArr[0]);
        }
        else if (paramsArr.Length == 2 && !"abs".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            SetStat(admin, creature, paramsArr[0], Aion.GameServer.Utils.ChatHandlers.JavaNumberParser.ParseInt(paramsArr[1]));
        }
        else if (paramsArr.Length == 2 && "abs".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            ModifiersTemplate template = DataManager.ABSOLUTE_STATS_DATA.GetTemplate(Aion.GameServer.Utils.ChatHandlers.JavaNumberParser.ParseInt(paramsArr[1]));
            if (template == null)
            {
                SendInfo(admin, "Invalid stat set ID.");
                return;
            }
            foreach (StatFunction m in template.GetModifiers())
                ApplyStatFunction(creature, m);
            SendInfo(admin, "Applied absolute stats to " + creature.GetName() + ".");
        }
        else
        {
            SendInfo(admin);
        }
    }

    public void ShowStatFunctions(Player admin, Creature target, string searchStat)
    {
        StatEnum? stat = FindStat(admin, searchStat);
        if (stat != null)
            ShowActiveStatFunctions(admin, target, stat.Value);
    }

    private StatEnum? FindStat(Player admin, string searchStat)
    {
        List<StatEnum> stats = FindPossibleMatches(searchStat);
        if (stats.Count != 1)
        {
            string message = "There is no stat with that name.";
            if (stats.Count != 0)
                message += " Possible matches:\n\t" + string.Join("\n\t", stats.Select(s => s.ToString()));
            SendInfo(admin, message);
            return null;
        }
        return stats[0];
    }

    private List<StatEnum> FindPossibleMatches(string searchStat)
    {
        if (searchStat.Length < 2)
            return new List<StatEnum>();
        List<StatEnum> possibleMatches = new List<StatEnum>();
        searchStat = searchStat.ToLowerInvariant();
        string searchStatShort = searchStat.Replace("_", "");
        foreach (StatEnum stat in Enum.GetValues<StatEnum>())
        {
            string statName = stat.ToString().ToLowerInvariant();
            string statNameShort = statName.Replace("_", "");
            if (searchStatShort.Equals(statNameShort))
                return new List<StatEnum> { stat };
            if (statNameShort.StartsWith(searchStatShort) || statName.Contains(searchStat))
            {
                possibleMatches.Add(stat);
            }
        }
        return possibleMatches;
    }

    private void ShowActiveStatFunctions(Player admin, Creature target, StatEnum stat)
    {
        List<IStatFunction> stats = target.GetGameStats().GetStatsSorted(stat);
        string targetInfo = admin.Equals(target) ? "You currently have " : target.GetName() + " currently has ";
        string statName = ChatUtil.Color(stat.ToString(), System.Drawing.Color.White);
        if (stats.Count == 0)
        {
            SendInfo(admin, targetInfo + "no active " + statName + " functions.");
            return;
        }
        SendInfo(admin, targetInfo + stats.Count + " active " + statName + " function(s):");
        var grouped = new Dictionary<StatFunctionInfo, long>();
        var order = new List<StatFunctionInfo>();
        foreach (IStatFunction f in stats)
        {
            var info = new StatFunctionInfo(f);
            if (grouped.TryGetValue(info, out long count))
                grouped[info] = count + 1;
            else
            {
                grouped[info] = 1;
                order.Add(info);
            }
        }
        foreach (StatFunctionInfo info in order)
            SendInfo(admin, ChatUtil.LeftPad(grouped[info], 3) + "x " + info);
    }

    public void ListStats(Player admin)
    {
        string stats = string.Join("\n\t", Enum.GetValues<StatEnum>().Select(s => s.ToString()));
        SendInfo(admin, "List of stats:\n\t" + stats);
    }

    public void SetStat(Player admin, Creature target, string searchStat, int value)
    {
        StatEnum? stat = FindStat(admin, searchStat);
        if (stat == null)
            return;
        ApplyStatFunction(target, new CommandStatFunction(stat.Value, value));
        string targetInfo = admin.Equals(target) ? "Your " : target.GetName() + "'s ";
        SendInfo(admin, targetInfo + stat.Value.ToString().ToLowerInvariant() + " is now set to " + value + ".");
    }

    private void ApplyStatFunction(Creature creature, StatFunction statFunction)
    {
        IStatOwner statOwner = CommandStatOwner.Get(statFunction.GetName());
        creature.GetGameStats().EndEffect(statOwner);
        creature.GetGameStats().AddEffect(statOwner, new List<StatFunction> { statFunction });
    }

    public void CancelStatOverrides(Player admin, Creature target)
    {
        CommandStatOwner.ForEach(owner => target.GetGameStats().EndEffect(owner));
        string targetInfo = admin.Equals(target) ? "Your" : target.GetName() + "'s";
        SendInfo(admin, targetInfo + " stat overrides have been canceled.");
    }

    internal class CommandStatFunction : StatFunction
    {
        public CommandStatFunction(StatEnum name, int value)
            : base(name, value, true)
        {
        }

        public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
        {
            stat.SetBonusRate(1f);
            stat.SetFinalRate(1f);
            stat.SetBonus(GetValue() - stat.GetExactCurrentWithoutBonus());
        }

        public override int GetPriority()
        {
            return 120;
        }
    }

    internal sealed record CommandStatOwner(StatEnum StatName) : IStatOwner
    {
        private static readonly Dictionary<StatEnum, IStatOwner> statOwnerByStat = new();

        internal static IStatOwner Get(StatEnum stat)
        {
            if (!statOwnerByStat.TryGetValue(stat, out IStatOwner owner))
                statOwnerByStat[stat] = owner = new CommandStatOwner(stat);
            return owner;
        }

        internal static void ForEach(Action<IStatOwner> consumer)
        {
            foreach (IStatOwner owner in statOwnerByStat.Values)
                consumer(owner);
        }
    }

    internal sealed record StatFunctionInfo(int Value, bool Bonus, int Priority, IStatOwner Owner, string Type)
    {
        internal StatFunctionInfo(IStatFunction f)
            : this(f.GetValue(), f.IsBonus(), f.GetPriority(), f.GetOwner(),
                (f is StatFunctionProxy p ? p.GetProxiedFunction() : f).GetType().Name)
        {
        }

        public override string ToString()
        {
            string info = IsOverrideFunction() ? "=" + Value : Value >= 0 ? "+" + Value : "" + Value;
            if (Type.Equals(nameof(CommandStatFunction)))
            {
                info = ChatUtil.Color(info, System.Drawing.Color.Cyan);
            }
            else
            {
                if (Type.Equals(nameof(StatRateFunction)))
                    info += "%";
                info = ChatUtil.Color(info, Value < 0 ? System.Drawing.Color.Red : Bonus ? System.Drawing.Color.Green : System.Drawing.Color.White);
                if (Bonus)
                    info += " bonus";
            }
            info += ", priority: " + Priority;
            info += ", type: " + Type;
            info += ", owner: " + (Owner == null ? "none" : Owner.GetType().Name);
            if (Owner is Effect effect)
                info += " (skill ID " + effect.GetSkillId() + ": " + effect.GetSkillName() + ")";
            else if (Owner is Item item)
                info += " (" + item.Name + ")";
            else if (Owner is EnchantEffect enchantEffect)
                info += " (" + enchantEffect.GetItemSlot() + ")";
            return info;
        }

        private bool IsOverrideFunction()
        {
            return Type.Equals(nameof(CommandStatFunction)) || Type.Equals(nameof(StatAbsFunction)) || Type.Equals(nameof(StatSetFunction));
        }
    }
}
