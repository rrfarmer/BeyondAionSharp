using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Chaoslord Kalabar of Eltnen and Visionmaster Omutata of Morheim. Retail pattern <c>NKrall_WhA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>He builds one thing at ninety, trades it for a
/// different thing at sixty, and detonates that at thirty-five.</b> Three health bands, one add each
/// time, and the add is the band.
/// <para>
/// <b>At ninety a wheel of death, at sixty a stone guard — and the wheel is dismissed in the same
/// breath the guard is made.</b> Retail's second band spawns into group two and despawns group one in
/// one branch, so the two adds are never both up. A raid that leaves the wheel alone finds it gone;
/// one that kills it early has changed nothing.
/// </para>
/// <para>
/// <b>At thirty-five he calls <c>3008</c>, and the stone guard answers by destroying itself.</b> That
/// is the whole of <see cref="StoneGuardAI"/> — the guard exists to be spent.
/// </para>
/// <para>
/// <b>Both spawn groups are cleared when he dies and when he leaves the fight</b>, which retail writes
/// as its own handlers rather than relying on <c>despawn_at_attack_state</c>. Unusually explicit, and
/// it means the adds cannot be pulled away and kept.
/// </para>
/// <para>
/// <b>Not translated:</b> six skills — the opener, the two self-casts that dress each band change, and
/// the low-band attack the second timer paces. That timer is built anyway: its re-arm is real state
/// even with nothing hanging off it, and dropping it would silently change the pattern's shape.
/// </para>
/// <para>
/// <b>Two gaps that are retail's own.</b> Above ninety there is no band at all — he simply heartbeats
/// — and health of exactly thirty-five belongs to no band, the same off-by-one Guardian Vingeveu
/// carries. Kept, both of them.
/// </para>
/// <para>
/// <b>And one dead action:</b> <c>on_enter_idle_state</c> sets <c>FLAGVARI_ZETA_5</c>, which no branch
/// in the pattern ever reads. Not built.
/// </para>
/// </remarks>
[AIName("chaoslord_kalabar")]
public class ChaoslordKalabarAI : PatternAi
{
	/// <summary>Retail's <c>3008</c>: go off. Answered only by the stone guard.</summary>
	public const int GoOff = 3008;

	/// <summary>Retail's <c>range_as_meter</c> on that call.</summary>
	public const float CallReach = 50f;

	/// <summary>The wheel of death, retail's <c>BLF2_NM2_RollingWheel_40_An</c>.</summary>
	/// <remarks>
	/// <b>Its own retail pattern, <c>ND2_RnJ</c>, is in neither the 2.7 nor the 5.8 dump</b> — the client
	/// names it and no leaked file carries it. So the wheel spawns and behaves as an ordinary monster
	/// here, which is a gap in the source rather than in the port. See docs/retail-ai-fidelity.md.
	/// </remarks>
	public const int WheelOfDeath = 280357;

	/// <summary>The stone guard, retail's <c>BLF2_NM2_SouledstoneSu_40_An</c>.</summary>
	public const int StoneGuard = 280356;

	// Retail's SPAWN_ID_1 and SPAWN_ID_2, which the despawn handlers name.
	private const int WheelGroup = 1;
	private const int GuardGroup = 2;

	private const int Heartbeat = 0;
	private const int LowBandTimer = 1;

	// One flag per band. Retail uses DELTA_3 for the highest and DELTA_1 for the lowest, which reads
	// backwards and is kept as written.
	private const int OpenedHigh = 3;
	private const int OpenedMiddle = 2;
	private const int OpenedLow = 1;

	/// <summary>Retail's <c>spawn_range</c> and <c>live_time</c> on both adds.</summary>
	private const float SpawnSpread = 6f;

	private const int LiveSeconds = 3000;

	/// <summary>Retail's <c>is_hp_lower_than percent=35</c>, against the middle band's <c>36</c>.</summary>
	private const int Low = 35;

	private static readonly PatternAction ClearBothGroups = Do.Custom(ai =>
	{
		ai.DespawnGroup(GuardGroup);
		ai.DespawnGroup(WheelGroup);
	});

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(4, "engaging", [], Do.ArmTimer(Heartbeat, 7_000))),

		OnDie = Of(Branch(9, "dying, and taking them with him", [], ClearBothGroups)),

		OnLeaveAttack = Of(Branch(3, "leaving the fight, and taking them with him", [], ClearBothGroups)),

		OnBattleTimer = Of(
			// Nothing hangs off this but a skill. The re-arm is kept: it is real state, and a pattern
			// missing a timer is a different pattern.
			Branch(8, "low band, keeping its own timer",
				[When.Timer(LowBandTimer), When.HpBelow(30)],
				Do.ArmTimer(LowBandTimer, 16_000)),

			Branch(7, "low band, opening it",
				[When.Timer(Heartbeat), When.HpBelow(Low), When.FirstTime(OpenedLow)],
				Do.ArmTimer(Heartbeat, 7_000),
				Do.ArmTimer(LowBandTimer, 10_000),
				Do.Broadcast(GoOff, CallReach, aboutTarget: true)),

			Branch(6, "middle band: the guard replaces the wheel",
				[When.Timer(Heartbeat), When.HpBetween(36, 60), When.FirstTime(OpenedMiddle)],
				Do.ArmTimer(Heartbeat, 7_000),
				Do.SpawnNear(StoneGuard, GuardGroup, 1, SpawnSpread, LiveSeconds),
				Do.Despawn(WheelGroup),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(5, "high band: the wheel",
				[When.Timer(Heartbeat), When.HpBetween(61, 90), When.FirstTime(OpenedHigh)],
				Do.ArmTimer(Heartbeat, 7_000),
				Do.SpawnNear(WheelOfDeath, WheelGroup, 1, SpawnSpread, LiveSeconds),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(1, "the heartbeat",
				[When.Timer(Heartbeat)],
				Do.ArmTimer(Heartbeat, 6_000))),
	};

	public ChaoslordKalabarAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The stone guard Chaoslord Kalabar makes at sixty. Retail pattern <c>ND2_PnD</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It exists to be spent.</b> Its only branch worth
/// anything hears its maker's <c>3008</c> and answers with a cast and <c>despawn_self</c> — so at
/// thirty-five the guard is gone, whatever health it had left.
/// <para>
/// <b>The cast is a skill index and the despawn is not</b>, which is the whole reason this class is
/// worth writing: a raid killing the guard to stop it, and a raid ignoring it, reach the same board at
/// thirty-five. Without the despawn the guard would simply accumulate.
/// </para>
/// <para>
/// <b>Not translated:</b> the cast that goes with the despawn, and the one it opens combat with.
/// </para>
/// </remarks>
[AIName("kalabar_stone_guard")]
public class StoneGuardAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(2, "he said to go off",
			[When.Message(ChaoslordKalabarAI.GoOff)],
			Do.DespawnSelf())),
	};

	public StoneGuardAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
