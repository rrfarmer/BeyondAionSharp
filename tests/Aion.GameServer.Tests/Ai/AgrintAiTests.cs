using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="AgrintAI"/>, translated from retail patterns <c>HLFP_Agrint*</c> and
/// <c>HDFP_Agrint*</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Eight agrints, eight patterns, one mechanic — so what is pinned is the mechanic and the two
/// faction offsets, not each season.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AgrintAiTests
{
	/// <summary>The Elyos housing field.</summary>
	private const int Elysea = 700010000;

	private const int SpringAgrint = 218850;
	private const int WinterAgrint = 218853;
	private const int AsmodianSpringAgrint = 218862;

	private const int SpringUnderling = 219170;
	private const int WinterUnderling = 219173;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Elysea).WithWorldSize(2048)
			.WithAi(typeof(AgrintAI), typeof(AggressiveNpcAI), typeof(OneDmgAI))
			.Build();

	private static (BossAiHarness, Npc, Player) Engaged(int npcId)
	{
		BossAiHarness harness = NewHarness();
		Npc agrint = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(303f, 300f, 200f);
		harness.Engage(agrint, player);
		return (harness, agrint, player);
	}

	private static void Advance(BossAiHarness harness, Npc agrint, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(agrint, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Both factions' agrints reach the same four underlings, by different offsets.</summary>
	[Fact]
	public void EachSeasonReachesItsOwnUnderling()
	{
		Assert.Equal(SpringUnderling, AgrintAI.UnderlingFor(SpringAgrint));
		Assert.Equal(WinterUnderling, AgrintAI.UnderlingFor(WinterAgrint));
		Assert.Equal(SpringUnderling, AgrintAI.UnderlingFor(AsmodianSpringAgrint));
		Assert.Equal(0, AgrintAI.UnderlingFor(123456));
	}

	/// <summary>
	/// The underlings come <b>thirty seconds into the fight</b>, not when the agrint is half dead.
	/// </summary>
	[Fact]
	public void FiveUnderlingsComeThirtySecondsIn()
	{
		var (harness, agrint, player) = Engaged(SpringAgrint);
		using BossAiHarness _h = harness;

		Advance(harness, agrint, player, 29);
		Assert.Equal(0, Count(harness, SpringUnderling));

		Advance(harness, agrint, player, 2);
		Assert.Equal(5, Count(harness, SpringUnderling));
	}

	/// <summary>They come regardless of health — the fifty-percent trigger was ours.</summary>
	[Fact]
	public void TheyComeAtFullHealth()
	{
		var (harness, agrint, player) = Engaged(SpringAgrint);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(agrint, 100);
		Advance(harness, agrint, player, 31);

		Assert.Equal(5, Count(harness, SpringUnderling));
	}

	/// <summary>Each lives twenty seconds, so a wave is a squall rather than a standing guard.</summary>
	[Fact]
	public void AnUnderlingLastsTwentySeconds()
	{
		var (harness, agrint, player) = Engaged(SpringAgrint);
		using BossAiHarness _h = harness;

		Advance(harness, agrint, player, 31);
		Assert.Equal(5, Count(harness, SpringUnderling));

		Advance(harness, agrint, player, 18);
		Assert.Equal(5, Count(harness, SpringUnderling));

		Advance(harness, agrint, player, 3);
		Assert.Equal(0, Count(harness, SpringUnderling));
	}

	/// <summary>And the next wave is two hundred seconds after the first, not on the next threshold.</summary>
	[Fact]
	public void TheNextWaveIsTwoHundredSecondsLater()
	{
		var (harness, agrint, player) = Engaged(SpringAgrint);
		using BossAiHarness _h = harness;

		Advance(harness, agrint, player, 31);
		Assert.Equal(5, Count(harness, SpringUnderling));

		// Gone at fifty, and nothing until the timer comes round at two hundred and thirty. Checked
		// halfway as well: a hundred-second interval would have laid a second wave at 130, which is
		// still standing at 140, and this window is where the two cadences disagree.
		Advance(harness, agrint, player, 109);
		Assert.Equal(0, Count(harness, SpringUnderling));

		Advance(harness, agrint, player, 89);
		Assert.Equal(0, Count(harness, SpringUnderling));

		Advance(harness, agrint, player, 2);
		Assert.Equal(5, Count(harness, SpringUnderling));
	}

	/// <summary>An agrint nobody has touched calls nobody.</summary>
	[Fact]
	public void AnUntouchedAgrintCallsNobody()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(SpringAgrint, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(120));

		Assert.Equal(0, Count(harness, SpringUnderling));
	}
}
