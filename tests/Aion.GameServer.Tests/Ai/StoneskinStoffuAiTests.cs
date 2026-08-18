using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the stoneskin stoffu, translated from retail patterns <c>D2_SouST_Su</c> and
/// <c>D2_FnG_D1</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class StoneskinStoffuAiTests
{
	private const int Morheim = 220020000;

	private const int Stoffu = 210617;
	private const int Fragment = 280100;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Morheim).WithWorldSize(2048)
			.WithAi(typeof(StoneskinStoffuAI), typeof(AngolemFragmentAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	private static (BossAiHarness, Npc, Player) Fight()
	{
		BossAiHarness harness = NewHarness();
		Npc stoffu = harness.Spawn(Stoffu, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ASMODIANS);
		harness.Engage(stoffu, raider);
		return (harness, stoffu, raider);
	}

	/// <summary>
	/// <b>It sheds a fragment in each band, once.</b> Retail writes two bands with a flag var apiece,
	/// so a stoffu worn down slowly drops two pieces and no more.
	/// </summary>
	[Fact]
	public void ItShedsAFragmentInEachBandOnce()
	{
		var (harness, stoffu, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(stoffu, 50);
		for (int i = 0; i < 3; i++)
			stoffu.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);
		Assert.Single(Live(harness, Fragment));

		BossAiHarness.SetExactPercent(stoffu, 30);
		for (int i = 0; i < 3; i++)
			stoffu.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);

		Assert.Equal(2, Live(harness, Fragment).Count);
	}

	/// <summary>
	/// <b>Three seconds later it points the fragment at its target, and the fragment commits.</b> The
	/// delay is the mechanic: a piece that arrived already fighting would be an add, and three seconds
	/// of it standing inert is a window to kill it in.
	/// </summary>
	[Fact]
	public void ThreeSecondsLaterItPointsTheFragment()
	{
		var (harness, stoffu, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(stoffu, 30);
		stoffu.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);

		Npc fragment = Assert.Single(Live(harness, Fragment));
		Assert.Equal(0, fragment.GetAggroList().GetHate(raider));

		harness.Clock.Advance(TimeSpan.FromSeconds(4));

		// A hundred and one, not a hundred: retail's point_to_add is a hundred and one more arrives
		// when the fragment actually starts swinging. Same reading as the corask clodworms, pinned as
		// read rather than rounded to retail's figure.
		Assert.Equal(101, fragment.GetAggroList().GetHate(raider));
		Assert.Same(raider, fragment.GetTarget());
	}

	/// <summary>
	/// <b>A band pays out once whether the stoffu was hit or cast at.</b> Retail puts the same flag var
	/// on both provocations, so a melee blow after a spell in the same band adds nothing.
	/// </summary>
	[Fact]
	public void ABandPaysOutOnceHoweverItWasProvoked()
	{
		var (harness, stoffu, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(stoffu, 30);
		stoffu.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);
		Assert.Single(Live(harness, Fragment));

		stoffu.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Single(Live(harness, Fragment));
	}

	/// <summary>
	/// <b>Inside the upper band it sheds one piece and no more, however long it is worked.</b> The
	/// lower band opens at thirty-five, not wherever the fight happens to be — and a band whose floor
	/// crept upwards would pay twice here, once per flag, while every other pin read the same totals.
	/// </summary>
	[Fact]
	public void InsideTheUpperBandItShedsOnePieceAndNoMore()
	{
		var (harness, stoffu, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(stoffu, 40);
		for (int i = 0; i < 4; i++)
			stoffu.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);

		Assert.Single(Live(harness, Fragment));
	}

	/// <summary>
	/// <b>Above sixty-five percent it sheds nothing.</b> The upper band starts at sixty-five, so the
	/// opening of the fight is a plain one.
	/// </summary>
	[Fact]
	public void AboveSixtyFivePercentItShedsNothing()
	{
		var (harness, stoffu, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(stoffu, 80);
		for (int i = 0; i < 5; i++)
			stoffu.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);
		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.Empty(Live(harness, Fragment));
	}

	/// <summary>
	/// <b>And it takes the pieces with it.</b> Retail clears the group on dying and on leaving the
	/// fight, so a fragment never outlives what shed it.
	/// </summary>
	[Fact]
	public void AndItTakesThePiecesWithIt()
	{
		var (harness, stoffu, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(stoffu, 30);
		stoffu.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);
		Assert.Single(Live(harness, Fragment));

		stoffu.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Empty(Live(harness, Fragment));
	}
}
