using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Omega's clone of physical barrier. Retail pattern <c>LF4_FieldRaid_SumD</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Left alone it does not die quietly: at 10% health it
/// detonates — casting Self Destruct, leaving a self-destruct effect and a soul essence behind, and
/// removing itself. Killed outright it still leaves the soul essence. Neither NPC was spawned by
/// anything in our server, and the detonation did not happen at all.
/// <para>
/// The barrier it puts on Omega is aionemu's, not retail's, and is kept: it is what makes killing these
/// clones worth doing. Retail applies that shield from somewhere in Omega's own rotation, which is not
/// resolvable — see below.
/// </para>
/// <para>
/// It also answers Omega's rally call (message 6354) by piling hate on the player he names and
/// turning to attack them.
/// </para>
/// <para>
/// Only skill index 1 is translated. It is anchored hard: its branch fires at 10% immediately before
/// <c>despawn_self</c> and alongside the self-destruct spawn, and our 19196 is named Self Destruct with
/// the stack <c>BNFI_AREABOMB10_LFRAID_SUM</c> — the 10 is the threshold. Indices 0 and 2 have no such
/// anchor: one is fired once each at 70% and 35% and the other every 20s, and our remaining two skills
/// (Protective Wave, an attack; Enervating Wave, a debuff) fit either role. Both keep their npc_skills
/// probabilities rather than being placed on a guess.
/// </para>
/// </remarks>
[AIName("omegaclone")]
public class CloneOfBarrierAI : PatternAi
{
    /// <summary>The shield it holds on Omega while it lives.</summary>
    private const int MagicWard = 18671;

    private const int SelfDestruct = 19196;
    private const int SoulEssence = 281764;
    private const int SelfDestructEffect = 281952;

    /// <summary>Both leavings last ten seconds.</summary>
    private const int EffectLife = 10;

    /// <summary>Retail leaves these unfiled (<c>SPAWN_ID_NONE</c>); nothing ever despawns them.</summary>
    private const int Unfiled = 0;

    /// <summary>Omega's rally call, and the hate his clones put on the player it names.</summary>
    /// <remarks>
    /// The pattern's <c>add_hate_point</c> carries no amount, so this is a judgement: enough to make
    /// the named player the clone's target on arrival, not so much that nothing can pull it off them
    /// afterwards. Marked as inferred in docs/retail-ai-fidelity.md.
    /// </remarks>
    private const int RallyMessage = 6354;
    private const int RallyHate = 1000;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(1, "", When.Always,
                Do.ArmTimer(0, 5000))),

        OnBattleTimer = Of(
            Branch(5, "detonate at 10%", [When.Timer(0), When.HpBelow(10), When.FirstTime(1)],
                Do.ArmTimer(0, 5000),
                Do.SkillOnSelf(SelfDestruct),
                Do.SpawnNear(SoulEssence, Unfiled, liveSeconds: EffectLife),
                Do.SpawnNear(SelfDestructEffect, Unfiled, liveSeconds: EffectLife),
                Do.DespawnSelf()),

            // The heartbeat the threshold branch waits on.
            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, 5000))),

        OnDie = Of(
            Branch(1, "", When.Always,
                Do.SpawnNear(SoulEssence, Unfiled, liveSeconds: EffectLife))),

        // Omega shouts this on every phase, naming whoever he is fighting. A clone that hears it
        // piles hate on that player and turns to attack, which is what makes a wave arrive aimed at
        // the tank instead of wandering to whoever happens to hit it first.
        OnMessage = Of(
            Branch(1, "", [When.Message(RallyMessage)],
                Do.HateMessageTarget(RallyHate))),
    };

    public CloneOfBarrierAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        // delay for spawn animation and because KnownList isn't initialized yet
        ThreadPoolManager.GetInstance().Schedule(
            _ =>
            {
                if (GetKnownList().GetObject(GetCreatorId()) is Npc omega)
                {
                    GetOwner().SetTarget(omega);
                    AIActions.UseSkill(this, MagicWard);
                }

                return ValueTask.CompletedTask;
            },
            3000L);
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        DropTheWard();
    }

    /// <summary>
    /// Detonating removes the clone without killing it, so the ward has to come off here too.
    /// </summary>
    /// <remarks>
    /// <c>despawn_self</c> deletes rather than kills, which does not run <c>HandleDied</c>. Leaving the
    /// removal only there would let a clone that blew itself up leave its shield on Omega permanently
    /// -- the one outcome worse than the clone never detonating.
    /// </remarks>
    protected override void HandleDespawned()
    {
        DropTheWard();
        base.HandleDespawned();
    }

    private void DropTheWard()
    {
        if (GetKnownList().GetObject(GetCreatorId()) is Npc omega)
            omega.GetEffectController().RemoveEffect(MagicWard);
    }
}
