using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The numbers Panesterra's base guards talk on, and the payloads that ride with them.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Each base runs the same two-tier conversation on
/// its own pair of numbers</b> — an ordinary guard's call, which every guard in the base answers with
/// ten hate, and its captain's call, which they answer with a hundred. Nothing else in the family
/// differs: the payloads, the actions and the absent state guards are identical across all ten
/// patterns.
/// <para>
/// <b>What does differ, pattern by pattern, is how far a call carries</b> — thirteen metres for most of
/// them and twenty-five for the lookouts and patrols whose job is to see. That is the only number worth
/// reading twice in the whole family.
/// </para>
/// </remarks>
public static class PanesterraCalls
{
	/// <summary>Retail's <c>41000</c>: an ordinary guard of the Vritra-side base.</summary>
	public const int VritraGuard = 41000;

	/// <summary>Retail's <c>41001</c>: that base's captain.</summary>
	public const int VritraCaptain = 41001;

	/// <summary>Retail's <c>41100</c>: an ordinary guard of the other base.</summary>
	public const int LightGuard = 41100;

	/// <summary>Retail's <c>41101</c>: its captain.</summary>
	public const int LightCaptain = 41101;

	/// <summary>Retail's <c>range_as_meter</c> on most of the family.</summary>
	public const float Near = 13f;

	/// <summary>The lookouts' and patrols' range — they are posted to see further.</summary>
	public const float Far = 25f;

	/// <summary>Retail's <c>point_to_add</c> when a guard answers a guard.</summary>
	public const int GuardAnswer = 10;

	/// <summary>Retail's <c>points_to_add</c> when anyone answers a captain.</summary>
	public const int CaptainAnswer = 100;

	/// <summary>
	/// The two answers every guard in a base gives, in retail's priority order: the captain first.
	/// </summary>
	/// <remarks>
	/// <b>Neither branch has a state guard and only one pattern in ten has an <c>is_enemy</c></b> — see
	/// <see cref="PanesterraSlayerAI"/>. So a guard answers whatever it is doing, which is what makes a
	/// Panesterra base pull as one piece.
	/// </remarks>
	public static PatternBranch[] Answers(int guardCall, int captainCall) => Of(
		Branch(99, "the captain is calling", [When.Message(captainCall)],
			Do.HateMessageTarget(CaptainAnswer)),

		Branch(1, "a guard is calling", [When.Message(guardCall)],
			Do.HateMessageTarget(GuardAnswer)));
}

/// <summary>
/// The Vritra-side cutthroats, who call at thirteen metres. Retail pattern <c>Gab1_Gaurd_An</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Calls when pulled and answers when called</b> —
/// the ordinary body of the base.
/// <para><b>Not translated:</b> the skills on the captain's answer, and the battle timers.</para>
/// </remarks>
[AIName("panesterra_cutthroat")]
public class PanesterraCutthroatAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.Broadcast(PanesterraCalls.VritraGuard, PanesterraCalls.Near, aboutTarget: true))),

		OnMessage = PanesterraCalls.Answers(PanesterraCalls.VritraGuard, PanesterraCalls.VritraCaptain),
	};

	public PanesterraCutthroatAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The Vritra-side lookouts, who call at twenty-five. Retail pattern <c>Gab1_Gaurd_Watch_PR_Rn</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The same guard with twice the reach</b>, which is
/// what a lookout is for: pull one and the call crosses most of a base rather than one post of it.
/// </remarks>
[AIName("panesterra_lookout")]
public class PanesterraLookoutAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.Broadcast(PanesterraCalls.VritraGuard, PanesterraCalls.Far, aboutTarget: true))),

		OnMessage = PanesterraCalls.Answers(PanesterraCalls.VritraGuard, PanesterraCalls.VritraCaptain),
	};

	public PanesterraLookoutAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The Vritra-side grunts, hidestitchers and defenders, who only answer. Retail patterns
/// <c>Gab1_Gaurd_Charge_PM_Fn</c>, <c>Gab1_Gaurd_Support_HBuff_Pn</c> and
/// <c>Gab1_Gaurd_Defend_PM_Kn</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Three retail patterns, one class</b>: they differ
/// only in which skills they cast on the captain's call, and every one of those is a skill index. The
/// answers themselves are identical.
/// </remarks>
[AIName("panesterra_soldier")]
public class PanesterraSoldierAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = PanesterraCalls.Answers(PanesterraCalls.VritraGuard, PanesterraCalls.VritraCaptain),
	};

	public PanesterraSoldierAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The Vritra-side dreadcaptains. Retail pattern <c>Gab1_VritraGuard_Boss_01</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It calls and it is never called</b> — its number
/// is the one every guard in the base answers with a hundred rather than ten, and it has no answer
/// branch of its own. Pulling the captain pulls the base; pulling the base does not pull the captain.
/// </remarks>
[AIName("panesterra_dreadcaptain")]
public class PanesterraDreadcaptainAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.Broadcast(PanesterraCalls.VritraCaptain, PanesterraCalls.Near, aboutTarget: true))),
	};

	public PanesterraDreadcaptainAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The other base's patrols. Retail pattern <c>Gab1_Gaurd_Ra_An_Broad</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The one pattern in the family where
