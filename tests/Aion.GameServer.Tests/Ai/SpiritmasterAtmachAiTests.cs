using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Spiritmaster atmach, who summoned nothing.
/// </summary>
/// <remarks>
/// See <see cref="SpiritmasterAtmachAI"/>. He ran plain <c>aggressive</c> while a <c>spawn_helpers</c>
/// block that nothing read named two npcs his retail pattern never mentions.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SpiritmasterAtmachAiTests
{
	private const int DarkPoeta = 300040000;

	private const int Atmach = 214843;
	private const int FrostRain = 281246;
	private const int Underling = 280645;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(SpiritmasterAtmachAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Player) Fighting()
	{
		BossAiHarness harness = NewHarness();
		Npc atmach = harness.Spawn(Atmach, 500f, 500f, 200f);
		Player player = harness.SpawnPlayer(504f, 500f, 200f);
		harness.Engage(atmach, player);
		return (harness, atmach, player);
	}

	private static void Advance(BossAiHarness harness, Npc atmach, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(atmach, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>The trap goes down when the fight starts.</b>
	/// </summary>
	[Fact]
	public void TheTrapGoesDownWhenTheFightStarts()
	{
		(BossAiHarness harness, Npc atmach, Player player) = Fighting();
		using BossAiHarness _ = harness;

		Assert.Equal(1, Count(harness, FrostRain));
	}

	/// <summary>
	/// <b>Two underlings when he first crosses thirty-five.</b>
	/// </summary>
	[Fact]
	public void TwoUnderlingsBelowThirtyFive()
	{
		(BossAiHarness harness, Npc atmach, Player player) = Fighting();
		using BossAiHarness _ = harness;
		BossAiHarness.SetExactPercent(atmach, 30);

		Advance(harness, atmach, player, 10);

		Assert.Equal(2, Count(harness, Underling));
	}

	/// <summary>
	/// <b>And none above it.</b> The rung is the only thing in his pattern that summons a fighting add,
	/// so a translation that dropped its band would look identical until somebody watched a full fight.
	/// </summary>
	[Fact]
	public void NoUnderlingsAboveThirtyFive()
	{
		(BossAiHarness harness, Npc atmach, Player player) = Fighting();
		using BossAiHarness _ = harness;
		BossAiHarness.SetExactPercent(atmach, 60);

		Advance(harness, atmach, player, 20);

		Assert.Equal(0, Count(harness, Underling));
	}

	/// <summary>
	/// <b>They come once, not every six seconds.</b> Retail guards the rung with a one-shot flag and the
	/// heartbeat it re-arms would otherwise bring two more on every turn of timer 0.
	/// </summary>
	[Fact]
	public void TheUnderlingsComeOnce()
	{
		(BossAiHarness harness, Npc atmach, Player player) = Fighting();
		using BossAiHarness _ = harness;
		BossAiHarness.SetExactPercent(atmach, 30);

		Advance(harness, atmach, player, 40);

		Assert.Equal(2, Count(harness, Underling));
	}

	/// <summary>
	/// <b>His underlings go when he does.</b> Retail's <c>on_killed_by_user</c> despawns
	/// <c>SPAWN_ID_2</c>, which is the group they are filed under.
	/// </summary>
	/// <remarks>
	/// <b>The trap is deliberately not asserted either way.</b> Retail clears only <c>SPAWN_ID_2</c> on
	/// death, which reads as the trap outliving him — but the trap carries
	/// <c>despawn_at_attack_state=TRUE</c>, and what that means for something spawned <i>on entering</i>
	/// combat is not established by anything this work has read. In this port it goes with him. Pinning
	/// a guess in either direction would freeze it; see docs/retail-ai-fidelity.md.
	/// </remarks>
	[Fact]
	public void HisUnderlingsGoWhenHeDies()
	{
		(BossAiHarness harness, Npc atmach, Player player) = Fighting();
		using BossAiHarness _ = harness;
		BossAiHarness.SetExactPercent(atmach, 30);
		Advance(harness, atmach, player, 10);
		Assert.Equal(2, Count(harness, Underling));

		BossAiHarness.Kill(atmach, player);

		Assert.Equal(0, Count(harness, Underling));
	}
}
