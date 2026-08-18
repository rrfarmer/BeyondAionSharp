using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Tests.Ai;

/// <summary>A test-only pattern whose only guard is a flag.</summary>
[AIName("flag_probe_bare")]
public class FlagProbeBareAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(1, "once, ever", [When.FirstTime(1)], Do.HateAttacker(7))),
	};

	public FlagProbeBareAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>The same, with a health guard in front of the flag — the shape every shipped encounter uses.</summary>
[AIName("flag_probe_guarded")]
public class FlagProbeGuardedAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(1, "once, below half",
			[When.HpBelow(50), When.FirstTime(1)], Do.HateTarget(7))),
	};

	public FlagProbeGuardedAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Pins for <see cref="AiPattern.When.FirstTime"/> — retail's <c>set_flag_var</c>, the guard a dozen
/// shipped encounters use to make a branch fire once.
/// </summary>
/// <remarks>
/// <b>These exist because the Esoterrace alarm's five once-only bands fired on every blow.</b> Every
/// other once-only branch in this log carries a health or timer guard in front of its flag and all of
/// them pass, which left two possibilities: the fault is peculiar to that pattern, or the flag has never
/// worked and something else has been doing the stopping. <b>Pinning the primitive is the only way to
/// tell</b>, and it is the rule the shulack relay earned.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class FirstTimeFlagTests
{
	private const int Altgard = 220030000;
	private const int AnyNpc = 210391;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Altgard).WithWorldSize(2048)
			.WithAi(typeof(FlagProbeBareAI), typeof(FlagProbeGuardedAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary><b>A branch guarded only by a flag fires once.</b></summary>
	[Fact]
	public void ABranchGuardedOnlyByAFlagFiresOnce()
	{
		using BossAiHarness harness = NewHarness();
		Npc npc = harness.SpawnWithAi(AnyNpc, "flag_probe_bare", 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		// Engage raises a genuine attack event, so the branch has already fired by the time this reads
		// -- which is itself the first half of the claim.
		harness.Engage(npc, raider);
		int afterFirst = npc.GetAggroList().GetHate(raider);

		npc.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		npc.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		npc.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(afterFirst, npc.GetAggroList().GetHate(raider));
	}

	/// <summary>
	/// <b>And so does one with a health guard in front of it</b> — the shape every shipped encounter
	/// uses, pinned here so the two cannot drift apart unnoticed.
	/// </summary>
	[Fact]
	public void AndSoDoesOneWithAHealthGuardInFront()
	{
		using BossAiHarness harness = NewHarness();
		Npc npc = harness.SpawnWithAi(AnyNpc, "flag_probe_guarded", 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		harness.Engage(npc, raider);
		BossAiHarness.SetExactPercent(npc, 40);

		int baseline = npc.GetAggroList().GetHate(raider);
		npc.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		int afterFirst = npc.GetAggroList().GetHate(raider);
		Assert.Equal(baseline + 7, afterFirst);

		npc.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		npc.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Equal(afterFirst, npc.GetAggroList().GetHate(raider));
	}
}
