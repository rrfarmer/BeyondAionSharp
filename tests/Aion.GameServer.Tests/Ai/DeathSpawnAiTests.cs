using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The nine NPCs that leave something behind when they fall, and left nothing.
/// </summary>
/// <remarks>
/// See <see cref="DeathSpawnAI"/>. Every one ran plain <c>aggressive</c>, and no data could have covered
/// them: <c>&lt;summons&gt;</c> is keyed on health percentage and has no death trigger.
/// <para>
/// The death event is raised directly rather than through <c>BossAiHarness.Kill</c>, because four of the
/// nine are guarded on <c>is_user</c> and <c>Kill</c> records no damage — see
/// <see cref="TiamatSiegeWeaponAiTests"/> for why those two cannot be had at once, and
/// <see cref="HarnessKillTests"/> for the pin that covers the controller path.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DeathSpawnAiTests
{
	/// <summary>Any map: these are spread across five instances and the harness spawns by id.</summary>
	private const int MapId = 300100000;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(MapId).WithWorldSize(2048)
			.WithAi(typeof(DeathSpawnAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static BossAiHarness Fell(int npcId, bool byPlayer)
	{
		BossAiHarness harness = NewHarness();
		Npc npc = harness.Spawn(npcId, 500f, 500f, 200f);
		if (byPlayer)
		{
			Player raider = harness.SpawnPlayer(504f, 500f, 200f);
			harness.Engage(npc, raider);
			BossAiHarness.Wound(npc, raider);
		}

		npc.GetAi().OnGeneralEvent(AiEventType.Died);
		return harness;
	}

	/// <summary>
	/// <b>Every NPC in the table leaves what retail says it leaves, in the number retail says.</b>
	/// </summary>
	[Fact]
	public void EveryOneLeavesWhatRetailLeaves()
	{
		foreach ((int npcId, DeathSpawnAI.Bequest left) in DeathSpawnAI.Bequests)
		{
			using BossAiHarness harness = Fell(npcId, byPlayer: true);

			Assert.Equal(left.Count, harness.LiveNpcs().Count(n => n.GetNpcId() == left.NpcId));
		}
	}

	/// <summary>
	/// <b>The suspicious boy is not a boy.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>ND2_ReA_1</c> puts <c>Adma_UndeadLightRaNamedReal_50_Ae</c> — betrayer villaire — where
	/// the boy fell, for an hour, and does it whether a player or an NPC landed the kill. Pinned by name
	/// because it is the row that makes the whole class worth having: a disguise nobody could see through.
	/// </remarks>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void TheSuspiciousBoyRevealsTheBetrayer(bool byPlayer)
	{
		using BossAiHarness harness = Fell(214700, byPlayer);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == 214701));
	}

	/// <summary>
	/// <b>The four guarded on <c>is_user</c> leave nothing when no player did the damage.</b>
	/// </summary>
	/// <remarks>
	/// Without this the guard is invisible, because every other pin here supplies a player. It is also
	/// the pin that would notice the flag being flattened to "always" across the table — which is the
	/// tempting simplification, since five of the nine are unguarded.
	/// </remarks>
	[Fact]
	public void ThePlayerKillOnlyOnesLeaveNothingToAnNpcKill()
	{
		foreach ((int npcId, DeathSpawnAI.Bequest left) in DeathSpawnAI.Bequests.Where(b => b.Value.PlayerKillOnly))
		{
			using BossAiHarness harness = Fell(npcId, byPlayer: false);

			Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == left.NpcId);
		}
	}

	/// <summary>
	/// <b>And the five that are not guarded leave theirs to any death.</b>
	/// </summary>
	/// <remarks>
	/// The mirror of the pin above: without it, guarding every row on <c>is_user</c> would also pass.
	/// </remarks>
	[Fact]
	public void TheUnguardedOnesLeaveTheirsToAnyDeath()
	{
		foreach ((int npcId, DeathSpawnAI.Bequest left) in DeathSpawnAI.Bequests.Where(b => !b.Value.PlayerKillOnly))
		{
			using BossAiHarness harness = Fell(npcId, byPlayer: false);

			Assert.Equal(left.Count, harness.LiveNpcs().Count(n => n.GetNpcId() == left.NpcId));
		}
	}
}
