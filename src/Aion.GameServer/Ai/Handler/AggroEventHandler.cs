using System;
using Aion.GameServer.Model;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/AggroEventHandler (ATracer). Aggro acquisition + guard/creature support requests. Nested Runnable
/// AggroNotifier -> nested class with Run() invoked via the async ThreadPool idiom (Schedule(ct => {...Run();...}, TimeSpan)).
/// instanceof+cast -> is patterns; CustomPlayerState/NpcTemplateType SCREAMING; AiEventType.CREATURE_NEEDS_SUPPORT ->
/// AiEventType.CreatureNeedsSupport. TribeRelationService/GeoService red-tolerated where unported.
/// </summary>
public class AggroEventHandler
{
    private const int SUPPORT_RANGE_OFFSET = 2; // might be <pushed_range> in client data

    public static void OnAggro(NpcAI npcAI, Creature myTarget)
    {
        Npc owner = npcAI.GetOwner();
        // TODO move out?
        if (myTarget is Player player)
        {
            if (player.IsInCustomState(CustomPlayerState.NEUTRAL_TO_ALL_NPCS))
                return;
        }
        else if (TribeRelationService.IsFriend(owner, myTarget) || myTarget.IsFlag())
            return;
        ThreadPoolManager.GetInstance().Schedule(ct => { new AggroNotifier(owner, myTarget, true).Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(500));
        owner.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnAggro(owner);
    }

    public static bool OnCreatureNeedsSupport(NpcAI npcAI, Creature creatureAskingForSupport)
    {
        Npc owner = npcAI.GetOwner();
        if (!(creatureAskingForSupport.GetTarget() is Creature attacker) || owner.GetAggroList().IsHating(attacker))
            return false;
        if (TribeRelationService.CanHelpCreature(owner, creatureAskingForSupport) && IsInSupportRange(owner, creatureAskingForSupport, attacker))
        {
            ThreadPoolManager.GetInstance().Schedule(ct => { new AggroNotifier(owner, attacker, false).Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(500));
            return true;
        }
        return false;
    }

    public static bool OnCreatureNeedsSupportByGuard(NpcAI npcAI, Creature creatureAskingForSupport)
    {
        Npc owner = npcAI.GetOwner();
        if (owner.GetNpcTemplateType() != NpcTemplateType.GUARD && !owner.GetTribe().IsGuard())
            return false;
        if (!(creatureAskingForSupport.GetTarget() is Player enemy) || owner.GetAggroList().IsHating(enemy))
            return false;
        if (owner.IsEnemy(enemy) && IsInSupportRange(owner, creatureAskingForSupport, enemy))
        {
            ThreadPoolManager.GetInstance().Schedule(ct => { new AggroNotifier(owner, enemy, false).Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(500));
            return true;
        }
        return false;
    }

    private static bool IsInSupportRange(Npc npc, Creature creatureAskingForSupport, Creature target)
    {
        int range = npc.GetAggroRange() + SUPPORT_RANGE_OFFSET;
        return PositionUtil.IsInRange(npc, creatureAskingForSupport, range, false) && GeoService.GetInstance().CanSee(npc, creatureAskingForSupport)
            || PositionUtil.IsInRange(npc, target, range, false) && GeoService.GetInstance().CanSee(npc, target);
    }

    private sealed class AggroNotifier
    {
        private readonly bool broadcast;
        private Npc aggressive;
        private Creature target;

        public AggroNotifier(Npc aggressive, Creature target, bool broadcast)
        {
            this.aggressive = aggressive;
            this.target = target;
            this.broadcast = broadcast;
        }

        public void Run()
        {
            aggressive.GetAggroList().AddHate(target, 1);
            if (broadcast)
                aggressive.GetKnownList().ForEachNpc(obj => obj.GetAi().OnCreatureEvent(AiEventType.CreatureNeedsSupport, aggressive));

            aggressive = null;
            target = null;
        }
    }
}
