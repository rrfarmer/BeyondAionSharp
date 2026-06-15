using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tallocsHollow/TallocsSummonAI (@author xTz).</summary>
[AIName("tallocssummon")]
public class TallocsSummonAI : NpcAI
{
    public TallocsSummonAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId != DialogAction.MAKE_MERCENARY || GetOwner().GetCreator() != null)
            return false;
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        GetOwner().SetCreatorId(player.GetObjectId());
        GetOwner().SetMasterName(player.GetName());
        GetOwner().SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.SendPacket(player, new SM_TRANSFORM_IN_SUMMON(player, GetObjectId()));
        PacketSendUtility.SendPacket(player, new SM_CUSTOM_SETTINGS(GetObjectId(), 0, CreatureType.FRIEND.GetId(), 0));
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
        return true;
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10));
    }
}
