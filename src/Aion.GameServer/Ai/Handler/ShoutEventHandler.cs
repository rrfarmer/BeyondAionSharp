using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Ai.Handler;

/// <summary>Java parity: ai/handler/ShoutEventHandler (Rolandas, Neon).</summary>
public sealed class ShoutEventHandler
{
    public static void OnSee(NpcAI npcAI, Creature target)
    {
        if (target is Aion.GameServer.Model.GameObjects.Players.Player && npcAI.Ask(AIQuestion.CAN_SHOUT))
        {
            Npc npc = npcAI.GetOwner();
            List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.SEE);
            Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, (Aion.GameServer.Model.GameObjects.Players.Player)target, shouts, 0);
        }
    }

    public static void OnSpawn(NpcAI npcAI)
    {
        if (npcAI.Ask(AIQuestion.CAN_SHOUT))
        {
            Npc npc = npcAI.GetOwner();
            Aion.GameServer.Services.NpcShoutsService.GetInstance().RegisterShoutTask(npc);
        }
    }

    public static void OnBeforeDespawn(NpcAI npcAI)
    {
        if (npcAI.Ask(AIQuestion.CAN_SHOUT))
        {
            Npc npc = npcAI.GetOwner();
            List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.BEFORE_DESPAWN);
            Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, null, shouts, 0);
            Aion.GameServer.Services.NpcShoutsService.GetInstance().RemoveShoutCooldown(npc);
        }
    }

    public static void OnReachedWalkPoint(NpcAI npcAI)
    {
        if (npcAI.Ask(AIQuestion.CAN_SHOUT))
        {
            Npc npc = npcAI.GetOwner();
            string walkerId = npc.GetSpawn().GetWalkerId();
            if (walkerId == null)
                return;
            Aion.GameServer.Model.Templates.Npcshout.ShoutEventType shoutType = npc.GetMoveController().IsChangingDirection() ? Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.WALK_DIRECTION : Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.WALK_WAYPOINT;
            List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), shoutType);
            if (shouts == null || shouts.Count == 0)
            {
                Aion.GameServer.Model.Templates.Walker.WalkerTemplate tp = DataManager.WALKER_DATA.GetWalkerTemplate(walkerId);
                int stepCount = tp.GetRouteSteps().Count;
                if (Aion.Commons.Utils.Rnd.NextInt(stepCount) < 2)
                {
                    if (npc.GetTarget() is Aion.GameServer.Model.GameObjects.Players.Player)
                        Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, (Aion.GameServer.Model.GameObjects.Players.Player)npc.GetTarget(), shouts, 0);
                    else
                        Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, null, shouts, 0);
                }
            }
        }
    }

    public static void OnSwitchedTarget(NpcAI npcAI, Creature target)
    {
        if (target is Aion.GameServer.Model.GameObjects.Players.Player && npcAI.Ask(AIQuestion.CAN_SHOUT))
        {
            Npc npc = npcAI.GetOwner();
            List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.SWITCH_TARGET);
            Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, (Aion.GameServer.Model.GameObjects.Players.Player)target, shouts, 0);
        }
    }

    public static void OnDied(NpcAI npcAI)
    {
        if (npcAI.Ask(AIQuestion.CAN_SHOUT))
        {
            Npc npc = npcAI.GetOwner();
            List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.DIED);
            Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, null, shouts, 0);
        }
    }

    /// <summary>Called on Aggro when NPC is ready to attack.</summary>
    public static void OnAttackBegin(NpcAI npcAI)
    {
        if (npcAI.Ask(AIQuestion.CAN_SHOUT))
        {
            Npc npc = npcAI.GetOwner();
            List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.ATTACK_BEGIN);
            Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, null, shouts, 0);
        }
    }

    /// <summary>Handle NPC attacked event (when damage was received or not).</summary>
    public static void OnEnemyAttack(NpcAI npcAI, Creature attacker)
    {
        // TODO: [RR] change AI or randomize behavior for "cowards" and "fanatics" ???
        // TODO: Figure out what the difference between ATTACK_BEGIN and HELP; HELPCALL should make NPC run
        if (npcAI.Ask(AIQuestion.CAN_SHOUT))
        {
            Npc npc = npcAI.GetOwner();
            if (attacker.GetActingCreature() is Aion.GameServer.Model.GameObjects.Players.Player)
            {
                if (npc.GetAttackedCount() == 0)
                {
                    List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.ATTACKED);
                    if (shouts != null && shouts.Count != 0)
                    {
                        Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, (Aion.GameServer.Model.GameObjects.Players.Player)attacker.GetActingCreature(), shouts, 0);
                        return;
                    }
                    shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.HELPCALL);
                    Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, (Aion.GameServer.Model.GameObjects.Players.Player)attacker.GetActingCreature(), shouts, 0);
                }
            }
            else
            {
                List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.ATTACKED);
                if (shouts != null && shouts.Count != 0)
                {
                    Aion.GameServer.Model.Templates.Npcshout.NpcShout shout = Aion.Commons.Utils.Rnd.Get(shouts);
                    Aion.GameServer.Services.NpcShoutsService.GetInstance().Shout(npc, null, shout, shout.GetPollDelay() / 1000);
                }
            }
        }
    }

    public static void OnCast(NpcAI npcAI, Creature firstTarget)
    {
        if (firstTarget is Aion.GameServer.Model.GameObjects.Players.Player && npcAI.Ask(AIQuestion.CAN_SHOUT))
            HandleNumericEvent(npcAI, (Aion.GameServer.Model.GameObjects.Players.Player)firstTarget, Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.CAST_K);
    }

    /// <summary>Handle target attacked events.</summary>
    public static void OnAttack(NpcAI npcAI, Creature attacked)
    {
        if (attacked is Aion.GameServer.Model.GameObjects.Players.Player && npcAI.Ask(AIQuestion.CAN_SHOUT))
            HandleNumericEvent(npcAI, (Aion.GameServer.Model.GameObjects.Players.Player)attacked, Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.ATTACK_K);
    }

    private static void HandleNumericEvent(NpcAI npcAI, Aion.GameServer.Model.GameObjects.Players.Player creature, Aion.GameServer.Model.Templates.Npcshout.ShoutEventType eventType)
    {
        Npc npc = npcAI.GetOwner();
        List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), eventType);
        if (shouts == null || shouts.Count == 0)
            return;

        List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> validShouts = new List<Aion.GameServer.Model.Templates.Npcshout.NpcShout>();
        List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> nonNumberedShouts = new List<Aion.GameServer.Model.Templates.Npcshout.NpcShout>();
        foreach (Aion.GameServer.Model.Templates.Npcshout.NpcShout shout in shouts)
        {
            if (shout.GetSkillNo() == 0)
                nonNumberedShouts.Add(shout);
            else if (shout.GetSkillNo() == npc.GetSkillNumber())
                validShouts.Add(shout);
        }

        Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, creature, validShouts.Count != 0 ? validShouts : nonNumberedShouts, 0);
    }

    public static void OnAttackEnd(NpcAI npcAI)
    {
        if (npcAI.Ask(AIQuestion.CAN_SHOUT))
        {
            Npc npc = npcAI.GetOwner();
            List<Aion.GameServer.Model.Templates.Npcshout.NpcShout> shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(npc.GetPosition().GetMapId(), npc.GetNpcId(), Aion.GameServer.Model.Templates.Npcshout.ShoutEventType.ATTACK_END);
            Aion.GameServer.Services.NpcShoutsService.GetInstance().ShoutRandom(npc, null, shouts, 0);
        }
    }
}
