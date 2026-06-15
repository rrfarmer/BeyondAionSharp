using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/beshmundirTemple/BigOrbAI (@author Gigi).</summary>
[AIName("bigorb")]
public class BigOrbAI : NpcAI
{
    public BigOrbAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (!IsPortalSpawned()) // Portal isn't spawned
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
        }
        else // Portal is already spawned
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10));
        }
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            Spawn(730276, 1604.6683f, 1606.5886f, 306.8665f, (sbyte)90);
        }
        return false;
    }

    private bool IsPortalSpawned()
    {
        return GetPosition().GetWorldMapInstance().GetNpcs(730276).Count != 0;
    }
}
