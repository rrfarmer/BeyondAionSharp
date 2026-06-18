using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Resurrect (ginho1). Resurrects the dead admin or selected player.</summary>
public class Resurrect : ConsoleCommand
{
    public Resurrect()
        : base("resurrect", "Resurrects a player.")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        VisibleObject candidate = admin.IsDead() ? admin : admin.GetTarget();
        if (candidate is not Player player)
        {
            SendInfo(admin, "Please select a player.");
            return;
        }

        if (!player.IsDead())
        {
            SendInfo(admin, player.Equals(admin) ? "You're already alive." : player.GetName() + " is already alive.");
            return;
        }

        player.SetPlayerResActivate(true);
        PacketSendUtility.SendPacket(player, new SM_RESURRECT(admin));
    }
}
