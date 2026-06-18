using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/See (Mathew).</summary>
public class See : AdminCommand
{
    public See()
        : base("see", "Lets you see hidden npcs and players.")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (admin.GetSeeState() < 2)
        {
            admin.SetSeeState(CreatureSeeState.Search20);
            SendInfo(admin, ChatUtil.L10n(288645)); // Can see targets in advanced hide states.
        }
        else
        {
            admin.UnsetSeeState(CreatureSeeState.Search20);
            SendInfo(admin, "You lost vision.");
        }
        PacketSendUtility.BroadcastPacket(admin, new SM_PLAYER_STATE(admin), true);
        admin.UpdateKnownlist();
    }
}
