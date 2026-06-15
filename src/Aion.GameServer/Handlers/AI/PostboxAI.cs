using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/PostboxAI (ATracer).</summary>
[AIName("postbox")]
public class PostboxAI : NpcAI
{
    public PostboxAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        player.GetMailbox().mailBoxState = PlayerMailboxState.REGULAR;
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.MAIL.Id()));
    }

    protected override void HandleDialogFinish(Player player)
    {
    }
}
