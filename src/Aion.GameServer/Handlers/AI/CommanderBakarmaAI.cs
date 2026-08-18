using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Commander Bakarma (213780), Draupnir Cave. Retail pattern <c>IDDF3_DrakanFiBossD</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. His legionaries were arriving and were the same
/// legionary all fight. Retail promotes them **twice**, on his own health:
/// <list type="table">
/// <item><term>between 26% and 50%</term><description><c>5001</c> — every legionary in fifty metres
/// becomes a <b>vanguard</b> where it stands</description></item>
/// <item><term>below 25%</term><description><c>5002</c> — every vanguard starts a <b>six-second</b>
/// countdown and becomes a <b>relic guardian</b></description></item>
/// </list>
/// <para>
/// <b>The ladder is a promotion, not a wave.</b> Nothing new is summoned by either call: each add
/// replaces itself where it is standing, so the count does not grow and the fight does. A raid that
/// leaves adds alive through a band is fighting something else by the end of it.
/// </para>
/// <para>
/// <b>Both steps are HP-anchored in retail and are HP-anchored here.</b> The guards are
/// <c>is_hp_in_boundary 26..50</c> and <c>is_hp_lower_than 25</c>, with a once-only flag var each,
/// which is what <see cref="HpPhases"/> already is. Retail fires them from inside a battle-timer
/// branch that also casts four skills; <em>when</em> in the band is a timer we cannot reproduce, but
/// <em>which</em> band is data and that is what is built.
/// </para>
/// <para>
/// <b>Not built: message <c>6001</c>.</b> Retail's "everyone onto my target" call, and he sends it on
/// a repeating timer whose period changes with the band — thirty seconds above twenty-five percent,
/// forty below — from branches that are otherwise all skill indices, with gaps in the ladder at 80–100
/// and 50–56 that only the timer chain produces. A plain beat would fire in those gaps. Deliberately
/// absent from these classes rather than approximated, so that
/// <c>tools/client-extract/audit_message_senders.py</c> keeps listing it as work.
/// </para>
/// <para>
/// <b>Not built: <c>on_see_friend_killed_by_user</c></b>, which all three ladder patterns carry and
/// which is the raid's answer to the ladder — kill one in front of the others and the rest leave. Our
/// AI event set has no equivalent event at all; see docs/retail-ai-fidelity.md for what it would
/// unlock.
/// </para>
/// <para>
/// <b>Not translated:</b> every skill on his timer chain, his shouts, and the treasure box on his
/// death.
/// </para>
/// </remarks>
[AIName("commander_bakarma")]
public class CommanderBakarmaAI : SummonerAI, HpPhases.PhaseHandler
{
	/// <summary>Retail's <c>5001</c>: legionaries, become vanguards.</summary>
	public const int TakeTheNextRank = 5001;

	/// <summary>Retail's <c>5002</c>: vanguards, begin.</summary>
	public const int TakeTheLast = 5002;

	/// <summary>Retail's <c>range_as_meter</c> on both.</summary>
	private const float CallReach = 50f;

	private readonly HpPhases hpPhases = new HpPhases(50, 25);

	public CommanderBakarmaAI(Npc owner)
		: base(owner)
	{
	}

	protected override void HandleAttack(Creature creature)
	{
		base.HandleAttack(creature);
		hpPhases.TryEnterNextPhase(this);
	}

	public void HandleHpPhase(int phaseHpPercent)
	{
		int message = phaseHpPercent switch
		{
			50 => TakeTheNextRank,
			25 => TakeTheLast,
			_ => 0,
		};
		if (message != 0)
			NpcMessageBus.Broadcast(GetOwner(), message, GetTarget(), CallReach);
	}

	protected override void HandleDied()
	{
		base.HandleDied();
		hpPhases.Reset();
	}

	protected override void HandleBackHome()
	{
		base.HandleBackHome();
		hpPhases.Reset();
	}
}

/// <summary>
/// Bakarma's legionaries (280685), the first rung. Retail pattern <c>NDrakan_ChSlave1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. On <c>5001</c> it casts, puts a vanguard on the
/// spot it is standing on, and leaves. <b>Immediately</b> — there is no countdown on this rung, unlike
/// the next one.
/// </remarks>
[AIName("bakarma_legionary")]
public class BakarmaLegionaryAI : PatternAi
{
	/// <summary>Retail's <c>BDF3_NM_DrakanDF3Slave2_48_Ah</c>.</summary>
	private const int Vanguard = 280686;

	/// <summary>Retail's <c>SPAWN_ID_1</c> and <c>live_time</c>: twenty minutes.</summary>
	private const int Replacement = 1;
	private const int TwentyMinutes = 1200;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(5, "take the next rank",
			[When.Message(CommanderBakarmaAI.TakeTheNextRank)],
			Do.SpawnNear(Vanguard, Replacement, count: 1, liveSeconds: TwentyMinutes),
			Do.DespawnSelf())),
	};

	public BakarmaLegionaryAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Bakarma's vanguards (280686), the second rung. Retail pattern <c>NDrakan_ChSlave2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>This rung has a countdown and the first does
/// not.</b> On <c>5002</c> it casts and arms a six-second timer; when that runs out it puts a relic
/// guardian on its spot and leaves. So the last promotion is something a raid can see coming and
/// interrupt by killing the vanguard inside six seconds, and the first is not.
/// <para>
/// The asymmetry is retail's and is worth stating, because a class that made both instant would be
/// simpler and would throw away the only window in the ladder.
/// </para>
/// </remarks>
[AIName("bakarma_vanguard")]
public class BakarmaVanguardAI : PatternAi
{
	/// <summary>Retail's <c>BDF3_NM_DragonDF3Slave_48_Ah</c> — the relic guardian.</summary>
	private const int RelicGuardian = 280687;

	private const int Replacement = 1;
	private const int TwentyMinutes = 1200;

	/// <summary>Retail's <c>BTIMERI_INDEX_0</c> and its <c>delay</c>.</summary>
	private const int Countdown = 0;
	private const int CountdownMillis = 6000;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(5, "begin",
			[When.Message(CommanderBakarmaAI.TakeTheLast)],
			Do.ArmTimer(Countdown, CountdownMillis))),

		OnBattleTimer = Of(Branch(7, "and take the last rank", [When.Timer(Countdown)],
			Do.SpawnNear(RelicGuardian, Replacement, count: 1, liveSeconds: TwentyMinutes),
			Do.DespawnSelf())),
	};

	public BakarmaVanguardAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
