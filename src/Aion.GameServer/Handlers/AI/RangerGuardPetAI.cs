using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The ranger guards' pet — a one-shot trap layer. Retail patterns <c>BGuard_RhAPet*</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The last unread group in the guard families, and
/// the tidiest: <b>twenty patterns, twenty-seven pets, one shape</b> — every branch spawns at two
/// metres, for ten minutes, on a target within fifty. Nothing had to be judged.
/// <para>
/// Attack a pet and it lays a trap on you and disappears. It is not a fighter at all: it exists to
/// place one thing and go, and walking away makes it leave rather than follow. Each level bracket has
/// its own trap npc, which is the only thing that differs between the twenty patterns.
/// </para>
/// <para>
/// <b>Retail lays the trap on <c>OBJI_EVENT_TARGET</c></b> — whoever just attacked it — where this
/// uses the pet's current target. For an NPC whose whole life is the moment it is first hit those are
/// the same creature; the distinction would matter only to something that survived to be attacked by
/// a second player, and this does not.
/// </para>
/// <para>
/// <b>The casts are not translated, and could not fire anyway.</b> Retail casts index 0 on itself and
/// index 1 on the target in the same breath as <c>despawn_self</c>. Two indices against pets whose
/// skill lists are a bare count match is not a resolution — and a queued cast does not survive a
/// despawn in the same branch, so translating them would have been inert on top of unfounded.
/// </para>
/// </remarks>
[AIName("ranger_guard_pet")]
public class RangerGuardPetAI : PatternAi
{
    /// <summary>Retail's <c>SPAWN_ID_1</c>. The pet is gone before anything could despawn it.</summary>
    private const int Laid = 1;

    /// <summary>Uniform across all twenty patterns: two metres, ten minutes.</summary>
    private const float AtTheTarget = 2f;
    private const int TrapLife = 600;

    /// <summary>Which trap each pet lays. The level bracket is the only thing that varies.</summary>
    private static readonly Dictionary<int, int> TrapByPet = new Dictionary<int, int>
    {
        [207674] = 207634, [207679] = 207639, [207685] = 207684, [207691] = 207690,
        [207698] = 296618, [207813] = 294707, [207824] = 294740, [294709] = 294707,
        [294710] = 294707, [294711] = 294707, [294742] = 294740, [294743] = 294740,
        [294744] = 294740, [295132] = 295131, [295143] = 295142, [296062] = 296061,
        [296073] = 296072, [296622] = 296618, [296623] = 296619, [296624] = 296620,
        [296625] = 296621, [296732] = 296728, [296733] = 296729, [296734] = 296730,
        [296735] = 296731, [296867] = 296617, [296875] = 296727,
    };

    private static readonly ConcurrentDictionary<int, AiPattern> ByNpcId = new ConcurrentDictionary<int, AiPattern>();
    private static readonly AiPattern Nothing = new AiPattern();

    private static AiPattern Build(int npcId)
    {
        if (!TrapByPet.TryGetValue(npcId, out int trap))
            return Nothing;

        return new AiPattern
        {
            OnEnterAttack = Of(
                Branch(7, "", When.Always,
                    Do.SpawnOnTarget(trap, Laid, count: 1, range: AtTheTarget, liveSeconds: TrapLife),
                    Do.DespawnSelf())),

            OnLeaveAttack = Of(
                Branch(7, "", When.Always,
                    Do.DespawnSelf())),
        };
    }

    public RangerGuardPetAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => Build(id));
}
