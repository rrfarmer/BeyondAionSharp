using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/EnergyBuff (Source).</summary>
public class EnergyBuff : AdminCommand
{
    public EnergyBuff()
        : base("energy")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            Info(player, null);
            return;
        }

        Player targetPlayer = player.GetTarget() is Player target ? target : player;
        if (paramsArr[0].Equals("refresh"))
        {
            PacketSendUtility.SendPacket(targetPlayer, new SM_STATS_INFO(targetPlayer));
            return;
        }

        if (paramsArr.Length < 2 || (paramsArr[1].Equals("add") && paramsArr.Length < 3))
        {
            Info(player, null!);
            return;
        }

        if (paramsArr[0].Equals("repose"))
        {
            if (paramsArr[1].Equals("info"))
                PacketSendUtility.SendMessage(player, "Current EoR: " + targetPlayer.GetCommonData().GetCurrentReposeEnergy() + "\n Max EoR: "
                    + targetPlayer.GetCommonData().GetMaxReposeEnergy());
            else if (paramsArr[1].Equals("add"))
                targetPlayer.GetCommonData().AddReposeEnergy(ParseLong(paramsArr[2]));
            else if (paramsArr[1].Equals("reset"))
                targetPlayer.GetCommonData().SetCurrentReposeEnergy(0);
        }
        else if (paramsArr[0].Equals("salvation"))
        {
            if (paramsArr[1].Equals("info"))
                PacketSendUtility.SendMessage(player, "Current EoS: " + targetPlayer.GetCommonData().GetCurrentSalvationPercent());
            else if (paramsArr[1].Equals("add"))
                targetPlayer.GetCommonData().AddSalvationPoints(ParseLong(paramsArr[2]));
            else if (paramsArr[1].Equals("reset"))
                targetPlayer.GetCommonData().ResetSalvationPoints();
        }
    }

    private void Info(Player player, string message)
    {
        string syntax = "//energy repose|salvation|refresh info|reset|add [points]";
        PacketSendUtility.SendMessage(player, syntax);
    }
}
