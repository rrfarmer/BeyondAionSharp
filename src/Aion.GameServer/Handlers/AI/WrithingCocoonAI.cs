using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tallocsHollow/WrithingCocoonAI (@author xTz).</summary>
[AIName("writhingcocoon")]
public class WrithingCocoonAI : NpcAI
{
    public WrithingCocoonAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SELECT1_1 && player.GetInventory().DecreaseByItemId(185000088, 1))
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            switch (GetNpcId())
            {
                case 730232:
                    Npc npc = GetPosition().GetWorldMapInstance().GetNpc(730233);
                    if (npc != null)
                    {
                        npc.GetController().Delete();
                    }
                    Spawn(799500, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)GetPosition().GetHeading());
                    PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(390510)); // Will you accompany me? Tell me if you will.
                    break;
                case 730233:
                    Npc npc1 = GetPosition().GetWorldMapInstance().GetNpc(730232);
                    if (npc1 != null)
                    {
                        npc1.GetController().Delete();
                    }
                    Spawn(799501, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)GetPosition().GetHeading());
                    PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(390511)); // Let me know if you need my help.
                    break;
            }
            AIActions.DeleteOwner(this);
        }
        else if (dialogActionId == DialogAction.SELECT1_1)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1097));
        }
        return true;
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }
}
