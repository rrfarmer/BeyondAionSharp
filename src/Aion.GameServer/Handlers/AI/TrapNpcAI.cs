using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author ATracer
/// </summary>
[AIName("trap")]
public class TrapNpcAI : NpcAI
{
    private ScheduledTask despawnTask;

    public TrapNpcAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        base.HandleCreatureSee(creature);
        TryActivateTrap(creature);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        base.HandleCreatureMoved(creature);
        TryActivateTrap(creature);
    }

    private void TryActivateTrap(Creature creature)
    {
        if (despawnTask != null)
        {
            return;
        }

        if (!creature.IsDead() && !creature.IsInVisualState(CreatureVisualState.BLINKING)
            && IsInRange(creature, GetOwner().GetGameStats().GetAttackRange().GetCurrent()))
        {
            Creature creator = (Creature)GetCreator();
            if (!creator.IsEnemy(creature))
            {
                return;
            }
            Explode(creature);
        }
    }

    protected override void HandleSpawned()
    {
        string name = GetObjectTemplate().GetName().ToLower();
        if (!name.Equals("scrapped mechanisms"))
            GetOwner().SetVisualState(CreatureVisualState.HIDE1);
        base.HandleSpawned();
        if (name.Equals("shock trap"))
            Explode(GetOwner());
    }

    private void Explode(Creature creature)
    {
        if (SetStateIfNot(AIState.FIGHT))
        {
            GetOwner().UnsetVisualState(CreatureVisualState.HIDE1);
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_PLAYER_STATE(GetOwner()));
            AIActions.TargetCreature(this, creature);
            GetOwner().UpdateKnownlist();
            NpcSkillEntry npcSkill = GetSkillList().GetRandomSkill();
            if (npcSkill != null)
            {
                AIActions.UseSkill(this, npcSkill.GetSkillId());
            }
            despawnTask = ThreadPoolManager.GetInstance().Schedule(_ => { AIActions.DeleteOwner(this); return ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(5000));
        }
    }

    public override bool IsMoveSupported()
    {
        return false;
    }

    protected override bool CanHandleEvent(AiEventType eventType)
    {
        if (eventType == AiEventType.CREATURE_MOVED)
            return true;
        return base.CanHandleEvent(eventType);
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
