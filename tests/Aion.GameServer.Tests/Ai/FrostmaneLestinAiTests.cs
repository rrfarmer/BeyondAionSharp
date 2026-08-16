using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="FrostmaneLestinAI"/>, translated from retail pattern <c>ND2_ElementalSu2</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// What this replaces: a generic <c>summoner</c> table that called the same NPC at all three rungs,
/// at the wrong thresholds, and let all twelve accumulate. Each of those three is a pin here.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class FrostmaneLestinAiTests
{
	/// <summary>Beluslan, where he stands.</summary>
	private const int Beluslan = 220030000;
	private const int Lestin = 212875;

	private const int FirstWave = 280489;
	private const int SecondWave = 280490;
	private const int ThirdWave = 280491;

	/// <summary>What the old summon table called at every rung — a fourth NPC of the same name.</summary>
	private const int TheOldAdd = 280481;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(FrostmaneLestinAI), typeof(AggressiveNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int raidSize = 3)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Lestin, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(305f + i, 300f, 200f));
		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);
		return (harness, boss, raid);
	}

	private static void Advance(BossAiHarness harness, Npc boss, List<Player> raid, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Untouched he summons nothing — the ladder hangs off the fight.</summary>
	[Fact]
	public void AnUnpulledLestinSummonsNothing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Lestin, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(120));

		Assert.Equal(0, Count(harness, FirstWave));
	}

	/// <summary>
	/// The first wave lands on dropping below ninety, not eighty — retail's band is 66..90.
	/// </summary>
	[Fact]
	public void TheFirstWaveComesBelowNinety()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 95);
		Advance(harness, boss, raid, 12);
		Assert.Equal(0, Count(harness, FirstWave));

		BossAiHarness.SetExactPercent(boss, 89);
		Advance(harness, boss, raid, 11);
		Assert.Equal(4, Count(harness, FirstWave));
	}

	/// <summary>Three different elementals, one per rung — not the same add three times.</summary>
	[Fact]
	public void EachRungCallsADifferentElemental()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 89);
		Advance(harness, boss, raid, 11);
		Assert.Equal(4, Count(harness, FirstWave));

		BossAiHarness.SetExactPercent(boss, 64);
		Advance(harness, boss, raid, 10);
		Assert.Equal(4, Count(harness, SecondWave));

		BossAiHarness.SetExactPercent(boss, 39);
		Advance(harness, boss, raid, 10);
		Assert.Equal(4, Count(harness, ThirdWave));

		// And never the one the old summon table called.
		Assert.Equal(0, Count(harness, TheOldAdd));
	}

	/// <summary>
	/// Each wave <b>replaces</b> the one before it, so four are standing at a time rather than twelve.
	/// </summary>
	[Fact]
	public void EachWaveClearsTheOneBeforeIt()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 89);
		Advance(harness, boss, raid, 11);

		BossAiHarness.SetExactPercent(boss, 64);
		Advance(harness, boss, raid, 10);
		Assert.Equal(0, Count(harness, FirstWave));
		Assert.Equal(4, Count(harness, SecondWave));

		BossAiHarness.SetExactPercent(boss, 39);
		Advance(harness, boss, raid, 10);
		Assert.Equal(0, Count(harness, SecondWave));
		Assert.Equal(4, Count(harness, ThirdWave));

		int standing = Count(harness, FirstWave) + Count(harness, SecondWave) + Count(harness, ThirdWave);
		Assert.Equal(4, standing);
	}

	/// <summary>Each rung is a one-shot, so sitting in a band does not keep calling.</summary>
	[Fact]
	public void ARungFiresOnlyOnce()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 89);
		Advance(harness, boss, raid, 40);

		Assert.Equal(4, Count(harness, FirstWave));
	}

	/// <summary>
	/// And the same is true of the rungs that replace a wave, which a head-count cannot see: a
	/// repeating second rung would clear the first group and lay a fresh four every nine seconds, and
	/// four would be standing throughout either way.
	/// </summary>
	/// <remarks>
	/// So this watches <b>which</b> four. Object ids, the same answer the guards' wave-lifetime pin
	/// arrived at for the same reason.
	/// </remarks>
	[Fact]
	public void AReplacingRungAlsoFiresOnlyOnce()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 89);
		Advance(harness, boss, raid, 11);

		BossAiHarness.SetExactPercent(boss, 64);
		Advance(harness, boss, raid, 10);
		int[] wave = harness.LiveNpcs().Where(n => n.GetNpcId() == SecondWave)
			.Select(n => n.GetObjectId()).ToArray();
		Assert.Equal(4, wave.Length);

		Advance(harness, boss, raid, 30);

		Assert.All(wave, id => Assert.Contains(harness.LiveNpcs(), n => n.GetObjectId() == id));
		Assert.Equal(4, Count(harness, SecondWave));
	}

	/// <summary>
	/// Burned down past every rung at once he calls the <b>third</b> wave: the deepest band outranks
	/// the rest, so he does not walk the ladder on the way down.
	/// </summary>
	[Fact]
	public void BurnedDownFastHeCallsTheThirdWave()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 39);
		Advance(harness, boss, raid, 11);

		Assert.Equal(4, Count(harness, ThirdWave));
		Assert.Equal(0, Count(harness, FirstWave));
		Assert.Equal(0, Count(harness, SecondWave));
	}

	/// <summary>
	/// From the second rung on he rounds on whoever is closest to dying — retail's
	/// <c>ATTACKERI_HAS_LOWEST_HP</c>, which our aggro list had no word for until now.
	/// </summary>
	[Fact]
	public void FromTheSecondRungHeTurnsOnTheWeakest()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		// The tank holds him; a third of the group is nearly dead and is not who he is fighting.
		raid[2].GetLifeStats().SetCurrentHpPercent(5);
		Assert.Same(raid[0], boss.GetTarget());

		BossAiHarness.SetExactPercent(boss, 64);
		for (int i = 0; i < 11; i++)
		{
			BossAiHarness.Rehate(boss, raid[0]);
			BossAiHarness.Rehate(boss, raid[1]);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Equal(4, Count(harness, SecondWave));
		Assert.Same(raid[2], boss.GetTarget());
	}

	/// <summary>Dying clears every wave, whichever rungs he reached.</summary>
	[Fact]
	public void DyingClearsEveryWave()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 89);
		Advance(harness, boss, raid, 11);
		Assert.Equal(4, Count(harness, FirstWave));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(0, Count(harness, FirstWave));
	}
}

