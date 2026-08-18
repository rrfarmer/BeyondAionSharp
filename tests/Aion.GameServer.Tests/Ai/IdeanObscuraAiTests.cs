using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="IdeanObscuraAI"/> and the call <see cref="CursedQueenModorAI"/> now raises,
/// translated from retail patterns <c>Rune_FrostNmd_MezSum_65_Ae</c> and <c>Rune_FrostNmd_N_65_Ah</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The player is kept out of the obscura's known list, so the call is the only way it can reach them —
/// the lesson from the abyss guard pins, applied from the start.
/// <para>
/// <b>The latch is not re-pinned here.</b> A version of this file asserted that an obscura arriving
/// after the pull hears nothing, and it failed: introducing a new NPC to a boss that is already
/// fighting is enough for our engine's own see-a-friend-attacked to bring it in, with no message
/// involved. <see cref="CombatAlarm"/>'s once-a-fight behaviour is pinned where it belongs, in the
/// Sauro Supply Base alarm tests, against guards that are known to their boss before the pull.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class IdeanObscuraAiTests
{
	private const int OphidanBridge = 300590000;

	private const int Modor = 234690;
	private const int Obscura = 284379;
	private const int WeakenedObscura = 284661;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(OphidanBridge).WithWorldSize(2048)
			.WithAi(typeof(IdeanObscuraAI), typeof(CursedQueenModorAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary><b>Modor's call brings her obscura onto the player she is fighting.</b></summary>
	[Fact]
	public void ModorsCallBringsHerObscura()
	{
		using BossAiHarness harness = NewHarness();
		Npc modor = harness.Spawn(Modor, 256f, 258f, 242f);
		Npc obscura = harness.Spawn(Obscura, 276f, 258f, 242f);
		Npc weakened = harness.Spawn(WeakenedObscura, 277f, 258f, 242f);
		Player player = harness.SpawnPlayer(256f, 218f, 242f);
		BossAiHarness.MakeMutuallyKnown(modor, obscura);
		BossAiHarness.MakeMutuallyKnown(modor, weakened);
		Assert.Null(obscura.GetTarget());

		harness.Engage(modor, player);

		Assert.Same(player, obscura.GetTarget());
		Assert.Same(player, weakened.GetTarget());
	}

	/// <summary><b>And only within fifty metres of her.</b></summary>
	[Fact]
	public void AndOnlyWithinFiftyMetres()
	{
		using BossAiHarness harness = NewHarness();
		Npc modor = harness.Spawn(Modor, 256f, 258f, 242f);
		Npc distant = harness.Spawn(Obscura, 336f, 258f, 242f);
		Player player = harness.SpawnPlayer(256f, 218f, 242f);
		BossAiHarness.MakeMutuallyKnown(modor, distant);

		harness.Engage(modor, player);

		Assert.Null(distant.GetTarget());
	}

	/// <summary>And it re-arms when she goes home, so a second pull calls them again.</summary>
	[Fact]
	public void AndItReArmsWhenSheGoesHome()
	{
		using BossAiHarness harness = NewHarness();
		Npc modor = harness.Spawn(Modor, 256f, 258f, 242f);
		Npc obscura = harness.Spawn(Obscura, 276f, 258f, 242f);
		Player player = harness.SpawnPlayer(256f, 218f, 242f);
		BossAiHarness.MakeMutuallyKnown(modor, obscura);
		harness.Engage(modor, player);

		modor.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Npc latecomer = harness.Spawn(WeakenedObscura, 277f, 258f, 242f);
		BossAiHarness.MakeMutuallyKnown(modor, latecomer);
		harness.Engage(modor, player);

		Assert.Same(player, latecomer.GetTarget());
	}

	/// <summary>
	/// <b>Below half an obscura turns onto a random attacker, two blows in five.</b> Read over many
	/// blows: retail's own guard is a forty-percent roll, and the flag makes it a once-a-fight event.
	/// </summary>
	[Fact]
	public void BelowHalfItTurnsOntoSomebodyAtRandom()
	{
		bool turned = false;

		for (int run = 0; run < 10 && !turned; run++)
		{
			using BossAiHarness harness = NewHarness();
			Npc obscura = harness.Spawn(Obscura, 300f, 300f, 200f);
			var raid = new List<Player>();
			for (int i = 0; i < 4; i++)
				raid.Add(harness.SpawnPlayer(304f + i, 300f, 200f));

			harness.Engage(obscura, raid[0]);
			for (int i = 0; i < raid.Count; i++)
				for (int n = raid.Count - i; n > 0; n--)
					BossAiHarness.Rehate(obscura, raid[i]);

			BossAiHarness.SetExactPercent(obscura, 40);
			for (int i = 0; i < 10 && !turned; i++)
			{
				obscura.GetAi().OnCreatureEvent(AiEventType.Attack, raid[0]);
				turned = !ReferenceEquals(obscura.GetTarget(), raid[0]);
			}
		}

		Assert.True(turned, "ten fights and it never turned below half");
	}

	/// <summary>And above half no blow turns it at all, over eight fights and forty blows apiece.</summary>
	/// <remarks>
	/// Eight fights because the turn it must not make is a random one over four players, so a single
	/// fight in which the mutation turns it back onto the same player proves nothing.
	/// </remarks>
	[Fact]
	public void AboveHalfNoBlowTurnsIt()
	{
		for (int run = 0; run < 8; run++)
		{
			using BossAiHarness harness = NewHarness();
			Npc obscura = harness.Spawn(Obscura, 300f, 300f, 200f);
			var raid = new List<Player>();
			for (int i = 0; i < 4; i++)
				raid.Add(harness.SpawnPlayer(304f + i, 300f, 200f));

			harness.Engage(obscura, raid[0]);
			for (int i = 0; i < raid.Count; i++)
				for (int n = raid.Count - i; n > 0; n--)
					BossAiHarness.Rehate(obscura, raid[i]);

			BossAiHarness.SetExactPercent(obscura, 80);
			for (int i = 0; i < 40; i++)
				obscura.GetAi().OnCreatureEvent(AiEventType.Attack, raid[0]);

			Assert.Same(raid[0], obscura.GetTarget());
		}
	}

	/// <summary>
	/// <b>And below half it turns once and no more.</b> Retail's flag var makes this a once-a-fight
	/// event, so forty blows produce one turn rather than sixteen.
	/// </summary>
	/// <remarks>
	/// Counted as turns away from whoever it was on, with the obscura put back on the tank after each
	/// one so a second turn would be visible. Without the flag the forty-percent roll fires again and
	/// again, which is what this catches.
	/// <para>
	/// At most one rather than exactly one: the single turn retail allows is a random pick over four
	/// players and lands back on the tank a quarter of the time, so demanding exactly one fails one run
	/// in four for no reason anybody would want to read about.
	/// </para>
	/// </remarks>
	[Fact]
	public void BelowHalfItTurnsOnceAndNoMore()
	{
		using BossAiHarness harness = NewHarness();
		Npc obscura = harness.Spawn(Obscura, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < 4; i++)
			raid.Add(harness.SpawnPlayer(304f + i, 300f, 200f));

		harness.Engage(obscura, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(obscura, raid[i]);

		BossAiHarness.SetExactPercent(obscura, 40);

		int turns = 0;
		for (int i = 0; i < 60; i++)
		{
			obscura.SetTarget(raid[0]);
			obscura.GetAi().OnCreatureEvent(AiEventType.Attack, raid[0]);
			if (!ReferenceEquals(obscura.GetTarget(), raid[0]))
				turns++;
		}

		Assert.True(turns <= 1, $"it turned {turns} times in sixty blows");
	}
}
