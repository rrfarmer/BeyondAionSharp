using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/StragglerPickupAI.
/// Assistant record keepers and sensors left behind in the previous arena and waiting room: they send stragglers after the group, the ones in
/// the arena to the arena of the next stage and the ones in the waiting room to its waiting room.
/// </summary>
[AIName("empyrean_straggler_pickup")]
public class StragglerPickupAI : NpcAI
{
    // arena of the next stage for the arena helpers (205506…205511, 206185…206190), its waiting room for the others
    private static readonly float[][] TARGETS =
    {
        new float[] { 1260.15f, 812.34f, 358.6056f, 90 }, new float[] { 1616.02f, 154.44f, 126f, 10 },
        new float[] { 1793.92f, 796.92f, 469.36542f, 60 }, new float[] { 1776.79f, 1755.59f, 303.69553f, 30 },
        new float[] { 1320.45f, 1738.48f, 316.1746f, 67 }, new float[] { 1762.43f, 1284.99f, 394.23764f, 15 },
        new float[] { 1260.14f, 832.06f, 358.60562f, 43 }, new float[] { 1596.60f, 148.47f, 128.81355f, 77 },
        new float[] { 1819.54f, 796.74f, 470.18304f, 27 }, new float[] { 1776.38f, 1724.55f, 304.039f, 113 },
        new float[] { 1359.50f, 1751.80f, 319.59406f, 30 }, new float[] { 1748.26f, 1254.75f, 394.2378f, 105 },
    };

    public StragglerPickupAI(Npc owner) : base(owner)
    {
    }

    private bool IsSensor()
    {
        return GetNpcId() >= 206185;
    }

    private void SendOn(Player player)
    {
        float[] t = TARGETS[GetNpcId() - (IsSensor() ? 206185 : 205506)];
        TeleportService.TeleportTo(player, GetPosition().GetWorldMapInstance(), t[0], t[1], t[2], (byte)t[3]);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (!IsSensor())
            PacketSendUtility.BroadcastMessage(GetOwner(), 1111506, 1000);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (IsSensor() && creature is Player player && !player.IsDead() && PositionUtil.IsInRange(GetOwner(), player, 10))
            SendOn(player);
    }

    protected override void HandleDialogStart(Player player)
    {
        if (!IsSensor())
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            SendOn(player);
        }
        return true;
    }
}
