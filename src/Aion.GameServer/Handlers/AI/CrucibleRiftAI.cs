using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("cruciblerift")]
public class CrucibleRiftAI : ActionItemNpcAI
{
    public CrucibleRiftAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        switch (GetNpcId())
        {
            case 730459:
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
                break;
            case 730460:
                TeleportService.TeleportTo(player, 300320000, GetPosition().GetInstanceId(), 1759.5004f, 1273.5414f, 389.11743f, (byte)10);
                Spawn(205679, 1765.522f, 1282.1051f, 389.11743f, (sbyte)0);
                AIActions.DeleteOwner(this);
                break;
        }
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetNpcId() == 730459)
        {
            AnnounceRift();
        }
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1 && GetNpcId() == 730459)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            TeleportService.TeleportTo(player, 300320000, GetPosition().GetInstanceId(), 1759.5946f, 1768.6449f, 389.11758f, (byte)16);
            Spawn(218190, 1760.8701f, 1774.7711f, 389.11743f, (sbyte)110);
            Spawn(218185, 1762.6906f, 1773.863f, 389.11743f, (sbyte)80);
            Spawn(218191, 1763.9441f, 1775.2466f, 389.1175f, (sbyte)80);
            AIActions.DeleteOwner(this);
        }
        return true;
    }

    private void AnnounceRift()
    {
        GetPosition().GetWorldMapInstance().ForEachPlayer(player => PacketSendUtility.SendMonologue(player, 1111482));
    }
}
