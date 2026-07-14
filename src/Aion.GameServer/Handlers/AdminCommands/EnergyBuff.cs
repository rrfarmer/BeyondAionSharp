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
        VisibleObject target = player.GetTarget();
        if (target == null)
        {
            PacketSendUtility.SendMessage(player, "No target selected");
            return;
        }

        Creature creature = (Creature)target;
        if (paramsArr == null || paramsArr.Length < 1)
        {
            Info(player, null);
        }
        else if (target is Player)
        {
            if (paramsArr[0].Equals("repose"))
            {
                Player targetPlayer = (Player)creature;
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
                Player targetPlayer = (Player)creature;
                if (paramsArr[1].Equals("info"))
                    PacketSendUtility.SendMessage(player, "Current EoS: " + targetPlayer.GetCommonData().GetCurrentSalvationPercent());
                else if (paramsArr[1].Equals("add"))
                    targetPlayer.GetCommonData().AddSalvationPoints(ParseLong(paramsArr[2]));
                else if (paramsArr[1].Equals("reset"))
                    targetPlayer.GetCommonData().ResetSalvationPoints();
            }
            else if (paramsArr[0].Equals("refresh"))
            {
                Player targetPlayer = (Player)creature;
                PacketSendUtility.SendPacket(targetPlayer, new SM_STATS_INFO(targetPlayer));
            }
        }
        else
            PacketSendUtility.SendMessage(player, "This is not player");
    }

    private void Info(Player player, string message)
    {
        string syntax = "//energy repose|salvation|refresh info|reset|add [points]";
        PacketSendUtility.SendMessage(player, syntax);
    }
}
