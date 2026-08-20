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
public class AbyssGuardSimpleAI : PatternAi, INpcMessageListener
{
    /// <summary>
    /// Retail's <c>30002</c> chain for the guards that have one, and nothing for the rest.
    /// </summary>
    /// <remarks>
    /// <b>57 npcs on this class call their killer</b> and they are all Kaldor's village chiefs, which
    /// broadcast in the enter-combat rung itself and then every five seconds. Subclasses that supply
    /// their own table override this, as <see cref="AbyssGuardReinforcementAI"/> does; they lose the
    /// call, and none of them is in the table.
    /// </remarks>
    protected override AiPattern Pattern => ProtectorCalls.PatternFor(GetOwner().GetNpcId());

    /// <summary>
    /// Retail's <c>on_message 30001</c>: the killer has woken, and the chief goes for it.
    /// </summary>
    /// <remarks>
    /// <b>The third side of a loop this class already had two of.</b> These same 57 Kaldor village
    /// chiefs call their killer with 30002 and announce their death with 30003, and could not answer the
    /// killer's own wake-up shout — so a killer spawned beside a village and nothing came.
    /// <para>
    /// It cannot come from the pattern. <see cref="GuardAnswers"/>.<c>RungsFor</c> deliberately skips
    /// sender-targeted answers, because <c>30001</c> names the <em>caller</em> rather than a player and a
    /// rung built for the player-targeted calls would put a million points of hate on the wrong
    /// creature. Only the two siege protector classes handled it in code, and this one was not among
    /// them. <see cref="SummonOrder"/> is retail's <c>add_hate_point</c> + <c>attack_most_hating</c>,
    /// which is what the rung is.
    /// </para>
    /// </remarks>
    public new void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (IsDead() || sender == GetOwner())
            return;

        if (messageType == FortressKillerAI.KillerAwake)
        {
            if (GuardAnswers.Answers(GetNpcId(), FortressKillerAI.KillerAwake))
                SummonOrder.Take(GetOwner(), sender, AbstractSiegeProtectorAI.DropEverything);

            return;
        }

        base.OnNpcMessage(sender, messageType, param);
    }

    /// <summary>
    /// Retail's <c>on_killed_by_user</c> / <c>on_killed_by_npc</c>, for the guards that carry it.
    /// </summary>
    /// <remarks>
    /// <b>57 npcs on this class announce their death and the rest do not</b>, so the rung cannot live in
    /// the pattern above — it is per npc, and <see cref="SiegeDeathCalls"/> is the list. All 57 are
    /// Kaldor's village chiefs: retail's <c>LDF5_Village_chiefNN</c> broadcasts 30003 at fifty metres
    /// when the chief falls, and the killer hunting the village answers by standing down.
    /// <para>
    /// Announced before <c>base.HandleDied()</c> for the same reason every other caller does it: the
    /// broadcast is the head of retail's rung, and what follows can end the fight before it is sent.
    /// </para>
    /// </remarks>
    protected override void HandleDied()
    {
        SiegeDeathCalls.Announce(GetOwner());
        base.HandleDied();
    }

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
