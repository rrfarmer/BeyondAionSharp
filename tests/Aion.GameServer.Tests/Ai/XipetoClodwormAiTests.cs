using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the corask and gnarl clodworm burst, translated from retail patterns
/// <c>LDF5_D2_Xipeto_Clodworm</c>, <c>_63</c>, <c>_65</c> and
/// <c>LDF5_D2_Xipeto_Sufur_Clodworm_65</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class XipetoClodwormAiTests
{
	private const int Cygnea = 210070000;

	private const int EbonCorask = 219754;
	private const int WilyGnarl = 230494;
	private const int LurkingCorask = 235878;
	private const int SwampGnarl = 230586;

	private const int Swarm61 = 284155;
	private const int Swarm63 = 284157;
	private const int Swarm65 = 283903;
	private const int SwarmSulphur = 283904;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Cygnea).WithWorldSize(2048)
			.WithAi(typeof(XipetoClodworm61AI), typeof(XipetoClodworm63AI), typeof(XipetoClodworm65AI),
				typeof(XipetoClodwormSulphurAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	private static void Strike(Npc target, Creature attacker) =>
		target.GetAi().OnCreatureEvent(AiEventType.Attack, attacker);

	private static (BossAiHarness, Npc, Player) Field(int npcId)
	{
		BossAiHarness harness = NewHarness();
		Npc worm = harness.Spawn(npcId, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f);
		harness.Engage(worm, raider);
		return (harness, worm, raider);
	}

	/// <summary><b>Below half health, three clodworms.</b> Retail's <c>num_to_spawn</c> is three.</summary>
	[Fact]
	public void BelowHalfHealthThreeClodworms()
	{
		var (harness, worm, raider) = Field(EbonCorask);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(worm, 60);
		Strike(worm, raider);
		Assert.Empty(Live(harness, Swarm61));

		BossAiHarness.SetExactPercent(worm, 49);
		Strike(worm, raider);

		Assert.Equal(3, Live(harness, Swarm61).Count);
	}

	/// <summary>
	/// <b>Once a fight, however many blows land.</b> Retail's <c>FLAGVARI_ALPHA_1</c>, and without it a
	/// corask fought from half to nothing would bury a player in swarms.
	/// </summary>
	[Fact]
	public void OnceAFightHoweverManyBlowsLand()
	{
		var (harness, worm, raider) = Field(EbonCorask);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(worm, 49);
		Strike(worm, raider);
		Strike(worm, raider);
		Strike(worm, raider);

		Assert.Equal(3, Live(harness, Swarm61).Count);
	}

	/// <summary>
	/// <b>They arrive already fighting.</b> Retail carries <c>attack_target_after_spawn</c> with a
	/// hundred hate points, so the swarm is on the player rather than waiting to be walked into.
	/// </summary>
	[Fact]
	public void TheyArriveAlreadyFighting()
	{
		var (harness, worm, raider) = Field(EbonCorask);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(worm, 49);
		Strike(worm, raider);

		// The hate lands on the next tick -- AttackAfterSpawn.NextTick, so a summon is in the world
		// before it is told who to hit.
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		// A hundred and one, not a hundred: retail's hatepoints_to_add is a hundred and our
		// AttackAfterSpawn adds one more when the summon actually starts swinging. Pinned as read
		// rather than rounded, so a change to either number shows up here.
		Assert.All(Live(harness, Swarm61), swarm =>
		{
			Assert.Same(raider, swarm.GetTarget());
			Assert.Equal(101, swarm.GetAggroList().GetHate(raider));
		});
	}

	/// <summary>
	/// <b>The corask takes them with it.</b> Retail clears the group on dying, on leaving the fight, on
	/// going idle and on returning to its spawn — so a swarm never outlives what made it.
	/// </summary>
	[Fact]
	public void TheCoraskTakesThemWithIt()
	{
		var (harness, worm, raider) = Field(EbonCorask);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(worm, 49);
		Strike(worm, raider);
		Assert.Equal(3, Live(harness, Swarm61).Count);

		worm.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Empty(Live(harness, Swarm61));
	}

	/// <summary>
	/// <b>And going home clears them too</b>, which is the branch a player who runs away exercises.
	/// </summary>
	[Fact]
	public void AndGoingHomeClearsThemToo()
	{
		var (harness, worm, raider) = Field(EbonCorask);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(worm, 49);
		Strike(worm, raider);
		Assert.Equal(3, Live(harness, Swarm61).Count);

		worm.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Empty(Live(harness, Swarm61));
	}

	/// <summary>
	/// <b>Each band calls its own swarm.</b> Retail gives the four patterns four different summons, and
	/// one shared id would put a level-sixty-one swarm on a level-sixty-five fight.
	/// </summary>
	[Theory]
	[InlineData(EbonCorask, Swarm61)]
	[InlineData(WilyGnarl, Swarm63)]
	[InlineData(LurkingCorask, Swarm65)]
	[InlineData(SwampGnarl, SwarmSulphur)]
	public void EachBandCallsItsOwnSwarm(int parent, int swarm)
	{
		var (harness, worm, raider) = Field(parent);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(worm, 49);
		Strike(worm, raider);

		Assert.Equal(3, Live(harness, swarm).Count);
		foreach (int other in new[] { Swarm61, Swarm63, Swarm65, SwarmSulphur })
			if (other != swarm)
				Assert.Empty(Live(harness, other));
	}
}
