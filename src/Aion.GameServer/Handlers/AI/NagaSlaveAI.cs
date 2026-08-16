using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Sorcerer's minion (290127), the naga captains' slave. Retail pattern <c>Naga_AWizardSlave</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The other half of <see cref="NagaCaptainAI"/>:
/// four of these arrive on the current target between 41 and 60, and when the captain drops to 21-40
/// it calls <b>3315</b> and every one of them detonates.
/// <para>
/// <b>Both skill indices resolve, and the roles corroborate each other.</b> Two indices are addressed
/// and the npc has exactly two skills. Index 0 is cast on waking and again on leaving the fight, and
/// is <c>16921 Fire Sparkle</c>, the list's only BUFF — the shape of a self-buff on arrival. Index 1 is
/// cast in the same breath as despawning, and is <c>16991 Explosion</c>. A minion that buffs itself
/// when it appears and explodes when dismissed is a mechanic; the reverse would be nonsense, so the
/// identity mapping is not merely the default here but the only one that reads.
/// </para>
/// </remarks>
[AIName("naga_slave")]
public class NagaSlaveAI : PatternAi
{
    private const int FireSparkle = 16921;   // index 0
    private const int Explosion = 16991;     // index 1

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(2, "", When.Always, Do.SkillOnSelf(FireSparkle))),

        OnMessage = Of(
            Branch(1, "dismissed", [When.Message(NagaCaptainAI.Dismiss)],
                Do.SkillOnSelf(Explosion),
                Do.DespawnSelf())),

        OnLeaveAttack = Of(
            Branch(3, "", When.Always, Do.SkillOnSelf(FireSparkle))),
    };

    public NagaSlaveAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
