using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The smoke a Vasharti assassin appears out of, which for a long time appeared out of nothing.
/// </summary>
/// <remarks>
/// Retail <c>IDElemental_Smoke</c> stands for six seconds. This class spawned it and <b>deleted it on the
/// very next line</b>, so the effect existed for no time at all.
/// <para>
/// <b>That is why no audit found it.</b> Every check this log built asks whether a spawn is bounded, and
/// this one was — bounded to zero. It came out only when the row was read by hand to be dismissed, and it
/// is the one case in the whole lifetime sweep where the bug was an add that was <i>too</i> short-lived
/// rather than too long.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class VashartiAssassinAiTests
{
	private const int RentusBase = 300280000;
	private const int Assassin = 236287;
	private const int Smoke = 282465;

	private static (BossAiHarness, Npc) Aggroed()
	{
		BossAiHarness harness = BossAiHarness.For(RentusBase).WithWorldSize(2048)
			.WithAi(typeof(VashartiAssassinAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc assassin = harness.Spawn(Assassin, 535f, 475f, 145.8f);
		Player player = harness.SpawnPlayer(537f, 477f, 145.8f);
		BossAiHarness.MakeMutuallyKnown(assassin, player);
		harness.Engage(assassin, player);
		return (harness, assassin);
	}

	private static int Smokes(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Smoke);

	/// <summary>
	/// <b>He arrives in a puff of smoke.</b> Before the fix the smoke was removed on the line after it was
	/// placed, so nobody ever saw it.
	/// </summary>
	[Fact]
	public void HeArrivesInSmoke()
	{
		var (harness, _) = Aggroed();
		using BossAiHarness _h = harness;

		Assert.Equal(1, Smokes(harness));
	}

	/// <summary>
	/// <b>And it hangs for six seconds.</b> Five against seven is the window: a bound of zero — what this
	/// class had — fails the first half, and no bound at all fails the second.
	/// </summary>
	[Fact]
	public void TheSmokeHangsForSixSeconds()
	{
		var (harness, _) = Aggroed();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.Equal(1, Smokes(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Smokes(harness));
	}

	/// <summary>
	/// <b>And only once.</b> Retail latches this on leaving home, so a second aggro during the same pull
	/// does not stack another cloud.
	/// </summary>
	[Fact]
	public void TheSmokeIsPlacedOncePerPull()
	{
		var (harness, assassin) = Aggroed();
		using BossAiHarness _h = harness;
		Player other = harness.SpawnPlayer(539f, 479f, 145.8f);
		BossAiHarness.MakeMutuallyKnown(assassin, other);

		harness.Engage(assassin, other);

		Assert.Equal(1, Smokes(harness));
	}
}
