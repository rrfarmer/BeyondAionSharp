using System;
using System.Linq;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Full retail patterns on npcs that do not fight.
/// </summary>
/// <remarks>
/// 425 patterns across 462 npcs, all on <c>general</c>. <see cref="WakeVariables"/> took the ones whose
/// whole behaviour was an unguarded list of variable writes; these carry a guard, a timer, a message or
/// a spawn as well.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PassivePatternAiTests
{
	private const int Map = 300520000;

	/// <summary><c>IDDF3_BroadNPC_System</c>: shouts to fifty metres and removes itself.</summary>
	private const int Relay = 282155;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Map).WithWorldSize(4096)
			.WithAi(typeof(PassivePatternAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary><b>A relay does its one job and goes.</b></summary>
	/// <remarks>
	/// The npc exists to carry a message and leave, and until this table ran its pattern it simply
	/// stood there forever. A death-spawn pin had been counting it as a lingering add, which was only
	/// ever countable because nothing ran the pattern.
	/// </remarks>
	[Fact]
	public void ARelayShoutsAndLeaves()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Relay, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.DoesNotContain(harness.LiveNpcs(), npc => npc.GetNpcId() == Relay);
	}

	/// <summary><b>And it does not fight, whatever its pattern says.</b></summary>
	/// <remarks>
	/// The invariant the whole table rests on. Every other pattern table feeds a class descending from
	/// <c>AggressiveNpcAI</c>; binding these npcs to one of those makes scenery attack on sight, which
	/// this project did to 67 wave controllers and did not notice for a dozen entries. The same npc is
	/// spawned under both classes so that only the class differs.
	/// </remarks>
	[Fact]
	public void APassivePatternNpcIgnoresAggroWhereAnAggressiveOneDoesNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc passive = harness.SpawnWithAi(Hostile, "passive_pattern", 300f, 300f, 200f);
		// Far apart: an aggressive npc broadcasts its aggro to nearby friends, and a passive one
		// standing beside it joins through the support path, which is correct and would hide the point.
		Npc aggressive = harness.SpawnWithAi(Hostile, "aggressive", 900f, 900f, 200f);
		Player near = harness.SpawnPlayer(302f, 300f, 200f);
		Player far = harness.SpawnPlayer(902f, 900f, 200f);
		BossAiHarness.MakeMutuallyKnown(passive, near);
		BossAiHarness.MakeMutuallyKnown(aggressive, far);

		passive.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureAggro, near);
		aggressive.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureAggro, far);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.NotEmpty(aggressive.GetAggroList().Stream());
		Assert.Empty(passive.GetAggroList().Stream());
	}

	/// <summary>An npc hostile enough to players that the aggro event reaches its list.</summary>
	private const int Hostile = 217307;
}
