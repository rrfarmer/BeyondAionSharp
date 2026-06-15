using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// March 25th, 2018: Reduced the damage output of 'Summon Rock' by 50% since on retail templars only receive
/// 2k to 3k damage, which is about 50% of the current. The base damage of this skill is 4500 so it is reduced
/// by something on retail. Maybe remove this hard-coded adjustment if something changes in damage calculations.
///
/// @author Cheatkiller, Estrayl
/// </summary>
[AIName("divisive_creation")]
public class DivisiveCreationAI : AggressiveNpcAI
{
    public DivisiveCreationAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            AIActions.TargetCreature(this, Rnd.Get(instance.GetPlayersInside()));
            SetStateIfNot(AIState.WALKING);
            GetOwner().SetState(CreatureState.ACTIVE, true);
            GetMoveController().MoveToTargetObject();
            PacketSendUtility.BroadcastToMap(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.WALK));
            return ValueTask.CompletedTask;
        }, 5000L);
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        if (effect != null)
        {
            switch (effect.GetSkillId())
            {
                case 20986:
                    damage *= 0.6f;
                    break;
                case 21897:
                case 21898:
                    damage *= 0.5f;
                    break;
            }
        }
        return damage;
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return ItemAttackType.MAGICAL_EARTH;
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
