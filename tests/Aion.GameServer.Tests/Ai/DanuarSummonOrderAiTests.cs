using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="DanuarSummonOrderAI"/>, translated from retail patterns
/// <c>Rune_FrostNmd_TankSum2_65_Ae</c>, <c>Rune_FrostNmd_DealSum2_65_Ae</c> and
/// <c>Rune_FrostNmd_MezSum2_65_Ae</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Queen Modor was placing her pillar trio without the order that comes with them. The mechanic is
/// not that she summons three adds — it is that she summons them <em>onto a named player</em>.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DanuarSummonOrderAiTests
{
	/// <summary>Danuar Reliquary.</summary>
	private const int DanuarReliquary = 301220000;

	private const int Bodyguard = 284380;
	private const int VengefulReaper = 284381;
	private const int AcheronDrake = 284382;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DanuarReliquary).WithWorldSize(2048)
			.WithAi(typeof(DanuarSummonOrderAI), typeof(AggressiveNpcAI))
			.Build();

	/// <summary>One summon, standing where Modor puts it, with a stand-in for the sender.</summary>
	private static (BossAiHarness, Npc, Npc, Player) Called(int npcId)
	{
		BossAiHarness harness = NewHarness();
		Npc queen = harness.Spawn(214870, 260f, 270f, 241f);
		Npc summon = harness.Spawn(npcId, 262f, 272f, 241f);
		Player named = harness.SpawnPlayer(266f, 274f, 241f);
		BossAiHarness.MakeMutuallyKnown(queen, summon);
		BossAiHarness.MakeMutuallyKnown(summon, named);
		return (harness, queen, summon, named);
	}

	/// <summary>
	/// <b>The order assigns them.</b> A summon that has just appeared holds no hate at all, so the one
	/// point retail's <c>add_hate_point</c> carries is enough to make the named player the most-hated —
	/// which is what turns three adds into three adds on somebody.
	/// </summary>
	[Theory]
	[InlineData(Bodyguard)]
	[InlineData(VengefulReaper)]
	[InlineData(AcheronDrake)]
	public void AFreshSummonTakesTheNamedPlayer(int npcId)
	{
		var (harness, queen, summon, named) = Called(npcId);
		using BossAiHarness _h = harness;

		Assert.Equal(0, summon.GetAggroList().GetHate(named));

		NpcMessageBus.Broadcast(queen, DanuarSummonOrderAI.OrderMessage, named, 50f);

		Assert.Equal(1, summon.GetAggroList().GetHate(named));
		Assert.Same(named, summon.GetTarget());
	}

	/// <summary>
	/// <b>One point, not a switch.</b> Retail adds a single hate point and then attacks whoever is
	/// most-hated — so a summon already holding real hate on somebody else stays on them and the order
	/// does nothing. Collapsing the pair into "switch to the named player" would be a different and
	/// much stronger mechanic.
	/// </summary>
	[Fact]
	public void ASummonAlreadyFightingSomeoneElseIgnoresTheOrder()
	{
		var (harness, queen, summon, named) = Called(Bodyguard);
		using BossAiHarness _h = harness;

		// A second player, not an NPC: the aggro list only offers a valid *enemy* as most-hated, and
		// two NPCs of one tribe are not enemies — an NPC stand-in here reads as "no hate at all" and
		// the pin passes for the wrong reason.
		Player busyWith = harness.SpawnPlayer(258f, 268f, 241f);
		BossAiHarness.MakeMutuallyKnown(summon, busyWith);
		summon.GetAggroList().AddHate(busyWith, 500);

		NpcMessageBus.Broadcast(queen, DanuarSummonOrderAI.OrderMessage, named, 50f);

		Assert.Equal(1, summon.GetAggroList().GetHate(named));
		Assert.Same(busyWith, summon.GetTarget());
	}

	/// <summary>
	/// It answers 444 and nothing else. Message numbers are chosen per encounter with no registry, and
	/// every other pin here passes just as well if the guard is dropped.
	/// </summary>
	[Fact]
	public void ASummonAnswersOnlyItsOwnMessage()
	{
		var (harness, queen, summon, named) = Called(Bodyguard);
		using BossAiHarness _h = harness;

		NpcMessageBus.Broadcast(queen, DanuarSummonOrderAI.OrderMessage + 1, named, 50f);
		Assert.Equal(0, summon.GetAggroList().GetHate(named));

		NpcMessageBus.Broadcast(queen, DanuarSummonOrderAI.OrderMessage, named, 50f);
		Assert.Equal(1, summon.GetAggroList().GetHate(named));
	}

	/// <summary>
	/// Fifty metres, which is retail's <c>range_as_meter</c>. Stated on its own because an order that
	/// reaches the whole room reads as working and is not what she sends.
	/// </summary>
	[Fact]
	public void TheOrderReachesFiftyMetresAndNoFurther()
	{
		using BossAiHarness harness = NewHarness();
		Npc queen = harness.Spawn(214870, 260f, 270f, 241f);
		Npc near = harness.Spawn(Bodyguard, 290f, 270f, 241f);   // 30m
		Npc far = harness.Spawn(Bodyguard, 400f, 270f, 241f);    // 140m
		Player named = harness.SpawnPlayer(262f, 272f, 241f);
		foreach (Npc summon in new[] { near, far })
		{
			BossAiHarness.MakeMutuallyKnown(queen, summon);
			BossAiHarness.MakeMutuallyKnown(summon, named);
		}

		NpcMessageBus.Broadcast(queen, DanuarSummonOrderAI.OrderMessage, named, 50f);

		Assert.Equal(1, near.GetAggroList().GetHate(named));
		Assert.Equal(0, far.GetAggroList().GetHate(named));
	}

	/// <summary>
	/// And the range the sender actually uses is that fifty, not a number of its own. Pinned against a
	/// literal because the behavioural pin above passes for any range the sender and the assertion
	/// happen to share — widening the order to the whole room survived a mutation sweep until this
	/// existed.
	/// </summary>
	[Fact]
	public void TheOrdersRangeIsRetailsFifty()
	{
		Assert.Equal(50f, DanuarSummonOrderAI.OrderRange);
	}

	/// <summary>A dead player cannot be named, and the order is dropped rather than half-applied.</summary>
	[Fact]
	public void AnOrderWithNobodyNamedDoesNothing()
	{
		var (harness, queen, summon, _) = Called(Bodyguard);
		using BossAiHarness _h = harness;

		NpcMessageBus.Broadcast(queen, DanuarSummonOrderAI.OrderMessage, null, 50f);

		Assert.Null(summon.GetTarget());
	}
}
