using System;
using System.Collections.Generic;
using System.Text;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Stat (MrPoke). Shows stat info of your target.</summary>
public class Stat : AdminCommand
{
    public Stat()
        : base("stat", "Shows stat info of your target.")
    {
        SetSyntaxInfo(
            "<stat> [details] - Show active stat functions for the given stat (default: just the name, optional: detailed).",
            "<abs> <stat set id|cancel> - Apply fixed stats of the given stats_set ID from absolute_stats.xml or cancel them.");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        VisibleObject target = admin.GetTarget();
        if (!(target is Creature))
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }
        Creature creature = (Creature)target;

        if (paramsArr.Length == 1)
        {
            List<IStatFunction> stats = creature.GetGameStats().GetStatsSorted(Enum.Parse<StatEnum>(paramsArr[0]));
            foreach (IStatFunction stat in stats)
            {
                SendInfo(admin, stat.ToString());
            }
        }
        else if (paramsArr.Length == 2 && "details".Equals(paramsArr[1]))
        {
            List<IStatFunction> stats = creature.GetGameStats().GetStatsSorted(Enum.Parse<StatEnum>(paramsArr[0]));
            foreach (IStatFunction stat in stats)
            {
                string details = CollectDetails(stat);
                SendInfo(admin, details);
            }
        }
        else if ("abs".Equals(paramsArr[0]))
        {
            if (!(target is Player))
            {
                SendInfo(admin, "Only players can be selected");
                return;
            }
            AbsoluteStatOwner absStats = ((Player)target).GetAbsoluteStats();
            if (int.TryParse(paramsArr[1], out int templateId))
            {
                absStats.SetTemplate(templateId);
                absStats.Apply();
                if (absStats.IsActive())
                {
                    SendInfo(admin, "Successfully applied absolute stats");
                }
                else
                {
                    SendInfo(admin, "No such template exists!");
                }
            }
            else
            {
                if (!"cancel".Equals(paramsArr[1], StringComparison.OrdinalIgnoreCase))
                {
                    SendInfo(admin, "Not a number");
                    return;
                }
                if (!absStats.IsActive())
                {
                    SendInfo(admin, "Nothing to cancel");
                    return;
                }
                absStats.Cancel();
                SendInfo(admin, "Successfully canceled absolute stats");
                PacketSendUtility.SendPacket((Player)target, new SM_STATS_INFO((Player)target));
            }
        }
        else
        {
            SendInfo(admin);
        }
    }

    private string CollectDetails(IStatFunction stat)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(stat.ToString() + "\n");
        if (stat is StatFunctionProxy)
        {
            StatFunctionProxy proxy = (StatFunctionProxy)stat;
            sb.Append(" -- " + proxy.GetProxiedFunction().ToString());
        }
        IStatOwner owner = stat.GetOwner();
        if (owner is Effect)
        {
            Effect effect = (Effect)owner;
            sb.Append("\n -- skillId: " + effect.GetSkillId());
            sb.Append("\n -- skillName: " + effect.GetSkillName());
        }
        return sb.ToString();
    }
}
