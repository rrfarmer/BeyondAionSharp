using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for retail's <c>30002</c> — the protector calling its killer (see docs/retail-ai-fidelity.md).
/// </summary>
/// <remarks>
/// <b>This is the middle message of a three-message loop, and it was sent by nothing.</b> A killer woke
/// and pulled the protectors (30001), a dying protector called it off (30003), and no protector ever
/// called the killer to <em>itself</em> — so <c>FortressKillerAI</c>'s answer to 30002 had never once
/// been reachable. The fight could start and it could end; it could not move.
/// <para>
/// The cadences here are read out of retail's timer chains rather than chosen, which is why the pins
/// name seconds: an artifact guard at 21.5 then every 22, and a village chief the moment it is engaged
/// then every 5.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ProtectorCallTests
{
	private const int Reshanta = 400010000;

	/// <summary><c>Ab1_1401_Boss_Li_3</c> — first call at 21.5 seconds, then every 22.</summary>
	private const int ArtifactGuard = 251469;

	/// <summary><c>LDF5_Village_chief01_L</c> — calls the instant it is engaged, then every 5 seconds.</summary>
	private const int VillageChief = 277069;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(ArtifactProtectorAI), typeof(FortressProtectorNpcAI),
				typeof(AbyssGuardSimpleAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>An artifact guard calls its killer while the fight runs.</b> Nothing sent this message before
	/// the timer chain was read out of the pattern.
	/// </summary>
	[Fact]
	public void AnArtifactGuardCallsItsKillerWhileTheFightRuns()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(ArtifactGuard, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
		{
			harness.Engage(guard, player);
			for (int second = 0; second < 25; second++)
			{
				BossAiHarness.Rehate(guard, player);
				BossAiHarness.KeepAlive(player);
				harness.Clock.Advance(System.TimeSpan.FromSeconds(1));
			}
		}

		Assert.Contains(AbstractSiegeProtectorAI.CallTheKiller, seen);
	}

	/// <summary>
	/// <b>And not before its chain reaches the rung.</b> Retail takes 21.5 seconds to get there, so a
	/// guard that called on contact would be a different fight — and is what a cadence fitted to the
	/// village chiefs would have produced.
	/// </summary>
	[Fact]
	public void AndNotBeforeItsChainReachesTheRung()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(ArtifactGuard, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
		{
			harness.Engage(guard, player);
			for (int second = 0; second < 15; second++)
			{
				BossAiHarness.Rehate(guard, player);
				BossAiHarness.KeepAlive(player);
				harness.Clock.Advance(System.TimeSpan.FromSeconds(1));
			}
		}

		Assert.DoesNotContain(AbstractSiegeProtectorAI.CallTheKiller, seen);
	}

	/// <summary>
	/// <b>A village chief calls the moment it is engaged.</b> Retail broadcasts in the enter-combat rung
	/// itself and then every five seconds — its own comment on that rung says so — and the first version
	/// of the extractor missed the opening call and reported it five seconds late.
	/// </summary>
	[Fact]
	public void AVillageChiefCallsTheMomentItIsEngaged()
	{
		using BossAiHarness harness = NewHarness();
		Npc chief = harness.Spawn(VillageChief, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			harness.Engage(chief, player);

		Assert.Contains(AbstractSiegeProtectorAI.CallTheKiller, seen);
	}

	/// <summary>
	/// <b>And keeps calling.</b> Five seconds apart, so a twenty-second fight holds several — the pin is
	/// on there being more than one, because the exact count depends on where the clock lands.
	/// </summary>
	[Fact]
	public void AndKeepsCalling()
	{
		using BossAiHarness harness = NewHarness();
		Npc chief = harness.Spawn(VillageChief, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 290f, 200f, race: Race.ASMODIANS);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
		{
			harness.Engage(chief, player);
			for (int second = 0; second < 20; second++)
			{
				BossAiHarness.Rehate(chief, player);
				BossAiHarness.KeepAlive(player);
				harness.Clock.Advance(System.TimeSpan.FromSeconds(1));
			}
		}

		Assert.True(seen.Count(m => m == AbstractSiegeProtectorAI.CallTheKiller) >= 3,
			$"a chief called {seen.Count(m => m == AbstractSiegeProtectorAI.CallTheKiller)} times in "
			+ "twenty seconds; retail's rung repeats every five");
	}

	/// <summary>
	/// <b>The table carries three different cadences, not one.</b> A model fitted to the artifact guards
	/// and applied everywhere would be wrong for the balaur twins and wrong again for the chiefs, and
	/// this is the arithmetic that says so.
	/// </summary>
	[Fact]
	public void TheTableCarriesMoreThanOneCadence()
	{
		var shapes = ProtectorCalls.ByNpc.Values
			.Select(call => (call.First, call.Period))
			.Distinct()
			.ToList();

		Assert.True(shapes.Count >= 3, $"only {shapes.Count} distinct cadences in the table");
		Assert.Contains((21500, 22000), shapes);
		Assert.Contains((0, 5000), shapes);
	}
}
