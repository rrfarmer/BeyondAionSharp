using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionMountableAI (@author Cheatkiller, Estrayl).</summary>
[AIName("eternal_bastion_mountable")]
public class EternalBastionMountableAI : ActionItemNpcAI
{
    public EternalBastionMountableAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
            UseNpc(player);
        return true;
    }

    private void UseNpc(Player player)
    {
        if ((GetOwner().GetTribe() == TribeClass.IDF5_TD_WEAPON_PC || GetOwner().GetTribe() == TribeClass.IDF5_TD_WEAPON_PC_DARK))
            TryMountNpc(player, 185000136, 21138 + player.GetRace().GetRaceId());
        else
            TryMountNpc(player, 185000137, 21141);
    }

    private void TryMountNpc(Player player, int keyItemId, int skillId)
    {
        if (!player.GetInventory().DecreaseByItemId(keyItemId, 1))
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
            return;
        }
        World.World.GetInstance().UpdatePosition(player, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), GetPosition().GetHeading());
        PacketSendUtility.BroadcastPacketAndReceive(player, new SM_POSITION(player));
        SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(skillId, player, player);
        AIActions.DeleteOwner(this);
    }
}
