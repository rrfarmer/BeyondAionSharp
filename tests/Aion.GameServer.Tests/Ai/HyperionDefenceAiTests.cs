using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="HyperionDefenceAI"/> and the dismissal <see cref="HyperionAI"/> now broadcasts,
/// translated from retail message <c>21101</c> across the <c>IDRuneWP_Main_*</c> family (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// One branch in twelve patterns and twenty-two npcs: when Hyperion goes, they go. Found by
/// <c>audit_message_senders.py</c> rather than by reading — the first gap of its shape the audit caught
/// before a human did.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class HyperionDefenceAiTests
{
	private const int InfinityShard = 300800000;

	private const int Hyperion = 231073;
	private const int Combatant = 231096;
	private const int Medic = 231098;
	private const int Turret = 231102;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(InfinityShard).WithWorldSize(2048)
			.WithAi(typeof(HyperionAI), typeof(HyperionDefenceAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Npc>) Defended()
	{
		BossAiHarness harness = NewHarness();
		Npc hyperion = harness.Spawn(Hyperion, 300f, 300f, 200f);
		var defence = new List<Npc>
		{
			harness.Spawn(Combatant, 310f, 300f, 200f),
			harness.Spawn(Medic, 315f, 300f, 200f),
			harness.Spawn(Turret, 320f, 300f, 200f),
		};

		foreach (Npc guard in defence)
			BossAiHarness.MakeMutuallyKnown(hyperion, guard);

		return (harness, hyperion, defence);
	}

	private static int Standing(BossAiHarness harness, List<Npc> defence) =>
		harness.LiveNpcs().Count(n => defence.Any(g => ReferenceEquals(g, n)));

	/// <summary>While he stands, so do they.</summary>
	[Fact]
	public void WhileHeStandsSoDoThey()
	{
		var (harness, _, defence) = Defended();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		Assert.Equal(3, Standing(harness, defence));
	}

	/// <summary><b>When Hyperion dies, his whole defence removes itself.</b></summary>
	[Fact]
	public void WhenHyperionDiesTheDefenceGoes()
	{
		var (harness, hyperion, defence) = Defended();
		using BossAiHarness _h = harness;

		hyperion.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Standing(harness, defence));
	}

	/// <summary>And when he leaves the fight, which retail treats the same way.</summary>
	[Fact]
	public void AndWhenHeLeavesTheFight()
	{
		var (harness, hyperion, defence) = Defended();
		using BossAiHarness _h = harness;

		hyperion.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Standing(harness, defence));
	}

	/// <summary>
	/// <b>And only within fifty metres.</b> Retail's <c>range_as_meter</c> is what keeps the dismissal
	/// to the force around him.
	/// </summary>
	[Fact]
	public void AndOnlyWithinFiftyMetres()
	{
		var (harness, hyperion, defence) = Defended();
		using BossAiHarness _h = harness;

		Npc distant = harness.Spawn(Combatant, 380f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(hyperion, distant);

		hyperion.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Standing(harness, defence));
		Assert.Contains(harness.LiveNpcs(), n => ReferenceEquals(n, distant));
	}
}
