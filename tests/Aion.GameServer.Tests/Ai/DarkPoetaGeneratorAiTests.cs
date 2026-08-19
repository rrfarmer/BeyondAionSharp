using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Dark Poeta's three generators, which summoned nothing at all until now.
/// </summary>
/// <remarks>
/// All three ran <c>aggressive</c>. <b>The mechanic being pinned is where the cores appear</b>: retail
/// spawns every one of them at the head of a named corridor with
/// <c>SPAWN_LOCATION_WAY_POINT_START</c>, not at the generator, and the walk down is the time the group
/// gets to intercept. A core standing on the generator would be the same count and a different room, so
/// these assert distance from the summoner rather than only how many arrived.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DarkPoetaGeneratorAiTests
{
	private const int DarkPoeta = 300040000;

	private const int MainGenerator = 214895;

	private static readonly int[] Cores = [281088, 281089, 281090, 281091, 281092, 281093];

	/// <summary>Where the generator stands, well clear of the corridor mouths at (233, 309).</summary>
	private const float GeneratorX = 300f;
	private const float GeneratorY = 380f;
	private const float GeneratorZ = 123.4f;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DarkPoeta).WithWorldSize(2048).WithWalkerRoutes()
			.WithAi(typeof(DarkPoetaGeneratorAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> LiveCores(BossAiHarness harness) =>
		harness.LiveNpcs().Where(n => Cores.Contains(n.GetNpcId())).ToList();

	private static (BossAiHarness, Npc) Engaged(int hpPercent)
	{
		BossAiHarness harness = NewHarness();
		Npc generator = harness.Spawn(MainGenerator, GeneratorX, GeneratorY, GeneratorZ);
		Player player = harness.SpawnPlayer(GeneratorX + 2f, GeneratorY + 2f, GeneratorZ, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(generator, player);
		harness.Engage(generator, player);
		generator.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		BossAiHarness.SetHpPercent(generator, hpPercent);
		return (harness, generator);
	}

	/// <summary><b>At full health it feeds nothing</b> — the first threshold is eighty.</summary>
	[Fact]
	public void AFullGeneratorSendsNoCores()
	{
		var (harness, _) = Engaged(100);
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		Assert.Empty(LiveCores(harness));
	}

	/// <summary><b>Below eighty, two cores</b>, once — retail's <c>FLAGVARI_BETA_1</c>.</summary>
	[Fact]
	public void BelowEightyItSendsTwoCoresAndThenNoMore()
	{
		var (harness, _) = Engaged(79);
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(2, LiveCores(harness).Count);

		// The core clock keeps ticking every five seconds; the flag is what stops the band repeating.
		harness.Clock.Advance(TimeSpan.FromSeconds(20));
		Assert.Equal(2, LiveCores(harness).Count);
	}

	/// <summary>
	/// <b>And they arrive at the corridor, not at the generator.</b> This is the whole point of
	/// <c>SPAWN_LOCATION_WAY_POINT_START</c>, and the assertion that would fail if the new spawn location
	/// silently fell back to placing them at the summoner.
	/// </summary>
	[Fact]
	public void TheCoresArriveAtTheCorridorHead()
	{
		var (harness, generator) = Engaged(79);
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(6));

		Assert.All(LiveCores(harness), core =>
			Assert.True(Distance(core, generator) > 40f,
				$"a core stood {Distance(core, generator):0} metres from the generator, "
					+ "which means it spawned at the summoner rather than at its route"));
	}

	/// <summary><b>Below thirty-five, three more</b>, and the two bands are independent.</summary>
	[Fact]
	public void BelowThirtyFiveItSendsThreeMore()
	{
		var (harness, _) = Engaged(34);
		using BossAiHarness _h = harness;

		// One tick opens both bands in turn: the low branch outranks the high one, and the heartbeat
		// re-arms, so the high band opens on the following tick.
		harness.Clock.Advance(TimeSpan.FromSeconds(11));

		Assert.Equal(5, LiveCores(harness).Count);
	}

	/// <summary>
	/// <b>The thirty-second cores leave and the others stay</b>, which is retail's per-core
	/// <c>live_time</c> rather than one number for a band.
	/// </summary>
	/// <remarks>
	/// The main generator's low band sends three — two that stay and one that goes at thirty seconds — and
	/// its high band sends two, one of each. So the count falls twice, five seconds apart, mirroring the
	/// five seconds between the bands opening. <b>A class that flattened the lifetimes per band would show
	/// one drop, not two</b>, which is what this measures.
	/// </remarks>
	[Fact]
	public void TheTimedCoresLeaveAndTheRestStay()
	{
		var (harness, _) = Engaged(34);
		using BossAiHarness _h = harness;

		// The low band opens on the core clock's first tick; the high band on the next, because the low
		// branch outranks it and consumes the tick it fires on.
		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(3, LiveCores(harness).Count);

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.Equal(5, LiveCores(harness).Count);

		// Thirty seconds after the low band opened, its one timed core goes and the high band's has not.
		harness.Clock.Advance(TimeSpan.FromSeconds(25));
		Assert.Equal(4, LiveCores(harness).Count);

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.Equal(3, LiveCores(harness).Count);
	}

	/// <summary><b>Dying sends one last core</b>, which is where the room's last wave comes from.</summary>
	[Fact]
	public void DyingSendsOneLastCore()
	{
		var (harness, generator) = Engaged(100);
		using BossAiHarness _h = harness;

		Assert.Empty(LiveCores(harness));
		generator.GetAi().OnGeneralEvent(AiEventType.Died);

		Npc core = Assert.Single(LiveCores(harness));
		Assert.Equal(281089, core.GetNpcId());
		Assert.True(Distance(core, generator) > 40f, "the dying core spawned at the generator");
	}

	private static float Distance(Npc a, Npc b) =>
		(float)Math.Sqrt(((a.GetX() - b.GetX()) * (a.GetX() - b.GetX()))
			+ ((a.GetY() - b.GetY()) * (a.GetY() - b.GetY())));
}
