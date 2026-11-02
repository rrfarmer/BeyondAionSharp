using System;
using System.Threading;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/BonusSaamAI.
/// King Saam of the stage 2 bonus: every hit has a 50 % chance to split off a little Saam, up to 30 times, and then he runs from the attacker
/// for 5 seconds. A cut Saam runs from its attacker for 3 seconds with a 50 % chance per hit.
/// </summary>
[AIName("empyrean_bonus_saam")]
public class BonusSaamAI : AggressiveNpcAI
{
    private const int CUT_SAAM = 217738, MAX_PIGLETS = 30;
    private int piglets;
    private readonly AtomicBoolean fleeing = new AtomicBoolean();

    public BonusSaamAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (creature.Equals(GetOwner()) || fleeing.Get() || IsDead() || !Rnd.NextBoolean())
            return;
        if (GetNpcId() == CUT_SAAM)
        {
            Flee(creature, 3000);
        }
        else if (TakePiglet() < MAX_PIGLETS)
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 342352);
            double angle = Math.PI / 180 * Rnd.Get(0, 359);
            float distance = Rnd.Get(1, 10);
            Spawn(CUT_SAAM, GetOwner().GetX() + (float)Math.Cos(angle) * distance, GetOwner().GetY() + (float)Math.Sin(angle) * distance,
                GetOwner().GetZ(), (sbyte)GetOwner().GetHeading(), 0, "empyrean_bonus_saam");
            Flee(creature, 5000);
        }
    }

    /// <summary>Java getAndUpdate(n -> n &lt; MAX_PIGLETS ? n + 1 : n): counts up to the cap and returns the count before this hit.</summary>
    private int TakePiglet()
    {
        while (true)
        {
            int current = Volatile.Read(ref piglets);
            if (current >= MAX_PIGLETS)
                return current;
            if (Interlocked.CompareExchange(ref piglets, current + 1, current) == current)
                return current;
        }
    }

    private void Flee(Creature from, int millis)
    {
        if (fleeing.CompareAndSet(false, true))
            CrucibleRun.Start(this, from, millis, () => fleeing.Set(false));
    }

    protected override void HandleDied()
    {
        fleeing.Set(false);
        base.HandleDied();
    }
}
