using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tiamat's first form in hard mode (236276). Retail pattern <c>IDTiamat_Hard_Tiamat_Dragon</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The phase before the dying rotation, which
/// <see cref="TiamatDyingRotationAI"/> already covers for both modes. What is translated here is
/// everything in the pattern that resolves; a good deal of it does not, and the split is worth
/// stating precisely.
/// <list type="bullet">
/// <item><b>Waking</b> — a transformation flash on its own mark and, at her feet, a blazing inferno
/// spirit and a burrowing arrival, ten, six and eight seconds. None of the three was spawned by
/// anything.</item>
/// <item><b>Fifteen seconds into the fight</b> — a one-shot that arms a four-second fuse, and on it
/// four drakan mages appear at the four corners of the arena. They are the mechanic this phase is
/// built around and nothing placed them.</item>
/// <item><b>Every ten seconds</b> — retail adds hate to its current target, which this does not
/// translate: there is no vocabulary for a bare hate bump and it changes nothing observable on our
/// threat model.</item>
/// <item><b>Dying</b> — a dust cloud she immediately clears, and the mages go with her. Retail's
/// <c>on_die</c> despawns <c>SPAWN_ID_1</c>, which is the group everything here is filed under.</item>
/// </list>
/// <para>
/// <b>The drakan rush is left out, and the reason is the walk routes.</b> Retail's idle timer spawns
/// <b>nineteen</b> <c>IDTiamat_TiamatRush_*</c> drakan across four corner points. Those eight NPCs do
/// exist in our client — 236713-236720, the protectorate elites — but every spawn carries
/// <c>pathname=path_tiamatdrakan_*</c>, a server-side walk route we do not have. Spawning them anyway
/// would leave nineteen elites standing in the corners instead of charging the raid, which is a
/// different fight rather than a partial one; the audit puts them in its "walks a server-side path"
/// bucket for exactly that reason. Since the rush is the whole of the idle timer, the idle chain is
/// left unarmed rather than ticking on an empty branch. <b>They are the only thing here still owed.</b>
/// </para>
/// <para>
/// <b>The dust cloud is deliberately self-cancelling.</b> Retail files it under <c>SPAWN_ID_1</c> and
/// then, four lines later in the same branch, despawns that group. Translating it literally reproduces
/// a cloud that is placed and removed in one breath — which is what retail does, so it is kept rather
/// than "fixed" into a cloud that lingers.
/// </para>
/// <para>
/// <b>Not translated: the message half.</b> The pattern listens for five message types — 31 arms a
/// twenty-second timer that rebroadcasts to the gods, 38/39/40 each cast a <c>SKILLI_INDEX</c>, and 27
/// removes her — and broadcasts three of its own on entering the fight. Every one of the casts is
/// index-only, and <b>nothing in our tree sends her any of those numbers</b>: the senders are the
/// instance script and her own adds' patterns, neither of which is translated. A listener with no
/// sender is silence, so the pair is left for whenever the instance side is done.
/// </para>
/// <para>
/// <b>Not translated either:</b> the <c>say_to_all</c> lines, the system messages, and the
/// world/condition variables (<c>GOD_SPAWN</c>, <c>TELEPORT_FUTUREIN1..4</c>, <c>SURUKANAFALLING</c>,
/// <c>TIAMAT_SPAWN</c>), which belong to the instance's own sequencing rather than to the fight.
/// </para>
/// </remarks>
[AIName("tiamat_dragon_hard")]
public class TiamatDragonHardAI : PatternAi
{
    private const int InfernoSpirit = 283067;
    private const int BurrowingArrival = 283062;

    /// <summary><c>IDTiamat_Tiamat_ShapeChangeFlash</c> — the transformation she wakes inside.</summary>
    private const int ShapeChangeFlash = 283174;

    /// <summary><c>IDTiamat_Tiamat_Dust</c> — see <see cref="TiamatDragonHardAI"/>'s remarks on dying.</summary>
    private const int ThickDust = 283134;

    /// <summary>Where the flash stands: a few metres west of her, on her own mark rather than hers.</summary>
    private static readonly SpawnSpot FlashPoint = new SpawnSpot(457.9f, 514.5f, 417.6f);

    private const int FlashLife = 10;
    private const int DustLife = 6;

    /// <summary>Retail's <c>SPAWN_ID_1</c>.</summary>
    private const int Placed = 1;

    private const int SpiritLife = 6;
    private const int ArrivalLife = 8;

    private static sbyte Facing(int degrees) =>
        (sbyte)PositionUtil.ConvertAngleToHeading((degrees + 360) % 360);

    /// <summary>
    /// The four corners the mages hold, with the headings retail gives them. Each faces roughly inwards
    /// across the arena rather than out at the wall, which is why the <c>dir</c> is carried.
    /// </summary>
    private static readonly SpawnSpot[] Corners =
    [
        new SpawnSpot(464.159f, 462.677f, 417.5f, Facing(77)),
        new SpawnSpot(464.164f, 566.648f, 417.5f, Facing(42)),
        new SpawnSpot(543.351f, 566.164f, 418f, Facing(17)),
        new SpawnSpot(543.669f, 462.703f, 417.4f, Facing(103)),
    ];

    private static readonly int[] Mages = [856483, 856484, 856485, 856486];

    /// <summary>Retail's <c>FLAGVARI_BETA_1</c> on the portal step — it arms the mages once.</summary>
    private const int PortalArmed = 1;

    private static PatternAction SummonMages => ai =>
    {
        for (int i = 0; i < Mages.Length; i++)
            ai.SpawnAt(Mages[i], Placed, 0, Corners[i]);
    };

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(13, "SetIdle", When.Always,
                Do.SpawnAt(ShapeChangeFlash, Placed, FlashLife, FlashPoint),
                Do.SpawnNear(InfernoSpirit, Placed, count: 1, range: 0f, liveSeconds: SpiritLife),
                Do.SpawnNear(BurrowingArrival, Placed, count: 1, range: 0f, liveSeconds: ArrivalLife))),

        OnEnterAttack = Of(
            Branch(9, "SetTimer", When.Always,
                Do.ArmTimer(0, 10000),
                Do.ArmTimer(1, 15000))),

        OnBattleTimer = Of(
            Branch(7, "PortalSpawnSet", [When.Timer(1), When.FirstTime(PortalArmed)],
                Do.ArmTimer(3, 4000)),

            Branch(6, "SpawnDrakan", [When.Timer(3)],
                SummonMages),

            // Retail's AddHatePoint step. The hate bump is not translated; the heartbeat is, so the
            // chain still ticks as retail's does.
            Branch(5, "AddHatePoint", [When.Timer(0)],
                Do.ArmTimer(0, 10000))),

        // Retail's SurkanaFalling+1, minus the instance's condition variables. The order is retail's own
        // and it is self-cancelling: the dust cloud is filed under SPAWN_ID_1 and the branch's last act
        // is to clear that group, so the cloud is placed and taken away in the same breath. Kept literal
        // -- see the remarks -- and the line that matters for the fight is the despawn: killing her
        // takes the four mages with her.
        OnDie = Of(
            Branch(14, "SurkanaFalling+1", When.Always,
                Do.SpawnNear(ThickDust, Placed, count: 1, range: 0f, liveSeconds: DustLife),
                Do.Despawn(Placed))),
    };

    public TiamatDragonHardAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