/// <c>is_user_flying</c> changes something real:</b> a patrol pulled by a player in the air calls at
/// thirteen metres, and one pulled from the ground calls at twenty-five.
/// <para>
/// <b>This port cannot evaluate that condition, so it takes the ground branch</b> — retail's own
/// fallback, the lower-priority of the two, and the overwhelmingly common case. <b>A flying puller
/// should get the shorter call and does not.</b> Recorded rather than approximated with an average.
/// </para>
/// </remarks>
[AIName("panesterra_patrol")]
public class PanesterraPatrolAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled from the ground", [],
			Do.Broadcast(PanesterraCalls.LightGuard, PanesterraCalls.Far, aboutTarget: true))),

		OnMessage = PanesterraCalls.Answers(PanesterraCalls.LightGuard, PanesterraCalls.LightCaptain),
	};

	public PanesterraPatrolAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The other base's slayers. Retail pattern <c>Gab1_LGuard_05</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The only pattern of the ten that checks whether
/// the player named is its enemy before answering a guard's call.</b> Nine others do not, and the
/// asymmetry is retail's — kept unchanged, as the same asymmetry has been kept in five earlier
/// encounters.
/// <para>
/// Its captain answer carries <c>percent_to_add=10</c> where the rest of the family carries eleven,
/// which is the sort of difference that only exists because a person typed it. Neither is translated:
/// this port has no percentage-of-existing-hate.
/// </para>
/// </remarks>
[AIName("panesterra_slayer")]
public class PanesterraSlayerAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.Broadcast(PanesterraCalls.LightGuard, PanesterraCalls.Near, aboutTarget: true))),

		OnMessage = Of(
			Branch(99, "the captain is calling", [When.Message(PanesterraCalls.LightCaptain)],
				Do.HateMessageTarget(PanesterraCalls.CaptainAnswer)),

			// The is_enemy that only this pattern carries.
			Branch(1, "a guard is calling, and it is my enemy",
				[When.Message(PanesterraCalls.LightGuard), When.MessageParamIsEnemy],
				Do.HateMessageTarget(PanesterraCalls.GuardAnswer))),
	};

	public PanesterraSlayerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The other base's infantry, healers and knights, who only answer. Retail patterns
/// <c>Gab1_Gaurd_Fi_An</c>, <c>Gab1_Gaurd_Pr_An</c> and <c>Gab1_Gaurd_Kn_An</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The mirror of
/// <see cref="PanesterraSoldierAI"/> on the other base's numbers.
/// </remarks>
[AIName("panesterra_infantry")]
public class PanesterraInfantryAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = PanesterraCalls.Answers(PanesterraCalls.LightGuard, PanesterraCalls.LightCaptain),
	};

	public PanesterraInfantryAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The other base's warcaptains. Retail pattern <c>Gab1_LGuard_Boss_01</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The mirror of
/// <see cref="PanesterraDreadcaptainAI"/>.
/// <para>
/// <b>Not built: its death.</b> <c>on_killed_by_user</c> fans out six different broadcasts behind
/// <c>is_tribe</c> guards on the killer — <c>10101</c>, <c>20101</c>, <c>30101</c>, <c>40101</c>,
/// <c>10103</c> and <c>4440444</c>, one per Panesterra faction. That is the base-capture announcement
/// rather than an AI mechanic, and it belongs with the siege code rather than here; it is listed in
/// docs/retail-ai-fidelity.md so nobody translates it into an aggro action by mistake.
/// </para>
/// </remarks>
[AIName("panesterra_warcaptain")]
public class PanesterraWarcaptainAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.Broadcast(PanesterraCalls.LightCaptain, PanesterraCalls.Near, aboutTarget: true))),
	};

	public PanesterraWarcaptainAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The cutthroats of the three rival bases, who answer a warcaptain's call and nothing else. Retail
/// patterns <c>Gab1_VritraGuard_BossKiller_01_02</c>, <c>_03</c> and <c>_04</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Aspamon, Atasin and Disilgot each keep four of
/// these, and what they listen for is the *other* base's captain.</b> A warcaptain pulled in Belani is
/// heard by twelve npcs who do not belong to Belani — which is the whole of Panesterra's design, four
/// factions in one map.
/// <para>
/// <b>Their answer is a bare switch with a hundred</b>, and no hate-adding action beside it, which is
/// how it differs from every other answer in the family.
/// </para>
/// <para>
/// <b>Not built:</b> their two <c>4440444</c> branches, whose actions the pattern leaves empty.
/// </para>
/// </remarks>
[AIName("panesterra_bosskiller")]
public class PanesterraBossKillerAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(2, "a warcaptain is calling", [When.Message(PanesterraCalls.LightCaptain)],
			Do.HateMessageTarget(PanesterraCalls.CaptainAnswer))),
	};

	public PanesterraBossKillerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
