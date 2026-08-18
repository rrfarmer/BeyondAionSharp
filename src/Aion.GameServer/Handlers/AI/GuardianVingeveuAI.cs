using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Guardian Vingeveu of Heiron. Retail pattern <c>ND2_KeB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Three health bands, and each one opens with a
/// single announcement that never comes again.</b> Above seventy he calls his servants onto whoever he
/// is holding; below seventy and again below thirty-five he calls them and <b>scatters</b>, and so do
/// they.
/// <para>
/// <b>The shape is one heartbeat timer and a ladder of guards hanging off it.</b> Timer zero runs
/// throughout — six seconds when nothing else claims it — and each band's opener is a branch on that
/// same timer carrying its own flag var, so it fires the first time the band is entered and never
/// again. The opener's job is to re-arm timer zero faster (seven seconds, then five, then five) and to
/// start the band's own timer, which then keeps itself going.
/// </para>
/// <para>
/// <b>What separates the bands is the announcement, not the pace.</b> The first band opens on
/// <c>6193</c> — come and help — and does not move him. The second and third open on <c>6194</c>, which
/// carries a scatter for him and for every servant that hears it. So the fight changes character
/// twice, at seventy and at thirty-five, and both times the whole room re-picks its targets at once.
/// </para>
/// <para>
/// <b>Fifty metres</b> is retail's range on every one of his calls — wide enough that servants pulled
/// away from him still answer.
/// </para>
/// <para>
/// <b>Not translated:</b> eight skills, which is every <c>use_skill</c> on every branch — the ones the
/// timers exist to pace. What is left is the pacing itself, which is the part a raid reads.
/// <c>OBJI_EVENT_TARGET</c> on the engage branch is translated as the current target, which on entering
/// combat is the same creature. <b>Health of exactly thirty-five belongs to no band</b>; see
/// <see cref="Third"/>.
/// </para>
/// </remarks>
[AIName("guardian_vingeveu")]
public class GuardianVingeveuAI : PatternAi
{
	/// <summary>Retail's <c>6193</c>: help me with this one.</summary>
	public const int HelpMe = 6193;

	/// <summary>Retail's <c>6194</c>: everyone, again — the band-change call, which scatters.</summary>
	public const int Again = 6194;

	/// <summary>Retail's <c>range_as_meter</c> on every call he makes.</summary>
	public const float CallReach = 50f;

	/// <summary>
	/// Retail's <c>is_hp_lower_than percent=35</c>, against the second band's
	/// <c>is_hp_in_boundary larger_than=36</c>.
	/// </summary>
	/// <remarks>
	/// <b>Exactly thirty-five belongs to no band</b>, which is retail's own hole and is kept: at that
	/// one value neither guard passes and only the heartbeat runs. It costs a raid nothing — health
	/// does not linger on an integer — and inventing a boundary to close it would be inventing a
	/// number.
	/// </remarks>
	private const int Third = 35;

	// The heartbeat, and the three band timers hanging off it.
	private const int Heartbeat = 0;
	private const int FirstBandSkill = 2;
	private const int SecondBandOpener = 4;
	private const int SecondBandSkill = 5;
	private const int ThirdBandSkill = 7;

	// One flag per band, so each opens exactly once however often the band is re-entered.
	private const int OpenedFirst = 1;
	private const int OpenedSecond = 2;
	private const int OpenedThird = 3;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(13, "engaging", [],
			Do.ArmTimer(Heartbeat, 15_000),
			Do.Broadcast(Again, CallReach, aboutTarget: true),
			Do.SwitchTarget(AggroTarget.RANDOM))),

		// Written in retail's priority order, highest first: the band guards are checked before the
		// bare heartbeat at the bottom, which is what makes that last branch the fallback.
		OnBattleTimer = Of(
			Branch(11, "third band, keeping it up",
				[When.Timer(ThirdBandSkill), When.HpBelow(Third)],
				Do.ArmTimer(ThirdBandSkill, 20_000),
				Do.Broadcast(HelpMe, CallReach, aboutTarget: true)),

			Branch(10, "third band, opening it",
				[When.Timer(Heartbeat), When.HpBelow(Third), When.FirstTime(OpenedThird)],
				Do.ArmTimer(Heartbeat, 5_000),
				Do.ArmTimer(ThirdBandSkill, 15_000),
				Do.Broadcast(Again, CallReach, aboutTarget: true),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(8, "second band, keeping it up",
				[When.Timer(SecondBandSkill), When.HpBetween(36, 70)],
				Do.ArmTimer(SecondBandSkill, 15_000)),

			Branch(7, "second band, handing over",
				[When.Timer(SecondBandOpener), When.HpBetween(36, 70)],
				Do.ArmTimer(SecondBandSkill, 20_000),
				Do.Broadcast(HelpMe, CallReach, aboutTarget: true)),

			Branch(6, "second band, opening it",
				[When.Timer(Heartbeat), When.HpBetween(36, 70), When.FirstTime(OpenedSecond)],
				Do.ArmTimer(Heartbeat, 5_000),
				Do.ArmTimer(SecondBandOpener, 15_000),
				Do.Broadcast(Again, CallReach, aboutTarget: true),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(4, "first band, keeping it up",
				[When.Timer(FirstBandSkill), When.HpBetween(71, 100)],
				Do.ArmTimer(FirstBandSkill, 20_000)),

			// The only opener that does not scatter -- see the class remarks.
			Branch(3, "first band, opening it",
				[When.Timer(Heartbeat), When.HpBetween(71, 100), When.FirstTime(OpenedFirst)],
				Do.ArmTimer(Heartbeat, 7_000),
				Do.ArmTimer(FirstBandSkill, 25_000),
				Do.Broadcast(HelpMe, CallReach, aboutTarget: true)),

			Branch(1, "the heartbeat",
				[When.Timer(Heartbeat)],
				Do.ArmTimer(Heartbeat, 6_000))),
	};

	public GuardianVingeveuAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Vinsev's servants, who answer Guardian Vingeveu. Retail pattern <c>ND2_Ksum1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It has nothing but two answers</b>, and which one
/// it gives decides whether the raid can hold it.
/// <para>
/// <b>On <c>6193</c> — help me — it takes a single point on the player he named</b> and keeps
/// whatever it was doing. A glance, in the vasharti watch's sense: enough to bring it in, not enough to
/// take it off a tank.
/// </para>
/// <para>
/// <b>On <c>6194</c> — the band change — it takes ten, buffs itself, and then throws that away by
/// switching to a random one of its own attackers.</b> Ten points and then a scatter is a strange pair
/// to read on the page and an obvious one in the room: the ten is the boss telling it who matters and
/// the scatter is it losing its head anyway. Every servant in fifty metres does this in the same
/// instant he does.
/// </para>
/// <para>
/// <b>Not translated:</b> the skill on each answer.
/// </para>
/// </remarks>
[AIName("vingeveu_servant")]
public class VingeveuServantAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c> on the help call.</summary>
	private const int Glance = 1;

	/// <summary>Retail's <c>point_to_add</c> on the band-change call.</summary>
	private const int Notice = 10;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(2, "he wants help with that one",
				[When.Message(GuardianVingeveuAI.HelpMe)],
				Do.HateMessageParam(Glance)),

			Branch(1, "he has changed, and so have I",
				[When.Message(GuardianVingeveuAI.Again)],
				Do.HateMessageParam(Notice),
				Do.SwitchTarget(AggroTarget.RANDOM))),
	};

	public VingeveuServantAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
