using System;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Tiamat's breath: the beacon marks the line, and two seconds later the damage lands on it.
/// </summary>
/// <remarks>
/// The rotation places the beacons and always has. What was missing is the second half — each beacon's
/// own pattern arms a 2000ms idle timer and spawns its <c>_dmg</c> twin. Twelve of the fifteen beacons
/// were on plain <c>aggressive</c>, so the warning appeared and nothing followed it.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TiamatBeaconAiTests
{
	private const int DragonLordsRefuge = 300520000;

	/// <summary>A middle beacon: eleven hits in an absolute line, two seconds each.</summary>
	private const int MiddleBeacon = 283156;

	/// <summary>A left beacon: one hit on the marker itself, three seconds.</summary>
	private const int LeftBeacon = 283234;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(4096)
			.WithAi(typeof(TiamatBeaconAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary><b>A middle beacon lays its eleven hits when the count runs out.</b></summary>
	[Fact]
	public void AMiddleBeaconLaysTheBreathAfterTwoSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc beacon = harness.Spawn(MiddleBeacon, 460f, 514f, 417f);

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == 283068);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(11, harness.LiveNpcs().Count(n => n.GetNpcId() == 283068));
	}

	/// <summary><b>And nothing lands before the count does.</b></summary>
	[Fact]
	public void AndNothingLandsBeforeTheCountDoes()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(MiddleBeacon, 460f, 514f, 417f);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == 283068);
	}

	/// <summary>
	/// <b>A left beacon lays a single hit on itself.</b> Retail's <c>SPAWN_LOCATION_MY_POINT</c>: those
	/// blocks carry no coordinates, and reading them as absolute would put the breath at the origin.
	/// </summary>
	[Fact]
	public void ALeftBeaconLaysOneHitOnItself()
	{
		using BossAiHarness harness = NewHarness();
		Npc beacon = harness.Spawn(LeftBeacon, 470f, 520f, 417f);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Npc hit = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == 283235));
		Assert.Equal(beacon.GetX(), hit.GetX(), 1);
		Assert.Equal(beacon.GetY(), hit.GetY(), 1);
	}

	/// <summary><b>Every beacon in the table carries a damage npc and a delay.</b></summary>
	[Fact]
	public void TheTableIsFifteenBeaconsAndRetailsTwoShapes()
	{
		int middles = 0;
		int sides = 0;
		foreach ((int _, TiamatBeacons.Breath breath) in TiamatBeacons.ByBeacon)
		{
			Assert.Equal(2000, breath.DelayMillis);
			Assert.True(breath.DamageNpc > 0);

			if (breath.AtTheBeacon)
			{
				// MY_POINT blocks carry no coordinates at all.
				Assert.Empty(breath.Spots);
				Assert.Equal(3, breath.LiveSeconds);
				sides++;
			}
			else
			{
				Assert.NotEmpty(breath.Spots);
				Assert.Equal(2, breath.LiveSeconds);
				middles++;
			}
		}

		Assert.Equal(15, TiamatBeacons.ByBeacon.Count);
		Assert.Equal(8, sides);
		Assert.Equal(7, middles);
	}
}
