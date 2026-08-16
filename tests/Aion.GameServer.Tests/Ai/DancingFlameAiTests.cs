using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="DancingFlameAI"/>, translated from retail patterns
/// <c>IDYun_Vasharti_Fire_Red</c>, <c>_Blue</c> and <c>IDYun_Vasharti_Fire_SkillLauncher</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// All four NPCs share the <c>ai_name</c> and the class treated them as one thing. What is pinned is
/// the split: a flame throws launchers, a launcher is what casts, and each colour throws its own.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DancingFlameAiTests
{
	private const int RentusBase = 300280000;

	private const int RedFlame = 282996;
	private const int BlueFlame = 282997;
	private const int RedLauncher = 282998;
	private const int BlueLauncher = 282999;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(RentusBase).WithWorldSize(2048)
			.WithAi(typeof(DancingFlameAI), typeof(AggressiveNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>The red flame throws a red launcher, and it takes three seconds to do it.</summary>
	[Fact]
	public void TheRedFlameThrowsARedLauncherEveryThreeSeconds()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(RedFlame, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, RedLauncher));

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(1, Count(harness, RedLauncher));
	}

	/// <summary>
	/// And the blue flame throws a <b>blue</b> one. It was reachable by nobody before this: the class
	/// picked its skill with "is this the red launcher, or anything else", and nothing ever created
	/// the blue.
	/// </summary>
	[Fact]
	public void TheBlueFlameThrowsABlueLauncher()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(BlueFlame, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));

		Assert.Equal(1, Count(harness, BlueLauncher));
		Assert.Equal(0, Count(harness, RedLauncher));
	}

	/// <summary>A launcher lives two seconds, so it is gone before the next one is thrown.</summary>
	[Fact]
	public void ALauncherLivesTwoSeconds()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(RedFlame, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(1, Count(harness, RedLauncher));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, RedLauncher));
	}

	/// <summary>It keeps throwing, so the flame is a heartbeat rather than a one-off.</summary>
	[Fact]
	public void TheFlameKeepsThrowing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(RedFlame, 300f, 300f, 200f);

		// Three intervals: one at 3, one at 6, one at 9, each gone two seconds later.
		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(1, Count(harness, RedLauncher));

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(1, Count(harness, RedLauncher));

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(1, Count(harness, RedLauncher));
	}

	/// <summary>A launcher does not throw launchers of its own — only a flame does.</summary>
	[Fact]
	public void ALauncherThrowsNothing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(RedLauncher, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(10));

		Assert.Equal(0, Count(harness, BlueLauncher));
		Assert.Equal(1, Count(harness, RedLauncher));
	}

	/// <summary>
	/// It throws whether or not anyone is standing in it. The ten-metre check the class used to make
	/// was ours; retail's launcher casts unconditionally as it appears.
	/// </summary>
	[Fact]
	public void ItThrowsWithNobodyStandingThere()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(RedFlame, 300f, 300f, 200f);
		harness.SpawnPlayer(900f, 900f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));

		Assert.Equal(1, Count(harness, RedLauncher));
	}
}
