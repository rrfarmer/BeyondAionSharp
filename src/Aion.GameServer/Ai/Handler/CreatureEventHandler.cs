using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/CreatureEventHandler (ATracer). Creature-moved/-see aggro detection. instanceof+cast -> is patterns;
/// AISubState.TARGET_LOST/NONE; AIState.FIGHT/RETURNING; AIEventType.CREATURE_AGGRO -> AiEventType.CreatureAggro;
/// CreatureVisualState.BLINKING -> .Blinking; CustomPlayerState/NpcTemplateType/AbnormalState SCREAMING. Quest/Tribe/Geo red-tolerated.
/// </summary>
public class CreatureEventHandler
{
    public static void OnCreatureMoved(NpcAI npcAI, Creature creature)
    {
        CheckAggro(npcAI, creature);
        if (creature is Player player)
        {
            QuestEngine.GetInstance().OnAtDistance(new QuestEnv(npcAI.GetOwner(), player, 0));
        }
    }

    public static void OnCreatureSee(NpcAI npcAI, Creature creature)
    {
        if (npcAI.IsInSubState(AiSubState.TargetLost) && creature.Equals(npcAI.GetTarget()))
        { // see target again after hide end
            npcAI.SetSubStateIfNot(AiSubState.None);
            if (npcAI.IsInState(AiState.Fight))
            { // continue to attack
                AttackManager.ScheduleNextAttack(npcAI);
                return;
            }
        }
        CheckAggro(npcAI, creature);
        if (creature is Player player)
        {
            QuestEngine.GetInstance().OnAtDistance(new QuestEnv(npcAI.GetOwner(), player, 0));
        }
    }

    internal static void CheckAggro(NpcAI ai, Creature creature)
    {
        if (ai.IsInState(AiState.Fight))
            return;

        if (ai.IsInState(AiState.Returning))
            return;

        if (creature.IsDead())
            return;

        if (creature.IsInVisualState(CreatureVisualState.Blinking))
            return;

        Npc owner = ai.GetOwner();
        if (!owner.IsSpawned())
            return;

        if (!owner.CanSee(creature))
            return;

        if (owner.GetEffectController().IsAbnormalSet(AbnormalState.SANCTUARY))
            return;

        if (!owner.GetPosition().IsMapRegionActive())
            return;

        if (IsInSeeRange(creature, owner))
        {
            ai.HandleCreatureDetected(creature); // TODO: Move to AIEventType, prevent calling multiple times
            bool isPlayer = creature is Player;
            if (isPlayer && ((Player)creature).IsInCustomState(CustomPlayerState.ENEMY_OF_ALL_NPCS)
                || TribeRelationService.IsAggressive(owner, creature) && (isPlayer || creature.IsEnemyFrom(owner)))
            { // aggressive mob
                if (ValidateAggro(owner, creature) && GeoService.GetInstance().CanSee(owner, creature))
                {
                    ShoutEventHandler.OnSee(ai, creature);
                    if (ai.CanThink())
                        ai.OnCreatureEvent(AiEventType.CreatureAggro, creature);
                }
            }
            else
            { // non aggressive (should we consider also using geo canSee checks here?)
                ShoutEventHandler.OnSee(ai, creature);
            }
        }
    }

    private static bool IsInSeeRange(Creature creature, Npc npc)
    {
        if (npc.GetAggroAngle() == 0 || npc.GetAggroRange() == 0)
            return false;
        if (!PositionUtil.IsInRange(npc, creature, npc.GetAggroRange(), false))
            return false;
        return PositionUtil.IsInFrontOf(creature, npc, npc.GetAggroAngle() / 2f) || PositionUtil.IsInRange(npc, creature, npc.GetShortAggroRange(), false);
    }

    private static bool ValidateAggro(Npc owner, Creature creature)
    {
        return creature.GetLevel() - owner.GetLevel() < 10 || owner.GetObjectTemplate().GetNpcTemplateType() == NpcTemplateType.GUARD
            || owner.GetObjectTemplate().GetNpcTemplateType() == NpcTemplateType.ABYSS_GUARD;
    }
}
