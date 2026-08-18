using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Chief maid Miladi, who summoned nothing at all until now.
/// </summary>
/// <remarks>
/// She ran <c>aggressive</c>, so retail's sixteen timer branches and six summons were a plain melee npc.
/// <b>Her mechanic is that the succubi land on players rather than on her</b> — the second and third most
/// hated get one each — so pinning her means checking <i>where</i> the adds appear, not how many.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ChiefMaidMiladiAiTests
{
	private const int AdmaStronghold = 320130000;
	private const int Miladi = 214693;
	private const int Succubus = 280963;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AdmaStronghold).WithWorldSize(2048)
			.WithAi(typeof(ChiefMaidMiladiAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Succubi(BossAiHarness harness) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == Succubus).ToList();

	/// <summary><b>Engaging places one succubus.</b></summary>
	[Fact]
	public void EngagingPlacesASuccubus()
	{
		using BossAiHarness harness = NewHarness();
		Npc miladi = harness.Spawn(Miladi, 497f, 575f, 189.49f);
		Player tank = harness.SpawnPlayer(499f, 577f, 189.49f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(miladi, tank);
		harness.Engage(miladi, tank);

		Assert.Single(Succubi(harness));
	}

	/// <summary>
	/// <b>And it lands on the player, not on her.</b> The whole point of
	/// <c>spawn_on_target_by_attacker_indicator</c>: a succubus at her feet would be a different fight.
	/// </summary>
	[Fact]
	public void TheSuccubusLandsOnThePlayer()
	{
		using BossAiHarness harness = NewHarness();
		Npc miladi = harness.Spawn(Miladi, 497f, 575f, 189.49f);
		Player tank = harness.SpawnPlayer(520f, 600f, 189.49f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(miladi, tank);
		harness.Engage(miladi, tank);

		Npc succubus = Assert.Single(Succubi(harness));

		// Twenty-three metres from her, next to the player she is fighting.
		Assert.True(PositionUtilDistance(succubus, tank) < PositionUtilDistance(succubus, miladi),
			"the succubus stood closer to Miladi than to the player she was summoned onto");
	}

	/// <summary><b>And it leaves at twelve seconds</b>, which is retail's <c>live_time</c>.</summary>
	[Fact]
	public void TheSuccubusLeavesAtTwelveSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc miladi = harness.Spawn(Miladi, 497f, 575f, 189.49f);
		Player tank = harness.SpawnPlayer(499f, 577f, 189.49f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(miladi, tank);
		harness.Engage(miladi, tank);

		var first = Succubi(harness).ToHashSet();
		Assert.NotEmpty(first);

		harness.Clock.Advance(TimeSpan.FromSeconds(11));
		Assert.All(first, s => Assert.Contains(s, harness.LiveNpcs()));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.DoesNotContain(harness.LiveNpcs(), n => first.Contains(n));
	}

	private static float PositionUtilDistance(Npc a, Creature b) =>
		(float)Math.Sqrt(((a.GetX() - b.GetX()) * (a.GetX() - b.GetX()))
			+ ((a.GetY() - b.GetY()) * (a.GetY() - b.GetY())));
}
