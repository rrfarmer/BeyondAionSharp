using Aion.GameServer.Ai;
using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/custom/eternalChallenge/CustomInstanceBulkyAI (Sykra).</summary>
[AIName("custom_instance_bulky")]
public class CustomInstanceBulkyAI : AggressiveNoLootNpcAI
{
    public CustomInstanceBulkyAI(Npc owner)
        : base(owner)
    {
    }

    public override AttackIntention ChooseAttackIntention()
    {
        WorldMapInstance wmi = GetPosition().GetWorldMapInstance();
        if (!(wmi.GetInstanceHandler() is RoahCustomInstanceHandler))
            return base.ChooseAttackIntention();

        VisibleObject target = GetTarget();
        if (!IsDead() && target != null)
        {
            if (!GeoService.GetInstance().CanSee(GetOwner(), target))
            {
                World.World.GetInstance().UpdatePosition(GetOwner(), target.GetX(), target.GetY(), target.GetZ(), (byte)30);
                PacketSendUtility.BroadcastPacketAndReceive(GetOwner(), new SM_FORCED_MOVE(GetOwner(), GetOwner()));
            }
        }
        return base.ChooseAttackIntention();
    }
}
