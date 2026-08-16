using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="AwakenedChamberLordAI"/>, translated from retail pattern
/// <c>BGuard_ChiefD</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Three lords in three chambers that share a layout, so the same absolute death-wave points serve all
/// of them — which is the thing most worth pinning, since a shared pattern with absolute coordinates is
/// normally a reason not to trust them.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AwakenedChamberLordAiTests
{
	private const int KrotanChamber = 300140000;
	private const int KysisChamber = 300120000;
	private const int MirenChamber = 300130000;

	private const int KrotanLord = 215136;
	private const int KysisDuke = 215179;
	private const int MirenPrince = 215222;

	private const int IllusionGate = 281226;
	private const int DrakanByTeleporter = 296339;
	private const int DrakanByBarrier = 296338;

	/// <summary>Where all three stand in their own chambers.</summary>
	private const float LordX = 526.4f;
	private const float LordY = 845.3f;
	private const float LordZ = 190.5f;

	public static TheoryData<int, int> Lords => new()
	{
		{ KrotanChamber, KrotanLord },
		{ KysisChamber, KysisDuke },
		{ MirenChamber, MirenPrince },
	};

	private static (BossAiHarness, Npc, Player) Engaged(int mapId, int npcId, int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(mapId).WithWorldSize(2048)
			.WithAi(typeof(AwakenedChamberLordAI), typeof(GroupGateAI), typeof(AggressiveNpcAI)).Build();
		Npc lord = harness.Spawn(npcId, LordX, LordY, LordZ);
		Player player = harness.SpawnPlayer(LordX + 2f, LordY + 2f, LordZ);
		BossAiHarness.SetHpPercent(lord, hpPercent);
		harness.Engage(lord, player);
		return (harness, lord, player);
	}

	private static void Advance(BossAiHarness harness, Npc lord, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(lord, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	[Theory]
	[MemberData(nameof(Lords))]
	public void AboveTwentyFiveNoGateOpens(int mapId, int npcId)
	{
		var (harness, lord, player) = Engaged(mapId, npcId, 60);
		using BossAiHarness _h = harness;

		Advance(harness, lord, player, 40);

		Assert.Equal(0, Count(harness, IllusionGate));
	}

	[Theory]
	[MemberData(nameof(Lords))]
	public void BelowTwentyFiveAGateOpensAtItsFeet(int mapId, int npcId)
	{
		var (harness, lord, player) = Engaged(mapId, npcId, 20);
		using BossAiHarness _h = harness;

		Advance(harness, lord, player, 8);

		Npc gate = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == IllusionGate));
		Assert.InRange(gate.GetX(), LordX - 4f, LordX + 4f);
		Assert.InRange(gate.GetY(), LordY - 4f, LordY + 4f);
	}

	/// <summary>One gate for the fight, not one every five seconds.</summary>
	[Fact]
	public void TheGateOpensOnceOnly()
	{
		var (harness, lord, player) = Engaged(KrotanChamber, KrotanLord, 20);
		using BossAiHarness _h = harness;
		var seen = new HashSet<Npc>();

		for (int i = 0; i < 60; i++)
		{
			Advance(harness, lord, player, 1);
			foreach (Npc gate in harness.LiveNpcs().Where(n => n.GetNpcId() == IllusionGate))
				seen.Add(gate);
		}

		Assert.Single(seen);
	}

	/// <summary>
	/// Six by teleporter, two at each of three points, and three through the barrier. The same points
	/// in all three chambers, which is what makes one table serve them.
	/// </summary>
	[Theory]
	[MemberData(nameof(Lords))]
	public void DyingBringsSixByTeleporterAndThreeThroughTheBarrier(int mapId, int npcId)
	{
		var (harness, lord, player) = Engaged(mapId, npcId, 20);
		using BossAiHarness _h = harness;
		Assert.Equal(0, Count(harness, DrakanByTeleporter));

		lord.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(6, Count(harness, DrakanByTeleporter));
		Assert.Equal(3, Count(harness, DrakanByBarrier));

		// Two at each of the three teleport points, not six in one place.
		Assert.Equal(3, harness.LiveNpcs()
			.Where(n => n.GetNpcId() == DrakanByTeleporter)
			.Select(n => (n.GetX(), n.GetY()))
			.Distinct()
			.Count());
	}

	/// <summary>A parting shot, not a second fight: eighteen seconds and twelve.</summary>
	[Fact]
	public void TheDeathWaveTimesOut()
	{
		var (harness, lord, player) = Engaged(KrotanChamber, KrotanLord, 20);
		using BossAiHarness _h = harness;
		lord.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(6, Count(harness, DrakanByTeleporter));
		Assert.Equal(3, Count(harness, DrakanByBarrier));

		Advance(harness, lord, player, 14);
		Assert.Equal(6, Count(harness, DrakanByTeleporter));
		Assert.Equal(0, Count(harness, DrakanByBarrier));

		Advance(harness, lord, player, 6);
		Assert.Equal(0, Count(harness, DrakanByTeleporter));
	}
}
