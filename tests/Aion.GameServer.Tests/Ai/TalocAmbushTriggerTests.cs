using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using System.Linq;
using Xunit;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <b><c>Elim_EventC</c> and <c>Elim_EventD</c>: a marker that turns into an ambush when somebody walks
/// past it.</b>
/// </summary>
/// <remarks>
/// Each trigger carries the same three actions on both sighting handlers — spawn three mobs at fixed
/// coordinates, then remove itself. The coordinates are retail's own, absolute rather than relative to
/// the trigger, so the ambush lands where the room was built for it rather than on top of the marker.
/// <para>
/// <b>This port places the mobs and not the trigger.</b> 216134-216137 have spawn rows in
/// <c>300190000_Taloc's_Hollow.xml</c>; 281531 and 281532 have none. So the three mobs stand in the
/// room from the moment the instance opens, where retail has them appear as somebody walks in. That is
/// a difference in <i>spawn data</i>, not in AI, and this port's spawn data is aionemu's — so it is
/// recorded here rather than quietly rewritten. The pattern layer is ready for the trigger the day
/// somebody decides those rows should change.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public class TalocAmbushTriggerTests
{
	private const int TalocsHollow = 300190000;

	/// <summary><c>Elim_EventC</c>'s marker.</summary>
	private const int TriggerC = 281531;

	/// <summary><c>Elim_EventD</c>'s marker.</summary>
	private const int TriggerD = 281532;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TalocsHollow).WithWorldSize(2048)
			.WithAi(typeof(BattleCycleAI), typeof(PassivePatternAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Walking past the marker brings the ambush and takes the marker away.</b> Three mobs for each
	/// trigger — two of one kind and one of the other — and the marker is gone in the same breath.
	/// </summary>
	[Theory]
	[InlineData(TriggerC, 216135, 2, 216137, 1)]
	[InlineData(TriggerD, 216136, 2, 216134, 1)]
	public void WalkingPastTheMarkerBringsTheAmbush(
		int trigger, int many, int howMany, int few, int howFew)
	{
		using BossAiHarness harness = NewHarness();
		Npc marker = harness.Spawn(trigger, 300f, 300f, 1145f);
		Player raider = harness.SpawnPlayer(360f, 300f, 1145f, race: Race.ELYOS);

		harness.Walk(raider, 302f, 300f, 1145f);

		Assert.Equal(howMany, Count(harness, many));
		Assert.Equal(howFew, Count(harness, few));
		Assert.DoesNotContain(marker, harness.LiveNpcs());
	}

	/// <summary>
	/// <b>Once, not once per step.</b> Retail guards both sighting rungs with the same test-and-set
	/// flag, and the marker despawning is the other half of that — but the flag is what stops a raid
	/// walking through and filling the room, and without this pin a marker that spawned its ambush on
	/// every movement notification would pass the theory above.
	/// </summary>
	[Fact]
	public void TheAmbushComesOnceHoweverFarTheRaidWalks()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(TriggerC, 300f, 300f, 1145f);
		Player raider = harness.SpawnPlayer(360f, 300f, 1145f, race: Race.ELYOS);

		harness.Walk(raider, 302f, 300f, 1145f);
		harness.Walk(raider, 303f, 300f, 1145f);
		harness.Walk(raider, 304f, 300f, 1145f);

		Assert.Equal(2, Count(harness, 216135));
		Assert.Equal(1, Count(harness, 216137));
	}

	/// <summary>
	/// <b>And not from across the room.</b> The engine's movement event covers the known list, so
	/// without the sight test the ambush would fire while the raid was still at the door.
	/// </summary>
	[Fact]
	public void WalkingAboutFarAwayDoesNotSpringIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc marker = harness.Spawn(TriggerC, 300f, 300f, 1145f);
		Player raider = harness.SpawnPlayer(360f, 300f, 1145f, race: Race.ELYOS);

		harness.Walk(raider, 355f, 300f, 1145f);

		Assert.Equal(0, Count(harness, 216135));
		Assert.Contains(marker, harness.LiveNpcs());
	}
}
