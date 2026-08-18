using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The kerubiel bandits. Retail pattern <c>ND2_AnE</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Below half health it names whoever is beating it,
/// and it does so on every single blow.</b> There is no flag var on either branch — which makes it the
/// first caller in this log that does not call once.
/// <para>
/// <b>That difference is the mechanic, not an oversight.</b> A tursin loudmouth calls once and a player
/// who survives the answer has survived it; a kerubiel bandit under half health keeps naming the same
/// player for as long as the fight lasts, so every fighter that wanders into earshot is pulled in as it
/// arrives. The camp does not answer a call — it answers continuously.
/// </para>
/// <para>
/// <b>The spell branch checks the caster is an enemy and the melee branch checks nothing</b> — the
/// ninth encounter in this log to carry that asymmetry, and the ninth to keep it.
/// </para>
/// <para>
/// <b>Not translated:</b> the two skills on each branch.
/// </para>
/// </remarks>
[AIName("kerubiel_bandit")]
public class KerubielBanditAI : PatternAi
{
	/// <summary>Retail's <c>2001</c>: the bandits' call.</summary>
	public const int GetHim = 2001;

	/// <summary>Retail's <c>range_as_meter</c>.</summary>
	public const float CallReach = 15f;

	private const int Half = 50;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		// No FirstTime: retail puts no flag on either branch. See the class remarks.
		OnAttacked = Of(Branch(2, "hit, below half", [When.HpBelow(Half)],
			Do.Broadcast(GetHim, CallReach, aboutTarget: true))),

		OnSpelled = Of(Branch(1, "cast at, below half", [When.Chance(50), When.HpBelow(Half), When.CasterIsEnemy],
			Do.Broadcast(GetHim, CallReach, aboutTarget: true))),
	};

	public KerubielBanditAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The kerubiel fighters, who answer them. Retail pattern <c>ND2_AnL</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A point and then a hundred, so a hundred and one
/// lands</b> — retail's usual order, and the same figure the dukaki miners and the pet drakes answer
/// with.
/// <para>
/// <b>Twenty of them against nine bandits</b>, which is the ratio that makes the repeated call matter:
/// there is always another fighter who has not yet been pulled.
/// </para>
/// <para><b>Not translated:</b> the skill it answers with.</para>
/// </remarks>
[AIName("kerubiel_fighter")]
public class KerubielFighterAI : PatternAi
{
	private const int Glance = 1;

	private const int Commit = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "a bandit is calling", [When.Message(KerubielBanditAI.GetHim)],
			Do.HateMessageParam(Glance),
			Do.HateMessageTarget(Commit))),
	};

	public KerubielFighterAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The kerubian hunters. Retail pattern <c>ND2_AnJ</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The same mechanic as the bandits at five metres
/// further</b> — below half health, on every blow, naming its target at twenty metres. Also unflagged.
/// <para>
/// <b>What differs is who answers.</b> A bandit's fighters take a hundred and one; a hunter's garks
/// take <b>two hundred</b>, because retail gives them an <c>add_hate_point</c> of a hundred where the
/// fighters get one. Two camps, the same call shape, and the pets hit twice as hard as the soldiers.
/// </para>
/// <para><b>Not translated:</b> the skill on each branch.</para>
/// </remarks>
[AIName("kerubian_hunter")]
public class KerubianHunterAI : PatternAi
{
	/// <summary>Retail's <c>2005</c>: the hunters' call, on their own number.</summary>
	public const int GetHim = 2005;

	/// <summary>Retail's <c>range_as_meter</c>, five metres further than the bandits'.</summary>
	public const float CallReach = 20f;

	private const int Half = 50;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(2, "hit, below half", [When.HpBelow(Half)],
			Do.Broadcast(GetHim, CallReach, aboutTarget: true))),

		OnSpelled = Of(Branch(1, "cast at, below half", [When.Chance(80), When.HpBelow(Half), When.CasterIsEnemy],
			Do.Broadcast(GetHim, CallReach, aboutTarget: true))),
	};

	public KerubianHunterAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The garks the hunters keep. Retail pattern <c>ND2_AnJ_BR</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A hundred and then a hundred, so two hundred
/// lands</b> — the largest answer to a field call anywhere in this log, and twice what the kerubiel
/// fighters give their own bandits.
/// <para>
/// <b>Twenty-five garks against twelve hunters.</b> A hunter under half health that keeps calling has
/// two garks apiece to send, and each arrives committed.
/// </para>
/// <para><b>Not translated:</b> the skill it answers with.</para>
/// </remarks>
[AIName("kerubian_gark")]
public class KerubianGarkAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c> — a hundred, where the kerubiel fighters take one.</summary>
	private const int Notice = 100;

	/// <summary>Retail's <c>points_to_add</c> on the switch that follows it.</summary>
	private const int Commit = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "a hunter is calling", [When.Message(KerubianHunterAI.GetHim)],
			Do.HateMessageParam(Notice),
			Do.HateMessageTarget(Commit))),
	};

	public KerubianGarkAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
