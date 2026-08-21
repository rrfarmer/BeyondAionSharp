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
/// <b>The drakan rush is in, and the routes are the reason it can be.</b> Retail's idle timer spawns
/// <b>nineteen</b> <c>IDTiamat_TiamatRush_*</c> drakan across four corner points — 236713-236720, the
/// protectorate elites — every one carrying <c>pathname=path_tiamatdrakan_*</c>.
/// <para>
/// This remark used to end "a server-side walk route we do not have ... they are the only thing here
/// still owed", and that was true when it was written. <b>All twelve routes are now in
/// <c>npc_walker/retail_pattern_paths.xml</c></b>, added by later route-extraction work that never
/// came back to this class. Found by <c>audit_stale_claims.py</c>, which exists because the same thing
/// had happened to Researcher Teselik's four bonus hands.
/// </para>
/// <para>
/// They are placed on retail's own absolute marks and then given their route, rather than at the head
/// of the route: the heads sit between half a metre and nine metres off the marks, and at the second
/// corner it is nine every time. Retail says where they appear and separately where they walk, so both
/// are kept — the same shape <see cref="BergrisarAI"/> uses for its blood wheels.
/// </para>
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

    /// <summary>
    /// The rush: nineteen drakan, four corners, twelve routes. Written out because retail writes it
    /// out — the corners repeat npcs and routes in a pattern that is not derivable from an index.
    /// </summary>
    /// <remarks>
    /// Note the fourth corner has four, not five, and reuses <c>path_tiamatdrakan_4_3</c> for two of
    /// them. Retail does that; a "tidier" four-by-five grid would be an invention.
    /// </remarks>
    internal static readonly (int NpcId, float X, float Y, string Path)[] Rush =
    [
        (236713, 464f, 463f, "path_tiamatdrakan_1_1"),
        (236717, 464f, 463f, "path_tiamatdrakan_1_2"),
        (236716, 464f, 463f, "path_tiamatdrakan_1_3"),
        (236715, 464f, 463f, "path_tiamatdrakan_1_1"),
        (236714, 464f, 463f, "path_tiamatdrakan_1_2"),

        (236713, 461f, 570f, "path_tiamatdrakan_2_1"),
        (236717, 461f, 570f, "path_tiamatdrakan_2_2"),
        (236720, 461f, 570f, "path_tiamatdrakan_2_3"),
        (236719, 461f, 570f, "path_tiamatdrakan_2_1"),
        (236718, 461f, 570f, "path_tiamatdrakan_2_2"),

        (236713, 543f, 463f, "path_tiamatdrakan_3_1"),
        (236717, 543f, 463f, "path_tiamatdrakan_3_2"),
        (236716, 543f, 463f, "path_tiamatdrakan_3_3"),
        (236715, 543f, 463f, "path_tiamatdrakan_3_1"),
        (236714, 543f, 463f, "path_tiamatdrakan_3_2"),

        (236713, 542f, 564f, "path_tiamatdrakan_4_1"),
        (236717, 542f, 564f, "path_tiamatdrakan_4_2"),
        (236715, 542f, 564f, "path_tiamatdrakan_4_3"),
        (236714, 542f, 564f, "path_tiamatdrakan_4_3"),
    ];

    /// <summary>Retail's z on every one of the nineteen.</summary>
    private const float RushZ = 417.4f;

    /// <summary>Retail's <c>FLAGVARI_BETA_1</c> — a world flag, so the rush happens once per instance.</summary>
    private const int RushCalled = 7;

    /// <summary>Retail re-arms the idle timer at two seconds in the same branch.</summary>
    private const int IdleRearmMillis = 2000;

    /// <summary>
    /// Places one of the rush on its mark and starts it down its route.
    /// </summary>
    /// <remarks>
    /// <c>Do.SpawnOnPath</c> would put it at the head of the route instead, which is a different place —
    /// see the class remarks. This keeps retail's mark and retail's route.
    /// </remarks>
    private static readonly PatternAction ChargeTheRaid = ai =>
    {
        foreach ((int npcId, float x, float y, string path) in Rush)
        {
            ai.SpawnAt(npcId, Placed, 0, new SpawnSpot(x, y, RushZ, 0));
            IReadOnlyList<Npc> placed = ai.Spawned(Placed);
            if (placed.Count == 0)
                continue;

            Npc drakan = placed[placed.Count - 1];
            drakan.GetSpawn()?.SetWalkerId(path);
            Aion.GameServer.Ai.Manager.WalkManager.StartWalking((NpcAI)drakan.GetAi());
        }
    };

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail's on_idle_timer, guarded by a world flag so the rush comes once, and re-arming itself.
        OnIdleTimer = Of(
            Branch(8, "the rush", [When.FirstTimeInWorld(RushCalled)],
                Do.SetIdleTimer(IdleRearmMillis),
                ChargeTheRaid)),

        OnWakeUp = Of(
            Branch(13, "SetIdle", When.Always,
                Do.SpawnAt(ShapeChangeFlash, Placed, FlashLife, FlashPoint),
                Do.SpawnNear(InfernoSpirit, Placed, count: 1, range: 0f, liveSeconds: SpiritLife),
                Do.SpawnNear(BurrowingArrival, Placed, count: 1, range: 0f, liveSeconds: ArrivalLife),

                // The step this branch is named after and did not do. Retail's on_wake_up ends with
                // set_idle_timer 2000, which is the only thing that ever starts the idle chain -- so
                // without it the rush below could never fire, whatever else was written.
                Do.SetIdleTimer(IdleRearmMillis))),

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
