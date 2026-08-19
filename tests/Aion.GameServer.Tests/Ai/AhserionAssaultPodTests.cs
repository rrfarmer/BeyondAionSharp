using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Ahserion assault pods and the wave each lands.
/// </summary>
/// <remarks>
/// Retail drops both pods' troopers with <c>SPAWN_LOCATION_RELATIVE</c> offsets. The strike pod
/// (297352) puts all three at <c>z=3</c>; the TBM pod (297353) puts two at ground level and one at
/// <c>z=2</c>. This port had the TBM pod exactly right and the strike pod's three at 0, 0.1 and 0 — the
/// wave arrived at the pod's feet rather than three metres above it.
/// <para>
/// Which is why the TBM pod is pinned here too: it is the control. Had both been wrong the zeroes would
/// have looked like this port's convention rather than a mistake in one of them.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AhserionAssaultPodTests
{
	private const int AhserionsFlight = 400030000;

	private const int StrikePod = 297352;
	private const int TbmPod = 297353;
	private const int Assassin = 297191;
	private const int Sorcerer = 297192;
	private const int DefenderCaptain = 297190;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AhserionsFlight).WithWorldSize(2048)
			.WithAi(typeof(AhserionSkyAssaulterAI), typeof(AhserionAggressiveNpcAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	private static List<Npc> WaveFrom(BossAiHarness harness, int podId, float z)
	{
		harness.Spawn(podId, 300f, 300f, z);
		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		return harness.LiveNpcs()
			.Where(n => n.GetNpcId() is Assassin or Sorcerer or DefenderCaptain).ToList();
	}

	/// <summary>
	/// <b>The strike pod's wave lands three metres up.</b>
	/// </summary>
	[Fact]
	public void TheStrikePodsWaveLandsThreeMetresUp()
	{
		using BossAiHarness harness = NewHarness();

		List<Npc> wave = WaveFrom(harness, StrikePod, 200f);

		// 203, written out: asserting against StrikeWaveUp puts the constant on both sides of the
		// comparison, so setting it to zero passed. Same shape as the Bakarma count pin.
		Assert.Equal(3, wave.Count);
		Assert.All(wave, t => Assert.Equal(203f, t.GetZ(), 2));
	}

	/// <summary>
	/// <b>And the TBM pod's does not — two at its own height and one at two.</b>
	/// </summary>
	/// <remarks>
	/// The control. This pod already matched retail, so a fix that raised every wave would break it.
	/// </remarks>
	[Fact]
	public void AndTheTbmPodsDoesNot()
	{
		using BossAiHarness harness = NewHarness();

		List<Npc> wave = WaveFrom(harness, TbmPod, 200f);

		Assert.Equal(3, wave.Count);
		Assert.Equal(2, wave.Count(t => Math.Abs(t.GetZ() - 200f) < 0.01f));
		Assert.Equal(1, wave.Count(t => Math.Abs(t.GetZ() - 202f) < 0.01f));
	}
}
