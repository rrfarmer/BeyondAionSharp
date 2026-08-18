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
/// Pins for the lobseks, translated from retail pattern <c>ND2_Xipeto_45</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class LobsekAiTests
{
	private const int Beluslan = 220030000;

	private const int CoastalLobsek = 214215;
	private const int SeaLobsek = 214216;
	private const int StrangeObject = 280934;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(LobsekAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	private static (BossAiHarness, Npc, Player) Fight(int lobsekId = CoastalLobsek)
	{
		BossAiHarness harness = NewHarness();
		Npc lobsek = harness.Spawn(lobsekId, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f, race: Race.ASMODIANS);
		harness.Engage(lobsek, raider);
		return (harness, lobsek, raider);
	}

	/// <summary>
	/// <b>Below half health it drops a strange object, once.</b> The third instance in three entries of
	/// retail's "sheds a piece when hurt" idiom.
	/// </summary>
	[Theory]
	[InlineData(CoastalLobsek)]
	[InlineData(SeaLobsek)]
	public void BelowHalfItDropsAStrangeObjectOnce(int lobsekId)
	{
		var (harness, lobsek, raider) = Fight(lobsekId);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(lobsek, 60);
		lobsek.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Empty(Live(harness, StrangeObject));

		BossAiHarness.SetExactPercent(lobsek, 40);
		for (int i = 0; i < 3; i++)
			lobsek.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Single(Live(harness, StrangeObject));
	}

	/// <summary>
	/// <b>A spell drops it too, and the flag is shared.</b> Retail writes the branch on both handlers
	/// with one <c>FLAGVARI_ALPHA_1</c> across them.
	/// </summary>
	[Fact]
	public void ASpellDropsItTooAndTheFlagIsShared()
	{
		var (harness, lobsek, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(lobsek, 40);
		lobsek.GetAi().OnCreatureEvent(AiEventType.Spelled, raider);
		Assert.Single(Live(harness, StrangeObject));

		lobsek.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		Assert.Single(Live(harness, StrangeObject));
	}

	/// <summary>
	/// <b>It lasts a minute.</b> Sixty seconds against the stoffu's six minutes and the lich's fifty —
	/// a nuisance with a clock rather than an add.
	/// </summary>
	[Fact]
	public void ItLastsAMinute()
	{
		var (harness, lobsek, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(lobsek, 40);
		lobsek.GetAi().OnCreatureEvent(AiEventType.Attack, raider);

		harness.Clock.Advance(TimeSpan.FromSeconds(50));
		Assert.Single(Live(harness, StrangeObject));

		harness.Clock.Advance(TimeSpan.FromSeconds(20));

		Assert.Empty(Live(harness, StrangeObject));
	}

	/// <summary>
	/// <b>And a dead lobsek takes it with it.</b> Retail clears the group on both death branches.
	/// </summary>
	[Fact]
	public void ADeadLobsekTakesItWithIt()
	{
		var (harness, lobsek, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(lobsek, 40);
		lobsek.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Single(Live(harness, StrangeObject));

		lobsek.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Empty(Live(harness, StrangeObject));
	}

	/// <summary>
	/// <b>But going home does not.</b> Retail has no leave-combat branch here, unlike the stoneskin
	/// stoffu — a lobsek that disengages leaves its object standing for the rest of its minute.
	/// Translated as written, and pinned so a later tidy-up has to argue with retail rather than with
	/// nothing.
	/// </summary>
	[Fact]
	public void ButGoingHomeDoesNot()
	{
		var (harness, lobsek, raider) = Fight();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(lobsek, 40);
		lobsek.GetAi().OnCreatureEvent(AiEventType.Attack, raider);
		Assert.Single(Live(harness, StrangeObject));

		lobsek.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Single(Live(harness, StrangeObject));
	}
}
