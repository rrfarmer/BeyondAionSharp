using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Handlers.Instance;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The gossip npc each Tiamat Stronghold brigade general leaves when he dies.
/// </summary>
/// <remarks>
/// Retail names one per boss in his own <c>on_die</c>: Sardha's is 283178, Rakshaka's 283179 and
/// Tahabata's 283180, each for fifteen seconds.
/// <para>
/// <b>All three were rotated by one.</b> Terath dropped Rakshaka's, Laksyaka dropped Tahabata's and
/// Tahabata dropped Sardha's — <b>at the right coordinates every time</b>, which is what made it
/// invisible: the npc appeared exactly where it should, wearing the wrong name. Every one of the three
/// call sites carried an <c>// ex 2839xx</c> comment from an earlier renumbering.
/// </para>
/// <para>
/// Pinned as a table rather than through a fight, because instance handlers have no harness. That is
/// weaker than a behavioural pin and worth saying: it fixes the mapping in place, and it would not
/// notice if the call sites stopped using the table.
/// </para>
/// </remarks>
public sealed class TiamatStrongholdGossipTests
{
	private const int TiamatStronghold = 300510000;


	private const int Terath = 219354;
	private const int Laksyaka = 219356;
	private const int Tahabata = 219358;

	[Theory]
	[InlineData(Terath, 283178)]
	[InlineData(Laksyaka, 283179)]
	[InlineData(Tahabata, 283180)]
	public void EachGeneralLeavesHisOwnGossipNpc(int boss, int gossip)
	{
		Assert.Equal(gossip, TiamatStrongHoldInstance.GossipOnDie[boss]);
	}

	/// <summary>
	/// <b>And no two share one.</b> A rotation keeps all three distinct, so counting them is what
	/// separates "rotated" from "wrong in one place".
	/// </summary>
	[Fact]
	public void TheThreeAreDistinct()
	{
		Assert.Equal(3, TiamatStrongHoldInstance.GossipOnDie.Values.Distinct().Count());
	}

	/// <summary>
	/// <b>Each gossip npc stands for retail's own lifetime</b> — fifteen seconds, and Tahabata's ten.
	/// </summary>
	/// <remarks>
	/// The decay this port scheduled at the spawn site never had any effect: the npc's AI deleted it
	/// after five seconds and the shorter clock wins. It was wrong besides — all three were given
	/// fifteen, and Tahabata's is ten. Found by <c>audit_lifetime_conflicts.py</c>, in work from this
	/// same session.
	/// </remarks>
	[Theory]
	[InlineData(283178, 15)]
	[InlineData(283179, 15)]
	[InlineData(283180, 10)]
	public void EachGossipNpcKeepsItsOwnLifetime(int gossip, int seconds)
	{
		using BossAiHarness harness = BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(TiamatEyeAI), typeof(GeneralNpcAI)).Build();
		harness.Spawn(gossip, 1030f, 301f, 411f);

		harness.Clock.Advance(TimeSpan.FromSeconds(seconds - 1));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == gossip));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == gossip));
	}
}
