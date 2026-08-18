using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the Lepharist citadel, translated from retail patterns <c>Xlehpar_KeA</c> and
/// <c>Xlehpar_FeC</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class CitadelOverseerAiTests
{
	private const int Heiron = 210040000;

	private const int CitadelOverseer = 212886;
	private const int CitadelLaborer = 212882;

	private static BossAiHarness NewHarness(int map) =>
		BossAiHarness.For(map).WithWorldSize(2048)
			.WithAi(typeof(CitadelOverseerAI), typeof(CitadelLaborerAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>A citadel overseer pulled calls its labourers, and they commit a hundred.</b>
	/// </summary>
	[Fact]
	public void AnOverseerPulledCallsItsLabourers()
	{
		using BossAiHarness harness = NewHarness(Heiron);
		Npc overseer = harness.SpawnWithAi(CitadelOverseer, "citadel_overseer", 300f, 300f, 200f);
		Npc laborer = harness.SpawnWithAi(CitadelLaborer, "citadel_laborer", 306f, 300f, 200f);
		Npc distant = harness.SpawnWithAi(CitadelLaborer, "citadel_laborer", 330f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(overseer, laborer);
		BossAiHarness.MakeMutuallyKnown(overseer, distant);

		harness.Engage(overseer, raider);

		Assert.Equal(100, laborer.GetAggroList().GetHate(raider));
		Assert.Equal(0, distant.GetAggroList().GetHate(raider));
	}

	/// <summary><b>The numbers and the ranges are retail's, not ours.</b></summary>
	[Fact]
	public void TheNumbersAreRetails()
	{
		Assert.Equal(9003, CitadelOverseerAI.ToMe);
		Assert.Equal(9001, CitadelOverseerAI.Rallied);
		Assert.Equal(20f, CitadelOverseerAI.PulledReach);
	}
}
