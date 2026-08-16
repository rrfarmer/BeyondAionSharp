using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="AbyssUndeadAI"/>, translated from retail patterns <c>AD2_UnDead*_Da</c> and
/// <c>AD2_UnDead*_Li</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Twenty-one spawned world npcs on plain <c>aggressive</c>, and one mechanic between them: killing one
/// is a coin flip that leaves a fear standing on whoever did it. The shape worth pinning is <em>on
/// whoever did it</em> — not near the corpse, and not on the tank.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AbyssUndeadAiTests
{
	/// <summary>The lower Abyss, where they stand.</summary>
	private const int Abyss = 400010000;

	private const int ImmortalWarrior = 252518;
	private const int EternalPriest = 253026;
	private const int Fear = 290137;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Abyss).WithWorldSize(4096)
			.WithAi(typeof(AbyssUndeadAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Fear);

	/// <summary>
	/// Kills the undead twenty times over and reports how many left a fear. A coin flip cannot be
	/// pinned by one death, and twenty is enough to separate "half the time" from "never" or "always".
	/// </summary>
	private static int DeathsThatLeftAFear(int npcId, int deaths = 20)
	{
		int left = 0;

		for (int i = 0; i < deaths; i++)
		{
			using BossAiHarness harness = NewHarness();
			Npc undead = harness.Spawn(npcId, 300f, 300f, 200f);
			Player killer = harness.SpawnPlayer(303f, 300f, 200f);
			harness.Engage(undead, killer);
			BossAiHarness.Wound(undead, killer);

			undead.GetAi().OnGeneralEvent(AiEventType.Died);
			left += Count(harness);
		}

		return left;
	}

	/// <summary>
	/// <b>Killing one is a coin flip.</b> Not never, and not always — twenty deaths land somewhere in
	/// between, and a translation that dropped the probability guard would sit at one end.
	/// </summary>
	[Fact]
	public void HalfOfTheDeathsLeaveAFear()
	{
		int left = DeathsThatLeftAFear(ImmortalWarrior);

		Assert.True(left > 2, $"only {left} of twenty deaths left a fear — the roll is not happening");
		Assert.True(left < 18, $"{left} of twenty deaths left a fear — the roll is not happening");
	}

	/// <summary>And every one of the eight patterns does it: one class covers all twenty-one npcs.</summary>
	[Fact]
	public void ThePriestSideDoesItToo()
	{
		int left = DeathsThatLeftAFear(EternalPriest);

		Assert.True(left > 2, $"only {left} of twenty deaths left a fear");
	}

	/// <summary>
	/// <b>The fear lands on the killer, not on the corpse.</b> Retail's <c>OBJI_KILLER</c>, read as the
	/// player who did the most damage — so a group that sends one member in gets the fear on that
	/// member wherever the undead happened to fall.
	/// </summary>
	[Fact]
	public void TheFearLandsOnTheKillerRatherThanTheCorpse()
	{
		Npc? standing = null;
		Player? killer = null;

		for (int i = 0; i < 20 && standing == null; i++)
		{
			using BossAiHarness harness = NewHarness();
			Npc undead = harness.Spawn(ImmortalWarrior, 300f, 300f, 200f);

			// Forty metres away: inside retail's valid_distance and nowhere near the corpse.
			Player attacker = harness.SpawnPlayer(340f, 300f, 200f);
			Player bystander = harness.SpawnPlayer(303f, 300f, 200f);

			harness.Engage(undead, attacker);
			BossAiHarness.Rehate(undead, bystander);
			BossAiHarness.Rehate(undead, bystander);
			BossAiHarness.Wound(undead, attacker, damage: 5000);
			BossAiHarness.Wound(undead, bystander, damage: 1);

			undead.GetAi().OnGeneralEvent(AiEventType.Died);

			standing = harness.LiveNpcs().FirstOrDefault(n => n.GetNpcId() == Fear);
			if (standing != null)
			{
				killer = attacker;
				Assert.Equal(killer.GetX(), standing.GetX(), 1);
				Assert.Equal(killer.GetY(), standing.GetY(), 1);
			}
		}

		Assert.NotNull(killer);
	}

	/// <summary>
	/// <b>And most damage decides it, not most hate.</b> The bystander holds the hate list; the fear
	/// still goes to whoever actually did the damage.
	/// </summary>
	[Fact]
	public void MostDamageDecidesItRatherThanMostHate()
	{
		bool everSeen = false;

		for (int i = 0; i < 20 && !everSeen; i++)
		{
			using BossAiHarness harness = NewHarness();
			Npc undead = harness.Spawn(ImmortalWarrior, 300f, 300f, 200f);
			Player damager = harness.SpawnPlayer(340f, 300f, 200f);
			Player tank = harness.SpawnPlayer(303f, 300f, 200f);

			harness.Engage(undead, tank);
			for (int n = 0; n < 20; n++)
				BossAiHarness.Rehate(undead, tank);

			BossAiHarness.Wound(undead, damager, damage: 5000);
			BossAiHarness.Wound(undead, tank, damage: 1);

			undead.GetAi().OnGeneralEvent(AiEventType.Died);

			if (harness.LiveNpcs().FirstOrDefault(n => n.GetNpcId() == Fear) is Npc fear)
			{
				everSeen = true;
				Assert.Equal(damager.GetX(), fear.GetX(), 1);
			}
		}

		Assert.True(everSeen, "twenty deaths and never a fear to look at");
	}

	/// <summary>
	/// Nothing comes without a killer — an undead that expires or is cleared leaves the field empty,
	/// because the branch has nobody to spawn on.
	/// </summary>
	[Fact]
	public void WithNobodyOnTheListNothingIsLeft()
	{
		using BossAiHarness harness = NewHarness();

		for (int i = 0; i < 20; i++)
		{
			Npc undead = harness.Spawn(ImmortalWarrior, 300f + i, 300f, 200f);
			undead.GetAi().OnGeneralEvent(AiEventType.Died);
		}

		Assert.Equal(0, Count(harness));
	}
}
