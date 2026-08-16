using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The eight gateway guards of Inggison and Gelkmaros. Retail patterns <c>GwLGuard_FlA</c> (Elyos)
/// and <c>GwDGuard_FlA</c> (Asmodian).
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Four LEGENDARY guards a side — Trigon, Lord
/// Skyrose, Lord Agios and Lady Eiros in Inggison; Matigium, Sands Kukinsia, Sibarum Darkwing and
/// Revolver Blackhands in Gelkmaros — all eight on plain <c>aggressive</c>, and <b>all eight trap
/// types they lay were spawned by nothing anywhere</b>.
/// <para>
/// The two patterns are identical bar the faction prefix on the trap names, so one table serves both
/// and the ids are chosen per guard.
/// </para>
/// <para>
/// <b>A trap ladder, one rung at a time.</b> A snare goes down the moment the fight starts, and then
/// one more at each of three thresholds, each a one-shot:
/// </para>
/// <list type="table">
/// <item><term>on engaging</term><description>snare trap</description></item>
/// <item><term>below 70</term><description>throw trap</description></item>
/// <item><term>below 50</term><description>explosion trap</description></item>
/// <item><term>below 30</term><description>mine trap</description></item>
/// </list>
/// <para>
/// Every trap lands within two metres of the guard and lasts a minute. Below 10 it calls out once more
/// but lays nothing.
/// </para>
/// <para>
/// <b>The empty rungs are kept.</b> Retail interleaves one-shots at 60, 40 and 20 that only cast, and
/// they are reproduced here as bare re-arms even though the casts are not translated. They are not
/// decoration: each occupies the timer-0 tick it fires on, so dropping them would bring every trap
/// below it forward by five seconds.
/// </para>
/// <para>
/// <b>The casts are not translated.</b> Ten indices are addressed across the pattern and neither
/// guard has branch comments naming a skill. Omitted with them: the timer-1 ladder that casts a
/// different skill in each health band, and timer 2's coin-flip pair on a fifteen-minute fuse. Also
/// not translated: the four shouts and the four broadcasts to twenty-five metres that accompany the
/// rungs, which have no numeric ids in our data.
/// </para>
/// </remarks>
[AIName("gateway_guard")]
public class GatewayGuardAI : PatternAi
{
    /// <summary>The four traps a guard carries, in the order it lays them.</summary>
    private readonly record struct Traps(int Snare, int Throw, int Explosion, int Mine);

    private static readonly Traps Elyos = new Traps(281472, 281473, 281474, 281475);
    private static readonly Traps Asmodian = new Traps(281482, 281483, 281484, 281485);

    private static readonly Dictionary<int, Traps> ByGuard = new Dictionary<int, Traps>
    {
        // Inggison
        [296444] = Elyos,   // trigon
        [296488] = Elyos,   // lord skyrose
        [296489] = Elyos,   // lord agios
        [296490] = Elyos,   // lady eiros

        // Gelkmaros
        [296453] = Asmodian, // matigium
        [296492] = Asmodian, // sands kukinsia
        [296493] = Asmodian, // sibarum darkwing
        [296494] = Asmodian, // revolver blackhands
    };

    /// <summary>Retail's <c>SPAWN_ID_NONE</c>: nothing clears them as a group, the minute does.</summary>
    private const int Untracked = 0;

    private const int TrapLife = 60;
    private const float AtItsFeet = 2f;

    // Retail's ALPHA_1..4 and BETA_1..3, one per rung of the ladder.
    private const int Below10 = 1;
    private const int Below20 = 2;
    private const int Below30 = 3;
    private const int Below40 = 4;
    private const int Below50 = 5;
    private const int Below60 = 6;
    private const int Below70 = 7;

    private static PatternAction Lay(System.Func<Traps, int> which) => ai =>
    {
        if (ByGuard.TryGetValue(ai.GetOwner().GetNpcId(), out Traps traps))
            ai.SpawnNear(which(traps), Untracked, count: 1, range: AtItsFeet, liveSeconds: TrapLife);
    };

    /// <summary>A rung that lays a trap.</summary>
    private static PatternBranch Rung(int priority, int below, int flag, System.Func<Traps, int> which)
        => Branch(priority, $"below {below}", [When.Timer(0), When.HpBelow(below), When.FirstTime(flag)],
            Do.ArmTimer(0, 5000),
            Lay(which));

    /// <summary>A rung that only casts, and so only spends its tick. See the class remarks.</summary>
    private static PatternBranch EmptyRung(int priority, int below, int flag)
        => Branch(priority, $"below {below}", [When.Timer(0), When.HpBelow(below), When.FirstTime(flag)],
            Do.ArmTimer(0, 5000));

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(1, "", When.Always,
                Do.ArmTimer(0, 5000),
                Lay(t => t.Snare))),

        OnBattleTimer = Of(
            // Deepest threshold first, which is what lets a guard that drops fast skip straight to the
            // rung it deserves rather than walking every one on the way down.
            EmptyRung(99, below: 10, flag: Below10),
            EmptyRung(98, below: 20, flag: Below20),
            Rung(97, below: 30, flag: Below30, t => t.Mine),
            EmptyRung(96, below: 40, flag: Below40),
            Rung(95, below: 50, flag: Below50, t => t.Explosion),
            EmptyRung(94, below: 60, flag: Below60),
            Rung(6, below: 70, flag: Below70, t => t.Throw),

            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, 5000))),
    };

    public GatewayGuardAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
