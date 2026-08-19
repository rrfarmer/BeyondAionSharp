using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Drakenspire seal guardian chiefs, which had eight spawns and no pins at all.
/// </summary>
/// <remarks>
/// Ten npcs run this class and nothing asserted any of it. Two of its markers were missing outright:
/// retail's chiefs drop a <b>delay keeper</b> on waking and leave a <b>reset marker</b> where they fall,
/// and both npcs were already in our data with an AI of their own, summoned by nobody.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SealGuardianAiTests
{
	private const int Drakenspire = 301500000;

	/// <summary>The four post chiefs, one per corner of the seal.</summary>
	private const int ChiefOne = 855460;
	private const int ChiefTwo = 855461;
	private const int ChiefThree = 855462;
	private const int ChiefFour = 855463;

	/// <summary>A dragon-phase chief, which places all four specters rather than one.</summary>
	private const int DragonPhaseChief = 855464;

	private const int SpecterOne = 855452;
	private const int SpecterTwo = 855454;
	private const int SpecterThree = 855456;
	private const int SpecterFour = 855458;

	private const int DelayKeeper = 855540;
	private const int ResetMarker = 855538;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Drakenspire).WithWorldSize(2048)
			// The specters carry drakenspire_ghastly_protector and the harness validates every AI name
			// it is asked to place, so omitting it makes each spawn throw. Third pin this session to
			// fail first for a missing WithAi entry.
			.WithAi(typeof(SealGuardianAI), typeof(GhastlyProtectorAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary><b>Each post chief places its own specter</b>, and only its own.</summary>
	[Theory]
	[InlineData(ChiefOne, SpecterOne)]
	[InlineData(ChiefTwo, SpecterTwo)]
	[InlineData(ChiefThree, SpecterThree)]
	[InlineData(ChiefFour, SpecterFour)]
	public void EachChiefPlacesItsOwnSpecter(int chief, int specter)
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(chief, 150f, 510f, 1749.59f);

		// The placement is a second behind the spawn.
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(1, Count(harness, specter));
		foreach (int other in new[] { SpecterOne, SpecterTwo, SpecterThree, SpecterFour })
			if (other != specter)
				Assert.Equal(0, Count(harness, other));
	}

	/// <summary><b>A dragon-phase chief places all four.</b></summary>
	[Fact]
	public void ADragonPhaseChiefPlacesEverySpecter()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(DragonPhaseChief, 150f, 510f, 1749.59f);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		foreach (int specter in new[] { SpecterOne, SpecterTwo, SpecterThree, SpecterFour })
			Assert.Equal(1, Count(harness, specter));
	}

	/// <summary>
	/// <b>And every chief drops a delay keeper at its own feet</b>, standing eighty seconds.
	/// </summary>
	/// <remarks>
	/// Retail's <c>on_wake_up</c>, which this class did not have. Eighty seconds is a fifth longer than
	/// the minute an untouched chief waits before teleporting out, so the keeper outlives it.
	/// </remarks>
	[Fact]
	public void EveryChiefDropsADelayKeeperForEightySeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc chief = harness.Spawn(ChiefOne, 150f, 510f, 1749.59f);

		Npc keeper = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == DelayKeeper);
		Assert.Equal(chief.GetX(), keeper.GetX(), 1);

		harness.Clock.Advance(TimeSpan.FromSeconds(79));
		Assert.Equal(1, Count(harness, DelayKeeper));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, DelayKeeper));
	}

	/// <summary>
	/// <b>And leaves one reset marker where it falls</b> — one, not two.
	/// </summary>
	/// <remarks>
	/// Retail writes the death branch twice, for <c>on_killed_by_user</c> and <c>on_killed_by_npc</c>,
	/// byte-identical and both carrying the same test-and-set flag var — so the first match sets it and
	/// the second can never run. Fourth time this idiom has been read wrong somewhere in this port, so
	/// the count is asserted rather than assumed.
	/// </remarks>
	[Fact]
	public void DyingLeavesOneResetMarker()
	{
		using BossAiHarness harness = NewHarness();
		Npc chief = harness.Spawn(ChiefOne, 150f, 510f, 1749.59f);
		Assert.Equal(0, Count(harness, ResetMarker));

		chief.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Npc marker = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == ResetMarker);
		Assert.Equal(chief.GetX(), marker.GetX(), 1);

		harness.Clock.Advance(TimeSpan.FromSeconds(11));
		Assert.Equal(0, Count(harness, ResetMarker));
	}
}
