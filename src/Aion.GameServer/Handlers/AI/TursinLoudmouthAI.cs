using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The tursin bosses and loudmouths of Altgard. Retail patterns <c>Krall_KnA</c> and <c>Krall_KnC</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Below forty health, once, it names whoever is
/// beating it to every dukaki within fifteen metres</b> — and the miners and diggers who have been
/// standing around come and finish the job.
/// <para>
/// <b>This is the low-level version of every guard call in this log</b>, and it reads the same: the
/// creature with the pattern is not the threat, the creature that answers is. A player who pulls a
/// tursin big boss cleanly and a player who lets it get to forty percent are in two different fights.
/// </para>
/// <para>
/// <b>The spell branch checks the caster is an enemy and the melee branch checks nothing</b> — the
/// eighth encounter in this log to carry that asymmetry, and the eighth to keep it.
/// </para>
/// <para>
/// <b>Not translated:</b> the skill each branch opens with, and the shout.
/// </para>
/// </remarks>
[AIName("tursin_loudmouth")]
public class TursinLoudmouthAI : PatternAi
{
	/// <summary>Retail's <c>1002</c>: get him. Answered by the dukaki and the mamaki.</summary>
	public const int GetHim = 1002;

	/// <summary>Retail's <c>range_as_meter</c> on the loudmouths' call.</summary>
	public const float CallReach = 15f;

	/// <summary>Retail's <c>FLAGVARI</c>, shared across both provocations.</summary>
	private const int Called = 1;

	private const int Cornered = 40;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(2, "hit, and cornered",
			[When.HpBelow(Cornered), When.FirstTime(Called)],
			Do.Broadcast(GetHim, CallReach, aboutTarget: true))),

		OnSpelled = Of(Branch(1, "cast at, and cornered",
			[When.HpBelow(Cornered), When.CasterIsEnemy, When.FirstTime(Called)],
			Do.Broadcast(GetHim, CallReach, aboutTarget: true))),
	};

	public TursinLoudmouthAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The kaidan bigmouths. Retail pattern <c>NKrall_KeA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It calls on a clock rather than on its health</b>
/// — fifteen seconds into any fight, once, at twenty metres. Health never enters into it, so a kaidan
/// bigmouth killed inside fifteen seconds never calls at all and one that survives always does.
/// <para>
/// <b>The timer that carries the call is never re-armed</b>, which is what makes it once. What is
/// re-armed is a second timer, every twenty seconds, carrying a different number entirely.
/// </para>
/// <para>
/// <b>Not translated:</b> every skill; the shout; and the <c>1398</c> call on that second timer, which
/// nothing our data places listens to.
/// </para>
/// </remarks>
[AIName("kaidan_bigmouth")]
public class KaidanBigmouthAI : PatternAi
{
	/// <summary>Retail's <c>range_as_meter</c>, wider than the tursin's.</summary>
	public const float CallReach = 20f;

	private const int CallTimer = 0;
	private const int OtherTimer = 1;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(3, "engaging", [], Do.ArmTimer(CallTimer, 15_000))),

		OnBattleTimer = Of(
			Branch(2, "fifteen seconds in", [When.Timer(CallTimer)],
				Do.ArmTimer(OtherTimer, 8_000),
				Do.Broadcast(TursinLoudmouthAI.GetHim, CallReach, aboutTarget: true)),

			// Kept because it is real state: retail re-arms this one forever, and a pattern missing a
			// timer is a different pattern. Its own broadcast has no listener our data places.
			Branch(1, "and the other clock", [When.Timer(OtherTimer)],
				Do.ArmTimer(OtherTimer, 20_000))),
	};

	public KaidanBigmouthAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The mamaki workers, who both call and answer. Retail pattern <c>NBrownie_FnC</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It answers with a hundred, and it calls when it
/// stops running away.</b> Seventeen of them, which makes them the largest single group on this number
/// and the only one on it that does both jobs.
/// <para>
/// <b>A worker that has fled and stopped names whatever it is facing</b> — so a player chasing one
/// through a camp collects the camp. That is the same shape the klaw sentinels use and the bakarma
/// lookouts use; retail reaches for it whenever a weak npc runs.
/// </para>
/// <para>
/// <b>Not translated:</b> the shout and the skill on its answer, and retail's <c>percent_to_add=10</c>
/// beside the hundred.
/// </para>
/// </remarks>
[AIName("mamaki_worker")]
public class MamakiWorkerAI : PatternAi
{
	/// <summary>Retail's <c>range_as_meter</c> when it stops running.</summary>
	private const float FleeReach = 20f;

	/// <summary>Retail's <c>points_to_add</c> on its answer.</summary>
	private const int Claim = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnStopFleeing = Of(Branch(2, "done running, and it is that one", [],
			Do.Broadcast(TursinLoudmouthAI.GetHim, FleeReach, aboutTarget: true))),

		OnMessage = Of(Branch(1, "somebody named one", [When.Message(TursinLoudmouthAI.GetHim)],
			Do.HateMessageTarget(Claim))),
	};

	public MamakiWorkerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The dukaki miners and diggers, who only answer. Retail patterns <c>Brownie_FnQ</c> and
/// <c>Brownie_FnR</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A point and then a hundred, so a hundred and one
/// lands</b> — retail's usual order, and the same number the pet drakes and the tamed taygas answer
/// with.
/// <para>
/// <b>They check nothing at all:</b> no <c>is_enemy</c>, no state guard. A dukaki hears the call and
/// comes, whatever it was doing.
/// </para>
/// <para>
/// <b>Not translated:</b> the shout, and the <c>percent_to_add=0</c> retail writes beside the hundred —
/// a zero percent, which does nothing in any reading.
/// </para>
/// </remarks>
[AIName("dukaki_miner")]
public class DukakiMinerAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c>, taken before the switch.</summary>
	private const int Glance = 1;

	/// <summary>Retail's <c>points_to_add</c> on the switch that follows.</summary>
	private const int Commit = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "somebody named one", [When.Message(TursinLoudmouthAI.GetHim)],
			Do.HateMessageTarget(Glance),
			Do.HateMessageTarget(Commit))),
	};

	public DukakiMinerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
