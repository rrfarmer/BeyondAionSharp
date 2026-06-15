using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theShugoEmperorsVault/IDSweep_In_Npc (Yeats).</summary>
[AIName("idsweep_in_npc")]
public class IDSweep_In_Npc : GeneralNpcAI
{
    public IDSweep_In_Npc(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        TalkEventHandler.OnSimpleTalk((NpcAI)GetOwner().GetAi(), player);
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetOwner().GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.TELEPORT_SIMPLE)
        {
            TeleportService.TeleportTo(player, 301400000, player.GetInstanceId(), 423.715f, 700.375f, 399f, (byte)44, TeleportAnimation.FADE_OUT_BEAM);
        }
        return true;
    }
}
