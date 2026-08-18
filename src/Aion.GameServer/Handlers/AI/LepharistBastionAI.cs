using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Lepharist bastion, and the three numbers it talks on.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A defender that is pulled whispers, and a
/// defender that is losing shouts.</b> The opening call carries five metres and buys a single hate
/// point; the second carries fifteen and buys a hundred. Three times the reach and a hundred times the
/// payload, from the same npc, decided by which branch fires.
/// </remarks>
public static class LepharistCalls
{
	/// <summary>Retail's <c>1017</c>: the whisper, sent on being pulled.</summary>
	public const int Whisper = 1017;

	/// <summary>Retail's <c>1018</c>: the shout. Not built -- see <see cref="LepharistDefenderAI"/>.</summary>
	public const int Shout = 1018;

	/// <summary>Retail's <c>1016</c>: what either of them sends after running away.</summary>
	public const int Rallied = 1016;

	/// <summary>Retail's <c>range_as_meter</c> on the whisper.</summary>
	public const float WhisperReach = 5f;

	/// <summary>Retail's <c>range_as_meter</c> on the call sent after fleeing.</summary>
	public const float RallyReach = 10f;

	/// <summary>What a drudge gives the whisper.</summary>
	public const int Glance = 1;

	/// <summary>What it gives the shout.</summary>
	public const int Commit = 100;
}

/// <summary>
/// The lepharist defenders. Retail pattern <c>NLehpar_KnA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Five metres is the shortest call in this log</b>
/// — a defender being pulled tells whoever is standing on top of it and nobody else, and the drudge
/// that hears it gives a single point.
/// <para>
/// <b>Not built: the shout.</b> Retail's <c>1018</c> rides a battle-timer branch guarded by
/// <c>is_skill_count_left</c> — the branch fires only while a particular skill still has charges, and
/// this port has no notion of a skill's remaining uses. Building it without the guard would make a
/// defender shout in a health band where retail may have fallen silent, which is inventing behaviour
/// rather than translating it. The drudges' answer to it <em>is</em> built, so the day that guard
/// becomes expressible the other half is already waiting.
/// </para>
/// <para>
/// <b>Not translated:</b> four skills, two shouts, and the low-health flee that shares those
/// charge-gated branches.
/// </para>
/// </remarks>
[AIName("lepharist_defender")]
public class LepharistDefenderAI : PatternAi
{
	private const int Heartbeat = 0;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_2</c> on the pull.</summary>
	private const int Whispered = 2;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		// Retail guards this with set_flag_var: the whisper goes once per fight, not on every entry into
		// combat. A defender pulled, dropped and pulled again does not call twice.
		OnEnterAttack = Of(Branch(5, "pulled", [When.FirstTime(Whispered)],
			Do.ArmTimer(Heartbeat, 4_000),
			Do.Broadcast(LepharistCalls.Whisper, LepharistCalls.WhisperReach, aboutTarget: true))),

		// Retail's fallback: the two branches above it are charge-gated and are not built.
		OnBattleTimer = Of(Branch(1, "the clock", [When.Timer(Heartbeat)],
			Do.ArmTimer(Heartbeat, 4_000))),

		OnStopFleeing = Of(Branch(4, "done running", [],
			Do.Broadcast(LepharistCalls.Rallied, LepharistCalls.RallyReach, aboutTarget: true))),
	};

	public LepharistDefenderAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The bastion drudges. Retail pattern <c>NLehpar_LnA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It answers the whisper with one point and the
/// shout with a hundred</b>, and it runs away on its own account — but only from an attacker still
/// worth running from.
/// <para>
/// <b>The flee guard reads the player's health, not its own.</b> Below thirty percent itself, a drudge
/// flees only if its attacker is above forty; <b>a drudge that has nearly killed the player stays and
/// finishes the job.</b> That is the only guard in this log that judges the fight rather than the npc,
/// and it needed a new condition to say.
/// </para>
/// <para>
/// <b>And when it stops running it calls <c>1016</c></b> — the number two lepharist protectors listen
/// for, and the only thing in our data that answers it. A drudge that runs and turns is what fetches
/// them.
/// </para>
/// <para><b>Not translated:</b> the skill on the shout answer and three shouts.</para>
/// </remarks>
[AIName("bastion_drudge")]
public class BastionDrudgeAI : PatternAi
{
	/// <summary>Retail's <c>seconds</c> on the flee.</summary>
	private const int FiveSeconds = 5;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c>, shared across both provocations.</summary>
	private const int Fled = 1;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(2, "the shout", [When.Message(LepharistCalls.Shout)],
				Do.HateMessageTarget(LepharistCalls.Commit)),

			// 1017 is add_hate_point in retail and 1018 is switch_target, so the whisper is noted and
			// the shout is obeyed -- which is the difference between the two calls.
			Branch(2, "the whisper", [When.Message(LepharistCalls.Whisper)],
				Do.HateMessageParam(LepharistCalls.Glance))),

		OnAttacked = Of(Branch(1, "hit, and it is still worth running from",
			[When.HpBelow(30), When.TargetHpBetween(40, 100), When.FirstTime(Fled)],
			Do.Flee(FiveSeconds))),

		OnSpelled = Of(Branch(1, "cast at, and it is still worth running from",
			[When.HpBelow(30), When.TargetHpBetween(40, 100), When.CasterIsEnemy,
				When.FirstTime(Fled)],
			Do.Flee(FiveSeconds))),

		OnStopFleeing = Of(Branch(3, "done running", [],
			Do.Broadcast(LepharistCalls.Rallied, LepharistCalls.RallyReach, aboutTarget: true))),
	};

	public BastionDrudgeAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
