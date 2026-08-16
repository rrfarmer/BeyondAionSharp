using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The spawn gates Unstable Yamennes opens (283203, 283222, 283223, 283233). Retail patterns
/// <c>IDAbRe_Core_Summon4_02</c>, <c>_3_02</c>, <c>_6_02</c> and <c>_Low_02</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. These are the gates the boss actually opens —
/// both the normal and hard patterns spawn this b-prefixed set exclusively — and none of them was
/// spawned by anything, nor was either thing they call up.
/// <para>
/// A gate starts its own fight and then feeds the room on a timer: the three upper gates put a
/// <b>summoned orkanimum</b> (283200) on a fixed mark every twelve seconds, and the lower gate puts a
/// <b>summoned lapilima</b> (283201) at its own feet every nine. Each arrival lasts a little over a
/// minute, and killing or removing the gate clears everything it has fed out.
/// </para>
/// <para>
/// <b>What this replaces, and what it does not.</b> The class the boss used before —
/// <c>UnstableYamenessPortalSummonedAI</c> on a different set of gate ids — spawns two other npcs at
/// ±3 metres twelve seconds in and once more at seventy-two. That is not this pattern under a
/// different name; it is an invention. This class is the pattern. The boss is repointed to open these
/// gates in <see cref="UnstableYamennesAI"/>.
/// </para>
/// <para>
/// <b>The on-wake summon is what starts everything, and it was nearly left out.</b> Every one of these
/// patterns opens by putting an <c>IDAbRe_Core_Sum_Teleport2_Enemy</c> on itself with a hundred
/// thousand hate and <c>attack_target_after_spawn</c>, so the gate is attacked by its own summon. That
/// is not decoration: <c>on_enter_attack_state</c> is where the feed timer is armed, so without it the
/// gate stands inert forever. An earlier revision of this class called the devname unresolvable in our
/// 4.8 client — it resolves to <b>282016</b>, and the mistake was reading the AI binding table, which
/// maps pattern owners rather than devnames. See docs/retail-ai-fidelity.md.
/// </para>
/// </remarks>
[AIName("yamennes_spawn_gate")]
public class YamennesSpawnGateAI : PatternAi
{
    /// <summary>Retail's <c>SPAWN_ID_1</c>: dying or despawning clears what the gate has fed out.</summary>
    private const int Fed = 1;

    /// <summary>What a gate feeds the room, how often, and where.</summary>
    /// <param name="AtOwnPoint">True for the lower gate, which drops its worm at its own feet.</param>
    private readonly record struct Feed(
        int NpcId, int OpeningMillis, int IntervalMillis, int LiveSeconds,
        bool AtOwnPoint, float X, float Y, float Z);

    private const int Orkanimum = 283200;
    private const int Lapilima = 283201;

    /// <summary><c>IDAbRe_Core_Sum_Teleport2_Enemy</c> — the spawn gate that attacks the gate.</summary>
    private const int TeleportEnemy = 282016;

    private const int EnemyLife = 70;
    private const int EnemyHate = 100000;

    private static readonly Dictionary<int, Feed> ByGate = new Dictionary<int, Feed>
    {
        // IDAbRe_Core_Summon4_02 / _3_02 / _6_02 — a cannon on a fixed mark, every twelve seconds.
        [283203] = new Feed(Orkanimum, 3000, 12000, 70, false, 309.95f, 738.02f, 217.12f),
        [283222] = new Feed(Orkanimum, 3000, 12000, 70, false, 331.33f, 722.18f, 212.93f),
        [283223] = new Feed(Orkanimum, 3000, 12000, 70, false, 348.55f, 741.76f, 212.93f),

        // IDAbRe_Core_Summon4_Low_02 — a worm at its own feet, faster and shorter-lived.
        [283233] = new Feed(Lapilima, 9000, 9000, 13, true, 0f, 0f, 0f),
    };

    private static readonly ConcurrentDictionary<int, AiPattern> ByNpcId = new ConcurrentDictionary<int, AiPattern>();
    private static readonly AiPattern Nothing = new AiPattern();

    private static AiPattern Build(int npcId)
    {
        if (!ByGate.TryGetValue(npcId, out Feed feed))
            return Nothing;

        PatternAction place = feed.AtOwnPoint
            ? Do.SpawnNear(feed.NpcId, Fed, count: 1, range: 0f, liveSeconds: feed.LiveSeconds)
            : Do.SpawnAt(feed.NpcId, Fed, feed.LiveSeconds, new SpawnSpot(feed.X, feed.Y, feed.Z));

        return new AiPattern
        {
            // The gate opens its own fight: it summons a spawn gate on itself that attacks it with a
            // hundred thousand hate. Nothing a player does is required, and without this the feed timer
            // below never starts, because nobody attacks a gate.
            OnWakeUp = Of(
                Branch(3, "", When.Always,
                    Do.SpawnAsMyEnemy(TeleportEnemy, Fed, EnemyLife, EnemyHate))),

            OnEnterAttack = Of(
                Branch(2, "", When.Always,
                    Do.ArmTimer(0, feed.OpeningMillis))),

            OnBattleTimer = Of(
                Branch(1, "", [When.Timer(0)],
                    place,
                    Do.ArmTimer(0, feed.IntervalMillis))),

            // Retail clears the group on both on_die and on_despawn; our runtime raises the first and
            // resets on the second, so the despawn is covered by the pattern reset.
            OnDie = Of(
                Branch(3, "", When.Always,
                    Do.Despawn(Fed))),
        };
    }

    public YamennesSpawnGateAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => Build(id));
}
