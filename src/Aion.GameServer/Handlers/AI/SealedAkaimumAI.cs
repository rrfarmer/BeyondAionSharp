using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The sealed akaimum (280973) that walks the hall above the Silikor of Memory. Retail pattern
/// <c>ND2_WhG3</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The two silikor guards come back.</b> A guard brought down leaves a marker for twelve seconds
/// (see <see cref="SilikorGuardMarkerAI"/>); the marker shouts <c>6620</c> a hundred metres; and the
/// akaimum answers by standing a new guard of that kind back on its own spot. So clearing the hall
/// takes killing the akaimum, not the guards — which is the point of it, and none of it existed here.
/// </para>
/// <para>
/// <b>Which guard it re-places is read from the marker's npc id, not from retail's own test.</b>
/// Retail's two branches are guarded on <c>is_message 6620</c> plus a bare <c>&lt;is_race/&gt;</c> that
/// carries no argument in the dump at all — with first-match-wins those two guards are identical, so
/// as written the second branch could never fire. The mechanic is unambiguous even where the
/// discriminator is not: a melee marker brings back a melee guard. Recorded rather than guessed at.
/// </para>
/// <para>
/// <b>Not translated:</b> the waypoint work — retail's <c>goto_waypoint</c>, its arrival branch, and
/// the return to waypoint 14 when a guard dies within ten metres; our pattern runtime has no
/// vocabulary for paths and the akaimum already carries a walker route in the spawn file. The two
/// guards it places on waking, which our spawn file already stands there. And <c>6621</c>, retail's
/// "clear the hall", whose only senders are the boss's <c>on_spelled</c> branch and a marker it drops
/// on walking home: both are events our runtime does not raise separately, and answering it would
/// delete statically-spawned NPCs with nothing to bring them back.
/// </para>
/// </remarks>
[AIName("sealed_akaimum")]
public class SealedAkaimumAI : PatternAi
{
    /// <summary>Retail's message: a guard has fallen, and where.</summary>
    public const int GuardDown = 6620;

    /// <summary>
    /// Retail <c>6621</c> — the silikor telling this akaimum to leave, and take its guards with it.
    /// </summary>
    /// <remarks>
    /// <b>Nothing sends it yet.</b> Retail sends it from the silikor <c>on_spelled</c>, guarded on a
    /// neutral-race caster and on consuming the <i>world</i> flag this akaimum sets when it re-places a
    /// guard. This port has no world flags, so the sending half cannot be built; the branch is written
    /// because it is unambiguous on its own and because a listener without a sender is the harmless
    /// half of the pair.
    /// </remarks>
    public const int Dismissed = 6621;

    /// <summary>Retail <c>STR_CHAT_BIDLF2A_HolyServantSum_Roamer_50_n_AIPattern_6</c>.</summary>
    private const int Farewell = 1500673;

    /// <summary>Retail's <c>range_as_meter</c> on the marker's shout.</summary>
    public const float Reach = 100f;

    private const int MeleeGuard = 280971;
    private const int CasterGuard = 280972;

    /// <summary>Retail's own placements, from <c>ND2_WhG3</c>'s <c>on_wake_up</c>.</summary>
    private static readonly SpawnSpot MeleePost = new SpawnSpot(377.24f, 762.6f, 189.2f);
    private static readonly SpawnSpot CasterPost = new SpawnSpot(407.19f, 762.6f, 189.2f);

    /// <summary>Retail's <c>SPAWN_ID_2</c> for the melee guard and <c>SPAWN_ID_1</c> for the caster.</summary>
    private const int MeleePlace = 2;
    private const int CasterPlace = 1;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            // Retail p5, above both guard branches: the silikor dismisses this akaimum and everything it
            // has standing. Its own spawn ids are used, so a guard re-placed a moment earlier goes with
            // it rather than being left behind with nothing to guard.
            Branch(5, "the silikor sends it away", [When.Message(Dismissed)],
                Do.Say(Farewell),
                Do.Despawn(CasterPlace),
                Do.Despawn(MeleePlace),
                Do.DespawnSelf()),

            Branch(2, "a melee guard fell", [When.Message(GuardDown), When.SenderIs(SilikorGuardMarkerAI.MeleeMarker)],
                Do.SpawnAt(MeleeGuard, MeleePlace, 0, MeleePost)),

            Branch(1, "a caster guard fell", [When.Message(GuardDown), When.SenderIs(SilikorGuardMarkerAI.CasterMarker)],
                Do.SpawnAt(CasterGuard, CasterPlace, 0, CasterPost))),
    };

    public SealedAkaimumAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The twelve-second markers a dying silikor guard leaves (281034 melee, 281035 caster). Retail
/// pattern <c>ND2_WhG4</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One line each: <b>shout a hundred metres on
/// waking</b>, so the akaimum knows to stand a replacement up. The twelve seconds are retail's
/// <c>live_time</c> and are what keeps a corpse from calling twice.
/// <para>
/// <b>The shout waits a second, and it has to.</b> A just-spawned NPC has an empty known list, so
/// <see cref="NpcMessageBus"/> falls back to scanning the sender's own map region — and this shout
/// carries a hundred metres, far enough to cross one. Measured: a marker left where the melee guard
/// stands is a region away from the akaimum and its <c>on_wake_up</c> broadcast reached nothing, while
/// the caster's, the same distance off but inside the region, arrived. One idle tick is enough for the
/// real known list to exist, and twelve seconds of life to spend it in.
/// </para>
/// </remarks>
[AIName("silikor_guard_marker")]
public class SilikorGuardMarkerAI : PatternAi
{
    /// <summary><c>BIDLF2A_HolyServantSum_MeleeDespawn_50_n</c> and its caster twin.</summary>
    public const int MeleeMarker = 281034;
    public const int CasterMarker = 281035;

    /// <summary>Ours, not retail's: see the remarks.</summary>
    private const int SettleMillis = 1000;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(8, "", When.Always,
                Do.SetIdleTimer(SettleMillis))),

        OnIdleTimer = Of(
            Branch(7, "", When.Always,
                Do.Broadcast(SealedAkaimumAI.GuardDown, SealedAkaimumAI.Reach))),
    };

    public SilikorGuardMarkerAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
