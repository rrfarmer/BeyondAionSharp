using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/AlterNpc (Estrayl). Used to alter stats of an NPC.</summary>
public class AlterNpc : AdminCommand
{
    private static readonly HashSet<StatEnum> allowedStats = new HashSet<StatEnum>
    {
        StatEnum.MAXHP, StatEnum.PHYSICAL_ATTACK, StatEnum.MAGICAL_ATTACK,
        StatEnum.PHYSICAL_DEFENSE, StatEnum.MAGICAL_DEFEND, StatEnum.MAGICAL_RESIST, StatEnum.PARRY, StatEnum.PHYSICAL_ACCURACY,
        StatEnum.MAGICAL_ACCURACY, StatEnum.PHYSICAL_CRITICAL_RESIST, StatEnum.MAGIC_SKILL_BOOST_RESIST
    };

    public AlterNpc()
        : base("alternpc", "Used to alter stats of an NPC.")
    {
        SetSyntaxInfo(
            "<list> - Displays all alterable stats.",
            "<change> [stat] [value] - Changes the respective stat to given value.");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(player);
            return;
        }
        if (paramsArr[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            ShowList(player);
        else if (paramsArr[0].Equals("change", StringComparison.OrdinalIgnoreCase) && paramsArr.Length > 2)
            ChangeStat(player, paramsArr);
        else
            SendInfo(player);
    }

    private void ChangeStat(Player player, string[] paramsArr)
    {
        StatEnum toModify = ParseEnumName<StatEnum>(paramsArr[1].ToUpper());
        if (!allowedStats.Contains(toModify))
        {
            SendInfo(player, "'" + paramsArr[0] + "' is not supported.");
            return;
        }

        if (!(player.GetTarget() is Npc))
        {
            SendInfo(player, "You should select an NPC first.");
            return;
        }
        Npc target = (Npc)player.GetTarget();
        int newValue = ParseValue(paramsArr[2]);
        if (newValue < 1)
        {
            SendInfo(player, "New stat values have to be numbers.");
            return;
        }

        target.GetGameStats().AddEffect(null, new List<IStatFunction> { new StatSetFunction(toModify, newValue) });
        if (toModify == StatEnum.MAXHP)
            target.GetLifeStats().SetCurrentHp(newValue);
        PacketSendUtility.SendMessage(player, "Altered " + target + "'s " + toModify.ToString() + " to " + paramsArr[2] + ".");
    }

    private void ShowList(Player player)
    {
        string msg = "";
        foreach (StatEnum se in allowedStats)
            msg += "\n" + se.ToString();
        PacketSendUtility.SendMessage(player, "These stats are currently allowed to change: " + msg);
    }

    private int ParseValue(string value)
    {
        if (TryParseInt(value.Replace("_", ""), out int result))
            return result;
        return 0;
    }
}
