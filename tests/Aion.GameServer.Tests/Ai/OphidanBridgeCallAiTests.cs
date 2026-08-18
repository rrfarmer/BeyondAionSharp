using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="OphidanBridgeCallAI"/>, translated from retail patterns
/// <c>BIDF5_U01_Boss_Wi</c>, <c>BIDF5_U01_Monster_01</c> and the twelve <c>BIDF5_U01_Runaway_*</c>
/// patterns (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Sixteen npcs share one branch pair: engaging calls everything within thirty metres onto your
/// target, and answering the call is itself an entry into combat, so the pull chains across the
/// bridge. Every pin here holds the player <b>forty metres away from the listener</b> so that a
/// fugitive which found the fight by itself would fail rather than pass — the geometry lesson from
/// the naga entry, applied from the start this time.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class OphidanBridgeCallAiTests
{
	private const int OphidanBridge = 300590000;

	private const int Aethercaster = 235769;
	private const int Aetherknife = 235771;
	private const int Mazikin = 235756;
	private const int SpiritedVelkur = 235768;

	private const float CallerX = 323f;
	private const float CallerY = 489f;
	private const float Floor = 607f;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(OphidanBridgeCallAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// The player stands forty metres south of the caller, which puts it forty-seven from a listener
	/// twenty-five metres east — outside the listener's own reach, so only the call can deliver it.
	/// </summary>
	private static (BossAiHarness, Npc, Player) Pulled(int callerId)
	{
		BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(callerId, CallerX, CallerY, Floor);
		Player player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		harness.Engage(caller, player);
		return (harness, caller, player);
	}

	private static void Advance(BossAiHarness harness, Npc caller, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(caller, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	/// <summary><b>Pulling one calls its neighbours onto the same player.</b></summary>
	[Fact]
	public void PullingOneCallsItsNeighbours()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(Aethercaster, CallerX, CallerY, Floor);
		Npc neighbour = harness.Spawn(Mazikin, CallerX + 25f, CallerY, Floor);
		Player player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		BossAiHarness.MakeMutuallyKnown(caller, neighbour);
		BossAiHarness.MakeMutuallyKnown(neighbour, player);
		Assert.Null(neighbour.GetTarget());

		harness.Engage(caller, player);

		Assert.Same(player, neighbour.GetTarget());
	}

	/// <summary>
	/// <b>And only within thirty metres.</b> Retail's <c>range_as_meter</c> is what keeps the bridge
	/// from emptying itself on the first pull.
	/// </summary>
	[Fact]
	public void AnythingBeyondThirtyMetresIsLeftAlone()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(Aethercaster, CallerX, CallerY, Floor);
		Npc distant = harness.Spawn(Mazikin, CallerX + 40f, CallerY, Floor);
		Player player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		BossAiHarness.MakeMutuallyKnown(caller, distant);
		BossAiHarness.MakeMutuallyKnown(distant, player);

		harness.Engage(caller, player);
		Advance(harness, caller, player, 10);

		Assert.Null(distant.GetTarget());
	}

	/// <summary>
	/// <b>The call chains.</b> Answering it is an entry into combat, and entering combat is what makes
	/// an NPC call in turn — so a listener fifty metres from the pull, but twenty-five from one that
	/// heard it, joins anyway.
	/// </summary>
	[Fact]
	public void TheCallChainsThroughWhoeverHeardIt()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(Aethercaster, CallerX, CallerY, Floor);
		Npc middle = harness.Spawn(Mazikin, CallerX + 25f, CallerY, Floor);
		Npc far = harness.Spawn(Aetherknife, CallerX + 50f, CallerY, Floor);
		Player player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		BossAiHarness.MakeMutuallyKnown(caller, middle);
		BossAiHarness.MakeMutuallyKnown(middle, far);
		BossAiHarness.MakeMutuallyKnown(middle, player);
		BossAiHarness.MakeMutuallyKnown(far, player);

		harness.Engage(caller, player);

		Assert.Same(player, middle.GetTarget());
		Assert.Same(player, far.GetTarget());
	}

	/// <summary>
	/// <b>Ten thousand hate points is a hand-off, not a nudge.</b> Retail's <c>point_to_add</c> is far
	/// above anything a player accumulates, so a fugitive that has answered the call does not drift
	/// back to somebody who turns up beside it afterwards.
	/// </summary>
	/// <remarks>
	/// Written first as a decoy standing next to the listener before the pull, which measured the
	/// wrong thing entirely: hating the decoy put the listener into combat, its own call named the
	/// decoy, and the caller took the decoy too. The order matters — the call has to be the first
	/// thing that happens.
	/// </remarks>
	[Fact]
	public void TheCallOutweighsWhoeverTurnsUpAfterwards()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(Aethercaster, CallerX, CallerY, Floor);
		Npc neighbour = harness.Spawn(Mazikin, CallerX + 25f, CallerY, Floor);
		Player quarry = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		BossAiHarness.MakeMutuallyKnown(caller, neighbour);
		BossAiHarness.MakeMutuallyKnown(neighbour, quarry);

		harness.Engage(caller, quarry);
		Assert.Same(quarry, neighbour.GetTarget());

		// A second player arrives at the fugitive's elbow and hits it. One thousand hate against the
		// call's ten thousand does not move it.
		Player latecomer = harness.SpawnPlayer(CallerX + 26f, CallerY, Floor);
		BossAiHarness.MakeMutuallyKnown(neighbour, latecomer);
		BossAiHarness.Rehate(neighbour, latecomer);

		Assert.Same(quarry, neighbour.GetAggroList().GetTarget(AggroTarget.MOST_HATED));
	}

	/// <summary>
	/// <b>Normal mode does not link.</b> Spirited Velkur has neither half of the pair — the same fight
	/// with one mechanic taken out, which is why he keeps the stock AI.
	/// </summary>
	[Fact]
	public void NormalModeDoesNotLink()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;

		Npc caller = harness.Spawn(SpiritedVelkur, CallerX, CallerY, Floor);
		Npc neighbour = harness.Spawn(Mazikin, CallerX + 25f, CallerY, Floor);
		Player player = harness.SpawnPlayer(CallerX, CallerY - 40f, Floor);
		BossAiHarness.MakeMutuallyKnown(caller, neighbour);
		BossAiHarness.MakeMutuallyKnown(neighbour, player);

		harness.Engage(caller, player);
		Advance(harness, caller, player, 10);

		Assert.Null(neighbour.GetTarget());
	}
}
