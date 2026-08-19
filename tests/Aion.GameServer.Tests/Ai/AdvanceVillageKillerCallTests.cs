using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Advance village guards answering the killer that comes for their village.
/// </summary>
/// <remarks>
/// This is the pairing retail's npc-versus-npc call family was designed around, and the one whose
/// tribes are hostile by design: the Advance killers are <c>LDF4_ADVANCE_DRGUARD</c>, whose tribe lists
/// <c>LDF4_ADVANCE_LGUARD</c> and <c>LDF4_ADVANCE_DGUARD</c> as <c>aggro</c> in the client's own
/// <c>npc_tribe_relation.xml</c> and in ours.
/// <para>
/// 113 npcs run one of the two <c>base_protector</c> patterns and not one of them heard 30001 before.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AdvanceVillageKillerCallTests
{
	private const int Gelkmaros = 220070000;

	/// <summary>An Advance village guard, and the killer whose tribe is hostile to it.</summary>
	private const int VillageGuard = 234199;
	private const int AdvanceKiller = 235543;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Gelkmaros).WithWorldSize(2048)
			.WithAi(typeof(BaseProtectorAI), typeof(FortressKillerAI), typeof(AbyssGuardCallAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary>
	/// <b>A killer arriving at the village pulls its guards onto it.</b>
	/// </summary>
	/// <remarks>
	/// The guard is never touched and no player is involved. Before this the killer stood in the village
	/// and nothing happened at all.
	/// </remarks>
	[Fact]
	public void AKillerArrivingPullsTheVillageGuardsOntoIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(VillageGuard, 300f, 300f, 200f);
		Npc killer = harness.Spawn(AdvanceKiller, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, guard);
		Assert.Null(guard.GetTarget());

		killer.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.SPAWNED);

		Assert.Equal(killer, guard.GetTarget());
	}

	/// <summary>
	/// <b>And the two really are enemies by the tribe data, not by accident.</b>
	/// </summary>
	/// <remarks>
	/// Stated as its own assertion because the previous attempt at this mechanic failed on exactly this
	/// point: a protector and a killer that share a tribe ignore each other, correctly, and picking one
	/// such pair made a working mechanic look like a data gap.
	/// </remarks>
	[Fact]
	public void AndTheTwoAreEnemiesByTheTribeData()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(VillageGuard, 300f, 300f, 200f);
		Npc killer = harness.Spawn(AdvanceKiller, 320f, 300f, 200f);

		Assert.NotEqual(guard.GetObjectTemplate().GetTribe(), killer.GetObjectTemplate().GetTribe());
		Assert.True(killer.IsEnemy(guard), "the Advance killer's tribe lists the village guard as aggro");
	}

	/// <summary>
	/// <b>A guard out of earshot stays at its post.</b>
	/// </summary>
	/// <remarks>
	/// Retail's range is fifty metres. The distance the harness can actually distinguish is narrow —
	/// the message bus's own reach ends near the same place — so this is a coarse check that the call
	/// does not simply cross the map.
	/// </remarks>
	[Fact]
	public void AGuardOutOfEarshotStaysAtItsPost()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(VillageGuard, 400f, 300f, 200f);
		Npc killer = harness.Spawn(AdvanceKiller, 300f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, guard);

		killer.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.SPAWNED);

		Assert.Null(guard.GetTarget());
	}

	/// <summary>
	/// <b>And a guard ignores a message that is not the call.</b>
	/// </summary>
	/// <remarks>
	/// 23000 reaches the same npcs and means a player to join on, at one hate point. A guard that
	/// answered both with a million would turn every call for help in a village into a stampede.
	/// </remarks>
	[Fact]
	public void AndAGuardIgnoresAMessageThatIsNotTheCall()
	{
		using BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(VillageGuard, 300f, 300f, 200f);
		Npc killer = harness.Spawn(AdvanceKiller, 320f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(killer, guard);

		NpcMessageBus.Broadcast(killer, AbyssGuardCallAI.CallForHelp, killer, 50f);

		Assert.Null(guard.GetTarget());
	}
}
