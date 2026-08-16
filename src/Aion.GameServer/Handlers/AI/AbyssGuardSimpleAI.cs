using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/AbyssGuardSimpleAI (Rolandas, Neon).</summary>
/// <remarks>
/// Every override below is the Java class verbatim. What changed is the base: it was
/// <c>AggressiveNpcAI</c> and is now <see cref="PatternAi"/>, which derives from it and adds nothing
/// when the table is empty — every pattern hook returns immediately on a zero-length branch list.
/// <para>
/// The reason is <see cref="AbyssGuardReinforcementAI"/>. Forty-nine abyss guards need this class's
/// aggro rules <i>and</i> the retail reinforcement branches, and C# gives one base class: either the
/// aggro rules get copied into a second class, forking Java-parity code, or this one moves onto the
/// pattern base so a subclass can supply a table. Moving the base is the smaller change and leaves
/// the behaviour identical.
/// </para>
/// </remarks>
[AIName("simple_abyssguard")]
public class AbyssGuardSimpleAI : PatternAi
{
    /// <summary>Nothing, unless a subclass says otherwise.</summary>
    private static readonly AiPattern Nothing = new AiPattern();

    protected override AiPattern Pattern => Nothing;

    public AbyssGuardSimpleAI(Npc owner)
        : base(owner)
    {
    }

    protected override bool CanHandleEvent(AiEventType eventType)
    {
        switch (eventType)
        {
            case AiEventType.CREATURE_MOVED:
                return GetState() != AIState.FIGHT;
        }
        return base.CanHandleEvent(eventType);
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        if (creature is Npc)
            CheckAggro((Npc)creature); // custom checkAggro for npc vs npc
        else
            base.HandleCreatureSee(creature); // calls CreatureEventHandler.checkAggro
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature is Npc)
            CheckAggro((Npc)creature); // custom checkAggro for npc vs npc
        else
            base.HandleCreatureMoved(creature); // calls CreatureEventHandler.checkAggro
    }

    protected override bool HandleCreatureNeedsSupportByGuard(Creature creature)
    {
        return false;
    }

    private void CheckAggro(Npc npc)
    {
        if (IsInState(AIState.FIGHT))
            return;

        if (IsInState(AIState.RETURNING))
            return;

        Npc owner = GetOwner();
        if (npc.IsDead() || !owner.CanSee(npc))
            return;

        if (!owner.IsEnemy(npc) || npc.GetLevel() < 2)
            return;

        // ignore npcs which are under attack
        if (npc.GetTarget() != null)
            return;

        if (!owner.GetPosition().IsMapRegionActive())
            return;

        if (PositionUtil.IsInRange(owner, npc, owner.GetAggroRange()) && GeoService.GetInstance().CanSee(owner, npc))
            OnCreatureEvent(AiEventType.CREATURE_AGGRO, npc);
    }
}
