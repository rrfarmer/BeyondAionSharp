using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The hard mode's eight incarnations, which ran an invented summon cycle or nothing at all.
/// </summary>
/// <remarks>
/// <b>There are twelve incarnations, not four.</b> 219365-219368 are normal mode; 236278-236281 and
/// 856030-856033 are two id sets running the same four <c>IDTiamat_Hard_*_Key</c> patterns. Six of the
/// eight fell through to a summon cycle nobody's pattern describes — two hazards on two random players
/// within thirty metres, every thirty seconds, on a clock that started at activation — and <b>both
/// Wrathclaws were left on plain <c>aggressive</c></b>, the same omission made twice.
/// <para>
/// The hard patterns turned out to be the normal patterns with a parallel set of hazard and sphere ids
/// and every number identical, so what is worth pinning is that each hard npc drops <i>its own</i>
/// hazards, on its pattern's clock, and never on the old invented one.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TiamatsHardIncarnationAiTests
{
	private const int DragonLordsRefuge = 300520000;

	private const int HardCavityOfEarth = 856068;
	private const int HardPetrificationCrystal = 856072;
	private const int HardGravityWhirlpool = 856074;

	private const int HardSphereOfWrath = 856078;
	private const int HardSphereOfPeace = 856080;

	/// <summary>The two points the spheres occupy, from the pattern's own absolute coordinates.</summary>
	private const float NorthX = 214f;
	private const float SouthX = 185f;

	/// <summary>Both hard-mode id sets, paired with the hazard each is supposed to drop.</summary>
	public static TheoryData<int, int> HardIncarnations => new TheoryData<int, int>
	{
		{ 236278, HardCavityOfEarth },
		{ 236279, HardGravityWhirlpool },
		{ 236281, HardPetrificationCrystal },
		{ 856030, HardCavityOfEarth },
		{ 856031, HardGravityWhirlpool },
		{ 856033, HardPetrificationCrystal },
	};

	public static TheoryData<int> BothWrathclaws => new TheoryData<int> { 236280, 856032 };

	private static BossAiHarness NewHarness() => BossAiHarness.For(DragonLordsRefuge)
		.WithWorldSize(2048)
		.WithAi(typeof(TiamatsIncarnationAI), typeof(TiamatsIncarnationSpawnsAI), typeof(AggressiveNpcAI),
			typeof(GeneralNpcAI))
		.Build();

	private static Npc SpawnBoss(BossAiHarness harness, int npcId) =>
		harness.Spawn(npcId, 470f, 510f, 418f);

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Each drops its own hard-mode hazard three seconds in</b>, which is its pattern's first timer.
	/// </summary>
	[Theory]
	[MemberData(nameof(HardIncarnations))]
	public void EachHardIncarnationDropsItsOwnHazardOnThePowerAttack(int npcId, int hazard)
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, npcId);
		Player player = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, hazard));

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.True(Count(harness, hazard) > 0, "the power attack should have left a hard-mode hazard");
	}

	/// <summary>
	/// <b>The invented cycle's own npcs never appear.</b>
	/// </summary>
	/// <remarks>
	/// The cycle picked at random from a two-id list per incarnation, and one id in each list —
	/// collapsing earth for Fissurefang, thunderbolt whirlpool for Graviwing — <b>is spawned by no branch
	/// of any of the four patterns</b>. Its presence is therefore proof the old cycle ran, and its absence
	/// over a full minute of combat is proof it did not. That is a sharper test than counting hazards,
	/// because the pattern drops the other id legitimately every nine seconds.
	/// </remarks>
	[Theory]
	[InlineData(236278, 856070)]
	[InlineData(856030, 856070)]
	[InlineData(236279, 856076)]
	[InlineData(856031, 856076)]
	public void TheInventedCyclesOwnSummonsNeverAppear(int npcId, int cycleOnly)
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, npcId);
		var raid = new List<Player>();
		for (int i = 0; i < 4; i++)
			raid.Add(harness.SpawnPlayer(472f + i, 512f, 418f));
		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);

		// Past the cycle's first placement at twenty seconds and its second at fifty.
		harness.Clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Equal(0, Count(harness, cycleOnly));
	}

	/// <summary>
	/// <b>Both Wrathclaws set the hard mode's own two spheres when they wake</b>, one at each point.
	/// </summary>
	[Theory]
	[MemberData(nameof(BothWrathclaws))]
	public void BothWrathclawsPlaceTheHardSpheres(int npcId)
	{
		using BossAiHarness harness = NewHarness();
		SpawnBoss(harness, npcId);

		Npc wrath = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == HardSphereOfWrath);
		Npc peace = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == HardSphereOfPeace);

		// Wrath north, peace south, which is the arrangement the fight starts from.
		Assert.Equal(NorthX, wrath.GetX(), 1);
		Assert.Equal(SouthX, peace.GetX(), 1);

		// And not the normal mode's spheres, which is what binding them to the wrong table would give.
		Assert.Equal(0, Count(harness, 282979));
		Assert.Equal(0, Count(harness, 282733));
	}

	/// <summary>
	/// <b>The area attack clears both spheres and puts them back</b>, sometimes swapped — which is the
	/// whole of Wrathclaw's fight, and which neither hard-mode Wrathclaw did at all before.
	/// </summary>
	[Theory]
	[MemberData(nameof(BothWrathclaws))]
	public void TheAreaAttackReplacesBothSpheres(int npcId)
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, npcId);
		Player player = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, player);

		HashSet<Npc> first = harness.LiveNpcs()
			.Where(n => n.GetNpcId() == HardSphereOfWrath || n.GetNpcId() == HardSphereOfPeace)
			.ToHashSet();
		Assert.Equal(2, first.Count);

		// The area attack is the fifteen-second timer.
		harness.Clock.Advance(TimeSpan.FromSeconds(16));

		List<Npc> now = harness.LiveNpcs()
			.Where(n => n.GetNpcId() == HardSphereOfWrath || n.GetNpcId() == HardSphereOfPeace)
			.ToList();

		// Still exactly two, and neither is one of the originals: the branch despawns before it respawns,
		// so a class that only ever placed them once would show the same count and the same objects.
		Assert.Equal(2, now.Count);
		Assert.DoesNotContain(now, n => first.Contains(n));
	}
}
