using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the decoy-lich markers, translated from retail patterns <c>IDCT_DebuffLich</c>, <c>_2</c>
/// and <c>_3</c> and the <c>6981</c> branch of <c>IDCT_Boss_LichKing</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class DecoyLichMarkerAiTests
{
	private const int Beshmundir = 300150000;

	private const int Marker = 281696;
	private const int Macunbello = 216245;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beshmundir).WithWorldSize(2048)
			.WithAi(typeof(DecoyLichMarkerAI), typeof(MacunbelloAI), typeof(MacunbelloSoulReaperAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	/// <summary>
	/// <b>The marker clears the liches in earshot and goes with them.</b> It exists for exactly one
	/// broadcast, which is how the room is left holding a single Macunbello.
	/// </summary>
	[Fact]
	public void TheMarkerClearsTheLichesAndGoesWithThem()
	{
		using BossAiHarness harness = NewHarness();
		Npc marker = harness.Spawn(Marker, 300f, 300f, 200f);
		Npc lich = harness.Spawn(Macunbello, 310f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(marker, lich);

		marker.GetAi().OnGeneralEvent(AiEventType.Spawned);

		Assert.Empty(Live(harness, Macunbello));
		Assert.False(marker.IsSpawned(), "the marker was still standing after it called");
	}

	/// <summary>
	/// <b>And only within fifty metres</b>, which is retail's range on both branches — so a lich in the
	/// next room is not cleared by a marker it never saw.
	/// </summary>
	[Fact]
	public void AndOnlyWithinFiftyMetres()
	{
		using BossAiHarness harness = NewHarness();
		Npc marker = harness.Spawn(Marker, 300f, 300f, 200f);
		Npc near = harness.Spawn(Macunbello, 310f, 300f, 200f);
		Npc distant = harness.Spawn(Macunbello, 400f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(marker, near);
		BossAiHarness.MakeMutuallyKnown(marker, distant);

		marker.GetAi().OnGeneralEvent(AiEventType.Spawned);

		Assert.Single(Live(harness, Macunbello));
		Assert.True(distant.IsSpawned(), "the far lich was cleared from out of range");
	}

	/// <summary>
	/// <b>The message number is retail's, not ours.</b> Marker and lich share one constant, so nothing
	/// else here would notice it changing.
	/// </summary>
	[Fact]
	public void TheMessageNumberIsRetails()
	{
		Assert.Equal(6981, DecoyLichMarkerAI.TheRealOneIsHere);
	}
}
