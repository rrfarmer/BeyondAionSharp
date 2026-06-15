using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/IDTiamat_2_DrakanNpcAI (Estrayl).</summary>
[AIName("IDTiamat_2_Drakan_NPC")]
public class IDTiamat_2_DrakanNpcAI : OneDmgAI
{
    public IDTiamat_2_DrakanNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return damage;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        AIActions.TargetCreature(this, Rnd.Get(GetPosition().GetWorldMapInstance().GetPlayersInside()));
        SetStateIfNot(AIState.WALKING);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        GetMoveController().MoveToTargetObject();
        PacketSendUtility.BroadcastToMap(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.WALK));
    }
}
