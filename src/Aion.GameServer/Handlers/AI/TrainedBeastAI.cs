using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The lizardmen's trained beasts — monitors, tipolids and the anuhart sergeant's mount. Retail pattern
/// <c>Lizardman_BeastB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>At a quarter health it calls its breeder, once, at
/// ten metres.</b> A trained animal that is losing shouts for the person who trained it.
/// <para>
/// <b>The two branches name two different people, and that is the point of them.</b> The melee branch
/// names <c>OBJI_ATTACKER</c> and the spell branch names <c>OBJI_CASTER</c> — for a beast being focused
/// by a melee player and a caster at once, those are two different players, and whichever landed the
/// blow that took it under a quarter is the one the breeder is sent after. A single "name my target"
/// would have picked whoever it happened to be holding instead.
/// </para>
/// <para>
/// <b>A guard that reads oddly and is kept.</b> The spell branch's <c>is_enemy</c> tests
/// <c>OBJI_CUR_TARGET</c>, not the caster it is about to name — so a beast whose current target is
/// friendly does not call, however hostile the caster. Retail's own wording; the melee branch has no
/// such guard at all, which is the fifth encounter in five entries to carry that asymmetry.
/// </para>
/// <para>
/// <b>Not translated:</b> the shout that goes with each call, and the <c>3201</c> branch — a reward
/// call from the <c>DrGuard_*_Reward</c> patterns, none of whose npcs our data places.
/// </para>
/// </remarks>
[AIName("trained_beast")]
public class TrainedBeastAI : PatternAi
{
	/// <summary>Retail's <c>3297</c>: this one is hurting me.</summary>
	public const int ThisOne = 3297;

	/// <summary>Retail's <c>range_as_meter</c> on both branches.</summary>
	private const float CallReach = 10f;

	/// <summary>Retail's <c>FLAGVARI_GAMMA_1</c>, shared across both provocations.</summary>
	private const int Called = 1;

	private const int Quarter = 25;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(5, "hit, at a quarter", [When.HpBelow(Quarter), When.FirstTime(Called)],
			Do.BroadcastAboutAttacker(ThisOne, CallReach))),

		OnSpelled = Of(Branch(4, "cast at, at a quarter",
			[When.HpBelow(Quarter), When.TargetIsEnemy, When.FirstTime(Called)],
			Do.BroadcastAboutCaster(ThisOne, CallReach))),
	};

	public TrainedBeastAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The bakarma breeders who answer their beasts (213398, 213399). Retail pattern
/// <c>Lizardman_BeastKA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>One hate point on whoever the beast named, and
/// go.</b> A glance rather than a claim, in the vasharti watch's sense — enough to bring the breeder
/// into the fight and not enough to take a player off whoever they were already fighting.
/// <para>
/// <b>Retail follows it with <c>switch_target OBJI_CUR_TARGET</c> carrying a hundred</b>, which on a
/// breeder that has just taken its only hate point means switching to the player it was named — the
/// same one. Translated as the single point it amounts to, rather than as two actions whose second
/// re-selects the first's result.
/// </para>
/// <para>
/// <b>Not translated:</b> the shout, and the <c>3298</c> branch beside this one — a second call with no
/// sender anywhere in the 5.8 files.
/// </para>
/// </remarks>
[AIName("bakarma_breeder")]
public class BakarmaBreederAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c>.</summary>
	private const int Glance = 1;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "the beast is calling",
			[When.Message(TrainedBeastAI.ThisOne)],
			Do.HateMessageTarget(Glance))),
	};

	public BakarmaBreederAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
