using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/VershakaStatueAI (Cheatkiller).</summary>
[AIName("vershakastatue")]
public class VershakaStatueAI : NpcAI
{
    public VershakaStatueAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(300, player, player);
        }

        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }
}
