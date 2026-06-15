using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/ShifterAI (xTz).</summary>
[AIName("shifter")]
public class ShifterAI : ActionItemNpcAI
{
    public ShifterAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        base.HandleUseItemFinish(player);
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(GetOwner(), EmotionType.EMOTE, 144, 0), true);
    }
}
