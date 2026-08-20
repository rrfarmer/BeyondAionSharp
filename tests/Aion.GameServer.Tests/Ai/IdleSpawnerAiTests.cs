using System;
using System.Linq;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// NPCs that wake, wait, and place something — twenty-one of them, all previously inert.
/// </summary>
/// <remarks>
/// Retail has <b>three</b> spawn locations on these rungs and reading one as another puts the add
/// somewhere else entirely. Both mistakes were made and caught by reading the emitted table:
/// <c>MY_POINT</c> carries no coordinates and lands at the origin if read as absolute, and
/// <c>RELATIVE</c> carries an offset — four arena adds went to x=1,y=1, the corner of the map.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class IdleSpawnerAiTests
{
	private const int AnyMap = 300520000;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AnyMap).WithWorldSize(4096)
			.WithAi(typeof(IdleSpawnerAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary><b>An absolute placement goes to retail's own coordinates.</b></summary>
	[Fact]
	public void AnAbsolutePlacementGoesWhereRetailSaysAndNotToTheSpawner()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(217575, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Npc placed = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == 282436));
		Assert.Equal(1629f, placed.GetX(), 1);
		Assert.Equal(154f, placed.GetY(), 1);
	}

	/// <summary><b>A MY_POINT placement lands on the spawner, not at the origin.</b></summary>
	[Fact]
	public void AnAtTheNpcPlacementLandsOnTheSpawner()
	{
		using BossAiHarness harness = NewHarness();
		// 282447 places a Tiamat tornado, a hazard whose own pattern casts and removes it, so nothing
		// is left to measure a moment later. This spawner places something that stays, which is what a
		// geometry claim needs; the hazard's own behaviour is pinned in the wake/idle tests.
		Npc spawner = harness.Spawn(855204, 700f, 800f, 250f);

		harness.Clock.Advance(TimeSpan.FromSeconds(6));

		Npc placed = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == 855206));
		Assert.Equal(spawner.GetX(), placed.GetX(), 0);
		Assert.Equal(spawner.GetY(), placed.GetY(), 0);
	}

	/// <summary>
	/// <b>A RELATIVE placement is an offset from the spawner.</b> Four adds, one metre out on each
	/// diagonal — not four adds at the corner of the map.
	/// </summary>
	[Fact]
	public void ARelativePlacementIsAnOffsetFromTheSpawner()
	{
		using BossAiHarness harness = NewHarness();
		Npc spawner = harness.Spawn(282414, 700f, 800f, 250f);

		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Npc[] placed = harness.LiveNpcs().Where(n => n.GetNpcId() == 282415).ToArray();
		Assert.Equal(4, placed.Length);
		foreach (Npc add in placed)
		{
			Assert.InRange(add.GetX(), spawner.GetX() - 2f, spawner.GetX() + 2f);
			Assert.InRange(add.GetY(), spawner.GetY() - 2f, spawner.GetY() + 2f);
		}
	}

	/// <summary><b>Nothing is placed before the wait is up.</b></summary>
	[Fact]
	public void NothingIsPlacedEarly()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(282447, 700f, 800f, 250f);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == 283069);
	}

	/// <summary>
	/// <b>A rung with no re-arm in retail does not gain one.</b> Fourteen of the nineteen patterns
	/// place once and stop; emitting a re-arm anyway would turn a one-shot into a heartbeat.
	/// </summary>
	[Fact]
	public void ARungWithNoReArmPlacesOnce()
	{
		Assert.Equal(-1, IdleSpawns.ByNpc[282447].ReArmMillis);

		using BossAiHarness harness = NewHarness();
		harness.Spawn(282447, 700f, 800f, 250f);

		// Past its own two-second wait, so the one placement has happened and its three seconds of life
		// have run out. Sampled just after a heartbeat would have fired rather than on a round minute:
		// a periodic spawner's add lives three seconds out of every thirty, so a five-minute mark can
		// easily land in a gap and pass whether the bug is there or not. This one failed to catch a
		// mutation for exactly that reason.
		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		harness.Clock.Advance(TimeSpan.FromSeconds(31));

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == 283069);
	}
	/// <summary><b>A permanent placement still goes when its spawner does.</b></summary>
	/// <remarks>
	/// All 40 placements in this table carry retail's <c>despawn_at_attack_state</c>, and three of them
	/// have no live time at all -- <c>IDYun_Temp_15</c> below is one. Those three are the leak: without
	/// the flag they stand on the ground forever, and nothing else was ever going to remove them,
	/// because the spawner does not track what it placed (<c>SPAWN_ID_NONE</c>) so no despawn branch
	/// can name them either.
	/// </remarks>
	[Fact]
	public void APermanentPlacementLeavesWithItsSpawner()
	{
		using BossAiHarness harness = NewHarness();
		Npc spawner = harness.Spawn(282547, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(1, harness.LiveNpcs().Count(npc => npc.GetNpcId() == 282544));

		Player player = harness.SpawnPlayer(900f, 900f, 200f);
		BossAiHarness.Kill(spawner, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(0, harness.LiveNpcs().Count(npc => npc.GetNpcId() == 282544));
	}
}