/// <summary>
/// Pins for <see cref="KlawEggAI"/>, translated from retail pattern <c>ND2_NeutEgg2</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Here rather than in a file of its own because it is the other half of Lestin's story: correcting
/// his summon table left the faithful subordinate with nothing to spawn it, and the egg is where it
/// was always supposed to come from.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class KlawEggAiTests
{
	private const int Beluslan = 220030000;
	private const int Egg = 280482;
	private const int FaithfulSubordinate = 280481;

	/// <summary>The egg hatches on waking: a klaw where it stood, and the egg itself gone.</summary>
	[Fact]
	public void TheEggHatchesAndRemovesItself()
	{
		using BossAiHarness harness = BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(KlawEggAI), typeof(AggressiveNpcAI)).Build();

		Npc egg = harness.Spawn(Egg, 300f, 300f, 200f);

		Npc klaw = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == FaithfulSubordinate));
		Assert.Equal(300f, klaw.GetX(), 1);
		Assert.Equal(300f, klaw.GetY(), 1);
		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == Egg);
	}

	/// <summary>And the klaw stays for ten minutes rather than for the fight.</summary>
	[Fact]
	public void TheKlawStaysForTenMinutes()
	{
		using BossAiHarness harness = BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(KlawEggAI), typeof(AggressiveNpcAI)).Build();

		harness.Spawn(Egg, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(590));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == FaithfulSubordinate));

		harness.Clock.Advance(TimeSpan.FromSeconds(20));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == FaithfulSubordinate));
	}
}
