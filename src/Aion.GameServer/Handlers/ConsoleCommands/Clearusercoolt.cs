using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Clearusercoolt (ginho1). Clears instance (portal) cooldowns for a named player.</summary>
public class Clearusercoolt : ConsoleCommand
{
    public Clearusercoolt()
        : base("clearusercoolt", "Clears cooldowns for instances.")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        string playerName = ChatUtil.GetRealCharName(paramsArr[0], true);
        Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(playerName);
        if (player == null)
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
            return;
        }
        ClearAllInstanceCooldowns(admin, player);
    }

    public static void ClearAllInstanceCooldowns(Player admin, Player player)
    {
        if (player.GetPortalCooldownList().GetPortalCoolDowns() == null)
        {
            PacketSendUtility.SendMessage(admin, (player.Equals(admin) ? "You have" : player.GetName() + " has") + " no instance cooldowns to remove.");
            return;
        }

        List<int> worldIds = player.GetPortalCooldownList().GetPortalCoolDowns().Keys.ToList();
        player.GetPortalCooldownList().SetPortalCoolDowns(null);
        foreach (int worldId in worldIds)
            player.GetPortalCooldownList().SendEntryInfo(worldId);

        if (player.Equals(admin))
        {
            PacketSendUtility.SendMessage(admin, "Your instance cooldowns were removed.");
        }
        else
        {
            PacketSendUtility.SendMessage(admin, "You have removed instance cooldowns of " + player.GetName() + '.');
            PacketSendUtility.SendMessage(player, admin.GetName(true) + " removed your instance cooldowns.");
        }
    }
}
