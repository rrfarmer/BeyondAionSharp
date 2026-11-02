using Aion.Bots.Protocol;
using Aion.Bots.Navigation;
using Aion.Bots.Movement;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.Commons.Logging;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunC1Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 17, "Asimcaa", Race.ASMODIANS, PlayerClass.WARRIOR, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.Equal(1, player.GetCommonData().GetExp());
		Assert.Equal(400, player.GetCommonData().GetExpNeed());

		for (int kill = 1; kill <= 5; kill++)
		{
			session.BeginStep($"s{kill + 1:D2}", $"kill-sprigg-worker-{kill}");
			Npc sprigg = FindLivingNpc(player, 210363);
			await PlaceBesideNpcAsync(session, player, sprigg, token);
			sprigg.GetLifeStats().SetCurrentHp(1);
			long before = player.GetCommonData().GetExp();
			await session.SendPacketAsync(session.Api.Target(sprigg.GetObjectId()), token);
			await session.SendPacketAsync(
				session.Api.Attack(sprigg.GetObjectId(), player.GetGameStats().GetAttackSpeed().GetCurrent()), token);
			await session.WaitForPacketAsync(typeof(SM_STATUPDATE_EXP), token);
			Assert.Equal(80, player.GetCommonData().GetExp() - before);
			Assert.Equal(kill == 5 ? 2 : 1, player.GetLevel());
			if (kill != 5)
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(1400), token);
		}

		Assert.Equal(401, player.GetCommonData().GetExp());
		policy.AssertClean();
	}

	private async Task RunC2Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 18, "Asimcab", Race.ASMODIANS, PlayerClass.WARRIOR, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		// As in C3, use the level-2 snuffler: a critical Ferocious Strike can kill
		// the 143-HP level-1 target before the ten-second cooldown can be measured.
		Npc target = fixture.World.GetWorldMap(220010000).GetMainWorldMapInstance().GetNpcs(210366)
			.First(npc => npc.IsSpawned() && !npc.IsDead());
		string DescribeTarget() => $"target={target.GetObjectId()}/{target.GetNpcId()}, hp={target.GetLifeStats().GetCurrentHp()}/{target.GetLifeStats().GetMaxHp()}, ai={target.GetAi().GetState()}, " +
			"damage=[" + string.Join(", ", target.GetAggroList().GetFinalDamageList().GetCreatureDamages().Select(damage =>
				$"{damage.GetAttacker().GetObjectId()}/{damage.GetAttacker().GetName()}:{damage.GetDamage()}")) + "]";
		string initialTarget = DescribeTarget();
		await PlaceBesideNpcAsync(session, player, target, token);
		await session.SendPacketAsync(session.Api.Target(target.GetObjectId()), token);
		var cast = new SpellCastData(2864, 1, 0) { TargetObjectId = target.GetObjectId() };

		session.BeginStep("s02", "cast-ferocious-strike");
		await session.SendPacketAsync(session.Api.Cast(cast), token);
		await session.WaitForPacketAsync(typeof(SM_CASTSPELL_RESULT), token,
			packet => packet.Get<ushort>("skillId") == 2864);
		Assert.False(target.IsDead());

		session.BeginStep("s03", "reject-before-ten-seconds");
		await session.SendPacketAsync(GameClientPackets.CastSpell(cast), token);
		DecodedBotServerPacket refused = await session.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token,
			packet => string.Equals(packet.Get<string?>("name"), "STR_SKILL_NOT_READY", StringComparison.Ordinal));
		Assert.Equal("STR_SKILL_NOT_READY", refused.Get<string>("name"));

		session.BeginStep("s04", "accept-at-ten-seconds");
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(10_001), token);
		Assert.False(target.IsDead(), $"C2 target died while waiting for cooldown. Before: {initialTarget}; after: {DescribeTarget()}");
		await session.SendPacketAsync(GameClientPackets.CastSpell(cast), token);
		try
		{
			await session.WaitForPacketAsync(typeof(SM_CASTSPELL_RESULT), token,
				packet => packet.Get<ushort>("skillId") == 2864);
		}
		catch (OperationCanceledException exception)
		{
			string packets = string.Join(Environment.NewLine, session.PacketHistory.TakeLast(20).Select(packet =>
				packet.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(packet.Fields)));
			throw new InvalidOperationException($"C2 cooldown-expiry cast did not complete. Player dead={player.IsDead()}, target dead={target.IsDead()}; " +
				$"player=({player.GetX()},{player.GetY()},{player.GetZ()}), target=({target.GetX()},{target.GetY()},{target.GetZ()}). Recent packets:{Environment.NewLine}{packets}", exception);
		}
		policy.AssertClean();
	}

	private async Task RunC3Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 19, "Asimcac", Race.ASMODIANS, PlayerClass.WARRIOR, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		// The level-2 bucktoothed snuffler (199 HP): a level-1 Warrior's Ferocious Strike has hit the level-1
		// snuffler (143 HP) for its whole health, which ended the fight before the gate could be measured.
		Npc target = fixture.World.GetWorldMap(220010000).GetMainWorldMapInstance().GetNpcs(210366)
			.Where(npc => npc.IsSpawned() && !npc.IsDead()).First();
		await PlaceBesideNpcAsync(session, player, target, token);
		target.GetLifeStats().SetCurrentHp(target.GetLifeStats().GetMaxHp());
		await session.SendPacketAsync(session.Api.Target(target.GetObjectId()), token);
		int attackSpeed = player.GetGameStats().GetAttackSpeed().GetCurrent();

		session.BeginStep("s02", "first-auto-attack");
		await session.SendPacketAsync(session.Api.Attack(target.GetObjectId(), attackSpeed), token);
		await session.WaitForPacketAsync(typeof(SM_ATTACK), token);

		session.BeginStep("s03", "skill-does-not-reset-auto-attack-gate");
		var cast = new SpellCastData(2864, 1, 0) { TargetObjectId = target.GetObjectId() };
		await session.SendPacketAsync(session.Api.Cast(cast), token);
		await session.WaitForPacketAsync(typeof(SM_CASTSPELL_RESULT), token,
			packet => packet.Get<ushort>("skillId") == 2864);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
		await session.SendPacketAsync(GameClientPackets.Attack(target.GetObjectId(), 1, 0, 0), token);
		await session.WaitForPacketAsync(typeof(SM_ATTACK_RESPONSE), token);

		session.BeginStep("s04", "accept-after-fourteen-hundred-milliseconds");
		// This scenario measures the auto-attack gate, not damage. The skill's hit lands during the wait below
		// (hitTime 800 ms); after the first auto-attack a strong roll could kill the target, leaving the final
		// swing nothing to hit. Which rolls come depend on spawn data, so start the wait at full health: the
		// largest Ferocious Strike seen (143) leaves a full-health level-2 snuffler standing.
		target.GetLifeStats().SetCurrentHp(target.GetLifeStats().GetMaxHp());
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(900), token);
		await session.SendPacketAsync(GameClientPackets.Attack(target.GetObjectId(), 2, 0, 0), token);
		await session.WaitForPacketAsync(typeof(SM_ATTACK), token);
		policy.AssertClean();
	}

	private async Task RunC4Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 20, "Asimcad", Race.ELYOS, PlayerClass.MAGE, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Npc target = fixture.World.GetWorldMap(210010000).GetMainWorldMapInstance().GetNpcs(210119)
			.First(npc => npc.IsSpawned() && !npc.IsDead());
		await PlaceBesideNpcAsync(session, player, target, token);
		await session.SendPacketAsync(session.Api.Target(target.GetObjectId()), token);
		var cast = new SpellCastData(1282, 1, 0) { TargetObjectId = target.GetObjectId() };

		session.BeginStep("s02", "movement-cancels-flame-bolt");
		await session.SendPacketAsync(session.Api.Cast(cast), token);
		await session.WaitForPacketAsync(typeof(SM_CASTSPELL), token,
			packet => packet.Get<ushort>("spellId") == 1282);
		await session.SendPacketAsync(GameClientPackets.Move(new MovementPacketData(
			player.GetX(), player.GetY(), player.GetZ(), player.GetHeading(), MovementMask.IMMEDIATE)), token);
		await session.WaitForPacketAsync(typeof(SM_SKILL_CANCEL), token,
			packet => packet.Get<ushort>("skillId") == 1282);

		session.BeginStep("s03", "early-recast-is-refused-and-audited");
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(501), token);
		await session.SendPacketAsync(GameClientPackets.CastSpell(cast), token);
		await session.WaitForPacketAsync(typeof(SM_CASTSPELL), token,
			packet => packet.Get<ushort>("spellId") == 1282);
		int logStart = fixture.LogCapture.Entries.Count;
		await session.SendPacketAsync(GameClientPackets.CastSpell(cast), token);
		await session.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token,
			packet => string.Equals(packet.Get<string?>("name"), "STR_SKILL_NOT_READY", StringComparison.Ordinal));
		var auditEntries = fixture.LogCapture.Entries.Skip(logStart)
			.Where(entry => entry.Category == "AUDIT_LOG")
			.ToArray();
		CapturedLogEntry audit = Assert.Single(auditEntries);
		Assert.Contains("tried to use skill 1282", audit.Message, StringComparison.Ordinal);
		Assert.Contains("Previous skill: 1282", audit.Message, StringComparison.Ordinal);

		session.BeginStep("s04", "complete-flame-bolt");
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(10_001), token);
		await session.WaitForPacketAsync(typeof(SM_CASTSPELL_RESULT), token,
			packet => packet.Get<ushort>("skillId") == 1282);
		policy.AssertClean();
	}

	private async Task RunC5Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 21, "Asimcae", Race.ELYOS, PlayerClass.WARRIOR, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Npc paruru = fixture.World.GetWorldMap(210010000).GetMainWorldMapInstance().GetNpcs(210673)
			.First(npc => npc.IsSpawned() && !npc.IsDead());
		float spawnX = paruru.GetX();
		float spawnY = paruru.GetY();
		float spawnZ = paruru.GetZ();

		session.BeginStep("s02", "enter-aggression-range");
		await TeleportForSetupAsync(session, player, paruru.GetWorldId(), spawnX - 10, spawnY, spawnZ, token);
		await session.MoveToPositionAsync(new BotPosition(spawnX - 1, spawnY, spawnZ, player.GetHeading()), token);
		await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
		Assert.True(paruru.GetAggroList().IsHating(player),
			$"Paruru did not aggro: distance={PositionUtil.GetDistance(paruru, player):F2}, " +
			$"known={paruru.GetKnownList().GetObject(player.GetObjectId()) != null}, " +
			$"playerKnown={player.GetKnownList().GetObject(paruru.GetObjectId()) != null}, " +
			$"canSee={paruru.CanSee(player)}, aggressive={TribeRelationService.IsAggressive(paruru, player)}, " +
			$"friend={TribeRelationService.IsFriend(paruru, player)}, enemy={player.IsEnemyFrom(paruru)}, " +
			$"regionActive={paruru.GetPosition().IsMapRegionActive()}, ai={paruru.GetAi().GetType().Name}, " +
			$"protection={player.IsProtectionActive()}, periodic=" +
			string.Join(';', fixture.Clock.GetTopPeriodicTaskTimings(20).Select(t => $"{t.Name}:{t.Invocations}")));

		session.BeginStep("s03", "leave-leash-range");
		await TeleportForSetupAsync(session, player, paruru.GetWorldId(), spawnX - 200, spawnY, spawnZ, token);
		await session.AdvanceAsync(TimeSpan.FromSeconds(60), token);
		Assert.False(paruru.GetAggroList().IsHating(player));
		Assert.Null(paruru.GetTarget());
		Assert.InRange(MathF.Abs(paruru.GetX() - spawnX), 0, 1.5f);
		Assert.InRange(MathF.Abs(paruru.GetY() - spawnY), 0, 1.5f);
		policy.AssertClean();
	}

	private async Task RunC6Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 22, "Asimcaf", Race.ASMODIANS, PlayerClass.WARRIOR, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		long expBefore = player.GetCommonData().GetExp();
		Npc attacker = FindLivingNpc(player, 210363);
		await PlaceBesideNpcAsync(session, player, attacker, token);
		// Teleport grants protection. Java CM_MOVE:140-141 ends it on real horizontal movement;
		// a director position change alone is not a player entering combat.
		await session.MoveToPositionAsync(new BotPosition(player.GetX() - 1, player.GetY(), player.GetZ(), player.GetHeading()), token);
		Assert.False(player.IsProtectionActive());

		session.BeginStep("s02", "npc-kills-level-one-player");
		player.GetLifeStats().SetCurrentHp(1);
		// A normal attack may dodge/resist; a single attempt does not guarantee the death packet.
		var attackSamples = new List<string>();
		for (int attempt = 0; attempt < 20 && !player.IsDead(); attempt++)
		{
			int beforeAttack = player.GetLifeStats().GetCurrentHp();
			attacker.GetController().AttackTarget(player, 0, true);
			attackSamples.Add($"{beforeAttack}->{player.GetLifeStats().GetCurrentHp()}");
			await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		}
		Assert.True(player.IsDead(), $"C6 target survived 20 attacks: hp={player.GetLifeStats().GetCurrentHp()}, protection={player.IsProtectionActive()}, attacks={string.Join(',', attackSamples)}.");
		await session.WaitForPacketAsync(typeof(SM_DIE), token);
		Assert.True(player.IsDead());
		Assert.Equal(expBefore, player.GetCommonData().GetExp());

		session.BeginStep("s03", "bind-revive-at-twenty-five-percent");
		await session.SendPacketAsync(session.Api.Revive(), token);
		await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
		Assert.False(player.IsDead());
		Assert.InRange(player.GetLifeStats().GetCurrentHp(),
			player.GetLifeStats().GetMaxHp() * 25 / 100,
			(int)Math.Ceiling(player.GetLifeStats().GetMaxHp() * 0.25));
		Assert.InRange(player.GetLifeStats().GetCurrentMp(),
			player.GetLifeStats().GetMaxMp() * 25 / 100,
			(int)Math.Ceiling(player.GetLifeStats().GetMaxMp() * 0.25));
		Assert.True(player.GetEffectController().HasAbnormalEffect(8291));
		Assert.Equal(expBefore, player.GetCommonData().GetExp());
		policy.AssertClean();
	}

	private async Task RunC7Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 23, "Asimcag", Race.ASMODIANS, PlayerClass.WARRIOR, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Item potion = player.GetInventory().GetFirstItemByItemId(162000002);
		Assert.NotNull(potion);
		player.GetLifeStats().SetCurrentHp(1);
		long initialCount = potion.GetItemCount();

		session.BeginStep("s02", "use-minor-life-potion");
		await session.SendPacketAsync(session.Api.UseItem(potion.GetObjectId(), potion.GetItemTemplate()), token);
		await session.WaitForPacketAsync(typeof(SM_ITEM_USAGE_ANIMATION), token);
		Assert.Equal(initialCount - 1, potion.GetItemCount());
		long firstReuse = player.GetItemReuseTime(11);
		Assert.InRange(firstReuse - SystemClock.CurrentMillis(), 29_999, 30_000);

		session.BeginStep("s03", "reject-during-shared-item-delay");
		await session.SendPacketAsync(GameClientPackets.UseItem(potion.GetObjectId()), token);
		await session.DrainServerPacketsAsync(token);
		Assert.Equal(initialCount - 1, potion.GetItemCount());
		Assert.Equal(firstReuse, player.GetItemReuseTime(11));

		session.BeginStep("s04", "accept-after-thirty-seconds");
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(30_001), token);
		player.GetLifeStats().SetCurrentHp(1);
		await session.SendPacketAsync(GameClientPackets.UseItem(potion.GetObjectId()), token);
		await session.WaitForPacketAsync(typeof(SM_ITEM_USAGE_ANIMATION), token);
		Assert.Equal(initialCount - 2, potion.GetItemCount());
		Assert.True(player.GetItemReuseTime(11) > firstReuse);
		policy.AssertClean();
	}

	private async Task RunC8Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 24, "Asimcah", Race.ELYOS, PlayerClass.WARRIOR, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Npc sparkie = player.GetPosition().GetWorldMapInstance().GetNpcs(210119)
			.Where(npc => npc.IsSpawned() && !npc.IsDead()).Skip(1).First();
		await PlaceBesideNpcAsync(session, player, sparkie, token);
		var spawn = sparkie.GetSpawn();

		session.BeginStep("s02", "empty-corpse-decays-after-two-seconds");
		await KillNpcAsync(session, player, sparkie, token);
		HashSet<Aion.GameServer.Model.Drop.DropItem> firstDrops =
			DropRegistrationService.GetInstance().GetCurrentDropMap()[sparkie.GetObjectId()];
		firstDrops.Clear();
		sparkie.GetController().CancelTask(TaskId.DECAY);
		RespawnService.ScheduleDecayTask(sparkie);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(1_999), token);
		Assert.Same(sparkie, fixture.World.FindVisibleObject(sparkie.GetObjectId()));
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(2), token);
		Assert.Null(fixture.World.FindVisibleObject(sparkie.GetObjectId()));

		session.BeginStep("s03", "respawn-on-spawn-schedule");
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(25_998), token);
		Assert.DoesNotContain(player.GetPosition().GetWorldMapInstance().GetNpcs(210119),
			npc => ReferenceEquals(npc.GetSpawn(), spawn) && npc.IsSpawned() && !npc.IsDead());
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(2), token);
		Npc respawned = Assert.Single(player.GetPosition().GetWorldMapInstance().GetNpcs(210119),
			npc => ReferenceEquals(npc.GetSpawn(), spawn) && npc.IsSpawned() && !npc.IsDead());

		session.BeginStep("s04", "looting-last-drop-deletes-corpse");
		await PlaceBesideNpcAsync(session, player, respawned, token);
		await KillNpcAsync(session, player, respawned, token);
		HashSet<Aion.GameServer.Model.Drop.DropItem> secondDrops =
			DropRegistrationService.GetInstance().GetCurrentDropMap()[respawned.GetObjectId()];
		secondDrops.Clear();
		secondDrops.Add(DropRegistrationService.GetInstance().RegDropItem(
			1, player.GetObjectId(), respawned.GetObjectId(), 182400001, 1));
		respawned.GetController().CancelTask(TaskId.DECAY);
		RespawnService.ScheduleDecayTask(respawned, RespawnService.WITH_DROP_DECAY);
		await session.SendPacketAsync(session.Api.Loot(respawned.GetObjectId()), token);
		DecodedBotServerPacket list = await session.WaitForPacketAsync(typeof(SM_LOOT_ITEMLIST), token);
		byte index = Assert.Single(list.Get<IReadOnlyList<IReadOnlyDictionary<string, object?>>>("items"))
			.GetValueOrDefault("index") is byte dropIndex ? dropIndex : throw new InvalidDataException("Missing drop index.");
		await session.WaitForPacketAsync(typeof(SM_LOOT_STATUS), token,
			packet => packet.Get<byte>("status") == (byte)SM_LOOT_STATUS.Status.OPEN_DROP_LIST);
		await session.SendPacketAsync(session.Api.Loot(respawned.GetObjectId(), index), token);
		await session.WaitForPacketAsync(typeof(SM_LOOT_STATUS), token,
			packet => packet.Get<byte>("status") == (byte)SM_LOOT_STATUS.Status.CLOSE_DROP_LIST);
		Assert.Null(fixture.World.FindVisibleObject(respawned.GetObjectId()));
		policy.AssertClean();
	}

	private async Task RunC9Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 25, "Asimcai", Race.ASMODIANS, PlayerClass.WARRIOR, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Npc snuffler = fixture.World.GetWorldMap(220010000).GetMainWorldMapInstance().GetNpcs(210365)
			.Where(npc => npc.IsSpawned() && !npc.IsDead()).Skip(2).First();
		await PlaceBesideNpcAsync(session, player, snuffler, token);
		await session.SendPacketAsync(session.Api.Target(snuffler.GetObjectId()), token);
		await session.SendPacketAsync(
			session.Api.Attack(snuffler.GetObjectId(), player.GetGameStats().GetAttackSpeed().GetCurrent()), token);
		await session.WaitForPacketAsync(typeof(SM_ATTACK), token);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
		Assert.True(snuffler.GetAggroList().IsHating(player));
		// Teleport despawns the target and clears NPC awareness. Retreat through CM_MOVE instead.
		var retreatStart = new BotPosition(player.GetX(), player.GetY(), player.GetZ(), player.GetHeading());
		var geometry = BotNavigationGeometry.ForServerWorld(player.GetInstanceId(), player.GetRace());
		var retreat = geometry.TraceEdge(player.GetWorldId(), retreatStart,
			retreatStart with { X = retreatStart.X - 10 });
		Assert.NotNull(retreat);
		await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(
			retreat, retreatStart, player.GetGameStats().GetMovementSpeedFloat()), token);
		Assert.True(snuffler.GetAggroList().IsHating(player), "Retreat lost aggro before the jump.");
		float chaseStartX = snuffler.GetX();
		float chaseStartY = snuffler.GetY();

		session.BeginStep("s02", "jump-while-npc-is-chasing");
		var jump = new MovementPacketData(
			player.GetX(), player.GetY(), player.GetZ(), player.GetHeading(),
			(byte)(MovementMask.POSITION | MovementMask.MANUAL),
			VectorX: 0.2f, VectorY: 0, VectorZ: 0.8f);
		foreach (BotClientPacket packet in session.Api.Jump(jump))
			await session.SendPacketAsync(packet, token);
		for (int sample = 0; sample < 30; sample++)
		{
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(100), token);
			if (sample == 2)
			{
				await session.SendPacketAsync(session.Api.MoveTo(jump with { X = jump.X + 0.1f, Z = jump.Z + 0.8f }), token);
				Assert.Equal(jump.Z + 0.8f, player.GetZ(), 3);
			}
			if (sample == 7)
			{
				float landingZ = Aion.GameServer.World.Geo.GeoService.GetInstance().GetZ(player.GetWorldId(),
					jump.X + 0.2f, jump.Y, jump.Z + 2, jump.Z - 2, player.GetInstanceId());
				Assert.True(float.IsFinite(landingZ));
				await session.SendPacketAsync(session.Api.MoveTo(new MovementPacketData(jump.X + 0.2f, jump.Y,
					landingZ, jump.Heading, MovementMask.IMMEDIATE)), token);
				Assert.Equal(landingZ, player.GetZ(), 3);
			}
			Assert.True(snuffler.GetAggroList().IsHating(player), $"Jump chase lost aggro at sample {sample}.");
			Assert.Same(player, snuffler.GetTarget());
		}
		float chaseDistance = MathF.Sqrt(
			MathF.Pow(snuffler.GetX() - chaseStartX, 2) + MathF.Pow(snuffler.GetY() - chaseStartY, 2));
		bool keptChasing = snuffler.GetAggroList().IsHating(player) &&
			ReferenceEquals(snuffler.GetTarget(), player) && chaseDistance > 0.5f;
		policy.AssertClean();
		Assert.True(keptChasing,
			$"NPC stopped chasing after a jump with geo enabled (distance {chaseDistance}, hating {snuffler.GetAggroList().IsHating(player)}).");
	}

	private async Task RunC10Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 26, "Asimcaj", Race.ELYOS, PlayerClass.WARRIOR, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Npc sparkie = player.GetPosition().GetWorldMapInstance().GetNpcs(210119)
			.Where(npc => npc.IsSpawned() && !npc.IsDead()).Skip(2).First();
		await PlaceBesideNpcAsync(session, player, sparkie, token);
		long kinahBefore = player.GetInventory().GetKinah();

		session.BeginStep("s02", "kill-and-enable-loot");
		await KillNpcAsync(session, player, sparkie, token);
		HashSet<Aion.GameServer.Model.Drop.DropItem> drops =
			DropRegistrationService.GetInstance().GetCurrentDropMap()[sparkie.GetObjectId()];
		drops.Clear();
		drops.Add(DropRegistrationService.GetInstance().RegDropItem(
			1, player.GetObjectId(), sparkie.GetObjectId(), 182400001, 1));

		session.BeginStep("s03", "open-drop-list");
		await session.SendPacketAsync(session.Api.Loot(sparkie.GetObjectId()), token);
		DecodedBotServerPacket list = await session.WaitForPacketAsync(typeof(SM_LOOT_ITEMLIST), token,
			packet => packet.Get<int>("targetObjectId") == sparkie.GetObjectId());
		IReadOnlyDictionary<string, object?> item = Assert.Single(
			list.Get<IReadOnlyList<IReadOnlyDictionary<string, object?>>>("items"));
		Assert.Equal(182400001, item.GetValueOrDefault("itemId"));
		byte index = Assert.IsType<byte>(item.GetValueOrDefault("index"));
		await session.WaitForPacketAsync(typeof(SM_LOOT_STATUS), token,
			packet => packet.Get<byte>("status") == (byte)SM_LOOT_STATUS.Status.OPEN_DROP_LIST);

		session.BeginStep("s04", "loot-item-and-close-list");
		await session.SendPacketAsync(session.Api.Loot(sparkie.GetObjectId(), index), token);
		await session.WaitForPacketAsync(typeof(SM_LOOT_STATUS), token,
			packet => packet.Get<byte>("status") == (byte)SM_LOOT_STATUS.Status.CLOSE_DROP_LIST);
		Assert.Equal(kinahBefore + 1, player.GetInventory().GetKinah());
		Assert.Null(fixture.World.FindVisibleObject(sparkie.GetObjectId()));
		policy.AssertClean();
	}

	private async Task RunC11Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
		CancellationToken token = timeout.Token;
		var classes = new[]
		{
			(PlayerClass.SCOUT, AccountId: 27, Name: "Asimcak", ActiveSkills: new[] { 3182, 3195 }),
			(PlayerClass.PRIEST, AccountId: 28, Name: "Asimcal", ActiveSkills: new[] { 1838, 4012 }),
			(PlayerClass.ENGINEER, AccountId: 29, Name: "Asimcam", ActiveSkills: new[] { 2219 }),
			(PlayerClass.ARTIST, AccountId: 30, Name: "Asimcan", ActiveSkills: new[] { 4408 }),
		};

		for (int classIndex = 0; classIndex < classes.Length; classIndex++)
		{
			var entry = classes[classIndex];
			await using SimulationL0Session session = await EnterCombatWorldAsync(
				policy, entry.AccountId, entry.Name, Race.ELYOS, entry.Item1, token);
			Player player = fixture.World.GetPlayer(session.CharacterId);
			Npc target = player.GetPosition().GetWorldMapInstance().GetNpcs(210119)
				.Where(npc => npc.IsSpawned() && !npc.IsDead()).OrderBy(npc => npc.GetObjectId()).Skip(4 + classIndex).First();
			if (entry.Item1 == PlayerClass.SCOUT)
				await PlaceBesideNpcAsync(session, player, target, token);
			else
			{
				// C11 proves starter skills, not survival while casting at melee range against a level-six mob.
				// A fixed offset from one Sparkie can put us inside a neighbouring mob's sight radius. Select a
				// clear approach from the actual world instead; no AI, damage or interruption rules are disabled.
				var instanceNpcs = player.GetPosition().GetWorldMapInstance().GetNpcs().Where(npc => npc.IsSpawned() && !npc.IsDead()).ToArray();
				var aggressive = instanceNpcs.Where(npc => TribeRelationService.IsAggressive(npc, player)).ToArray();
				Assert.NotEmpty(aggressive);
				var approach = instanceNpcs.Where(npc => npc.GetNpcId() == 210119)
					.OrderBy(npc => npc.GetX()).ThenBy(npc => npc.GetY()).ThenBy(npc => npc.GetObjectId())
					.SelectMany(npc => Enumerable.Range(0, 8).Select(direction =>
					{
						double angle = direction * Math.PI / 4;
						return (Target: npc, Point: new BotPosition(npc.GetX() + 19 * (float)Math.Cos(angle),
							npc.GetY() + 19 * (float)Math.Sin(angle), npc.GetZ(), 0));
					}))
					.Select(candidate => (candidate.Target, Point: candidate.Point with { Z =
						Aion.GameServer.World.Geo.GeoService.GetInstance().GetZ(candidate.Target.GetWorldId(),
							candidate.Point.X, candidate.Point.Y, candidate.Point.Z + 2, candidate.Point.Z - 2, player.GetInstanceId()) }))
					.Where(candidate => float.IsFinite(candidate.Point.Z) &&
						Aion.GameServer.World.Geo.GeoService.GetInstance().GetMap(candidate.Target.GetWorldId()).CanSee(
							candidate.Point.X, candidate.Point.Y, candidate.Point.Z + 1.25f,
							candidate.Target.GetX(), candidate.Target.GetY(), candidate.Target.GetZ() + 1.25f,
							player.GetInstanceId(), Aion.GameServer.GeoEngine.Collision.IgnoreProperties.Of(player.GetRace())))
					.First(candidate => aggressive.All(npc =>
						Math.Pow(npc.GetX() - candidate.Point.X, 2) + Math.Pow(npc.GetY() - candidate.Point.Y, 2) +
						Math.Pow(npc.GetZ() - candidate.Point.Z, 2) > Math.Pow(npc.GetAggroRange() + 10, 2)));
				target = approach.Target;
				await TeleportForSetupAsync(session, player, target.GetWorldId(), approach.Point.X - 0.5f, approach.Point.Y, approach.Point.Z, token);
				await session.MoveToPositionAsync(approach.Point, token);
				Assert.True(Aion.GameServer.World.Geo.GeoService.GetInstance().CanSee(player, target), "C11 setup must provide an unobstructed cast, not disable LOS.");
			}

			int[] actualActive = player.GetSkillList().GetAllSkills()
				.Where(skill => skill.IsNormalSkill() && !skill.GetSkillTemplate().IsPassive())
				.Select(skill => skill.GetSkillId())
				.Where(skillId => skillId is not 243 and not 245 and not 302)
				.OrderBy(id => id)
				.ToArray();
			Assert.Equal(entry.ActiveSkills.OrderBy(id => id), actualActive);

			foreach (int skillId in entry.ActiveSkills)
			{
				var skillEntry = player.GetSkillList().GetSkillEntry(skillId);
				var template = skillEntry.GetSkillTemplate();
				bool selfTarget = template.GetProperties()?.GetFirstTarget() is
					Aion.GameServer.SkillEngine.Properties.FirstTargetAttribute.ME or
					Aion.GameServer.SkillEngine.Properties.FirstTargetAttribute.TARGETORME;
				int targetId = selfTarget ? player.GetObjectId() : target.GetObjectId();
				if (!selfTarget)
				{
					target.GetLifeStats().SetCurrentHp(target.GetLifeStats().GetMaxHp());
					await session.SendPacketAsync(session.Api.Target(targetId), token);
				}
				else if (skillId == 1838)
				{
					player.GetLifeStats().SetCurrentHp(Math.Max(1, player.GetLifeStats().GetMaxHp() / 2));
				}

				session.BeginStep($"s{classIndex + 2:D2}", $"cast-{entry.Item1}-{skillId}");
				var cast = new SpellCastData((ushort)skillId, (byte)skillEntry.GetSkillLevel(), 0)
				{
					TargetObjectId = targetId,
				};
				using var castTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
				castTimeout.CancelAfter(TimeSpan.FromSeconds(5));
				try
				{
					bool completed = false;
					for (int attempt = 0; attempt < 3 && !completed; attempt++)
					{
						int firstPacket = session.PacketHistory.Count;
						await session.SendPacketAsync(GameClientPackets.CastSpell(cast), token);
						if (template.GetDuration() > 0)
						{
							await session.WaitForPacketAsync(typeof(SM_CASTSPELL), castTimeout.Token,
								packet => packet.Get<ushort>("spellId") == skillId);
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(template.GetDuration() + 1), token);
						}
						var outcome = await session.WaitForPacketAsync(packet =>
							(packet.PacketType == typeof(SM_CASTSPELL_RESULT) && packet.Get<ushort>("skillId") == skillId) ||
							(packet.PacketType == typeof(SM_SKILL_CANCEL) && packet.Get<ushort>("skillId") == skillId &&
								packet.Get<int>("objectId") == player.GetObjectId()), castTimeout.Token);
						completed = outcome.PacketType == typeof(SM_CASTSPELL_RESULT);
						if (!completed)
						{
							// Java CreatureController.onAttack legitimately interrupts casts. Retry only an observed
							// combat cancellation, never a timeout/refusal; still require a completed cast within 3 tries.
							Assert.Contains(session.PacketHistory.Skip(firstPacket), packet => packet.PacketType == typeof(SM_ATTACK));
							await session.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), castTimeout.Token,
								packet => packet.Get<string?>("name") == "STR_SKILL_CANCELED");
							Console.WriteLine($"C11 {entry.Item1} skill {skillId}: combat interruption on attempt {attempt + 1}.");
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(2_001), token);
						}
					}
					Assert.True(completed, $"{entry.Item1} skill {skillId} did not complete within three combat attempts.");
				}
				catch (OperationCanceledException) when (!token.IsCancellationRequested)
				{
					DecodedBotServerPacket? systemMessage = session.PacketHistory.LastOrDefault(
						packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE));
					throw new Xunit.Sdk.XunitException(
						$"{entry.Item1} skill {skillId} did not complete. " +
						$"System message: {systemMessage?.Get<string?>("name") ?? "none"}. " +
						$"Packets: {string.Join(", ", session.PacketTypes.TakeLast(12))}");
				}
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(2_001), token);
			}
		}

		policy.AssertClean();
	}

	private async Task RunC12Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 31, "Asimcao", Race.ELYOS, PlayerClass.MAGE, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.SPIRIT_MASTER, false, true));
		player.GetCommonData().SetLevel(10);
		Assert.True(player.GetSkillList().IsSkillPresent(3706));
		Assert.True(player.GetSkillList().IsSkillPresent(3837));

		Npc attackTarget = FindLivingNpc(player, 210119);
		await PlaceBesideNpcAsync(session, player, attackTarget, token);
		attackTarget.GetLifeStats().SetCurrentHp(attackTarget.GetLifeStats().GetMaxHp());

		session.BeginStep("s02", "summon-fire-spirit");
		await CastAndFinishAsync(session, player, 3706, player.GetObjectId(), token);
		Summon firstSummon = Assert.IsType<Summon>(player.GetSummon());
		Assert.Equal(833343, firstSummon.GetNpcId());
		Assert.NotNull(fixture.World.FindVisibleObject(firstSummon.GetObjectId()));

		session.BeginStep("s03", "summon-auto-attack");
		int hpBeforeAttack = attackTarget.GetLifeStats().GetCurrentHp();
		await session.SendPacketAsync(
			session.Api.SummonAttack(firstSummon.GetObjectId(), attackTarget.GetObjectId()), token);
		await session.WaitForPacketAsync(typeof(SM_ATTACK), token);
		Assert.True(attackTarget.GetLifeStats().GetCurrentHp() < hpBeforeAttack);
		Assert.False(attackTarget.IsDead());

		Npc skillTarget = attackTarget;
		await session.SendPacketAsync(session.Api.Target(skillTarget.GetObjectId()), token);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(10_001), token);

		session.BeginStep("s04", "summon-order-and-cast-skill");
		await CastAndFinishAsync(session, player, 3837, skillTarget.GetObjectId(), token);
		int? mappedPetSkillId = DataManager.PET_SKILL_DATA.GetPetOrderSkill(3837, firstSummon.GetNpcId());
		Assert.True(mappedPetSkillId.HasValue);
		int petSkillId = mappedPetSkillId.GetValueOrDefault();
		Assert.Equal(22216, petSkillId);
		int hpBeforeSkill = skillTarget.GetLifeStats().GetCurrentHp();
		await session.SendPacketAsync(
			session.Api.SummonCast(firstSummon.GetObjectId(), (ushort)petSkillId, 1, skillTarget.GetObjectId()), token);
		await session.WaitForPacketAsync(typeof(SM_CASTSPELL_RESULT), token,
			packet => packet.Get<ushort>("skillId") == petSkillId);
		Assert.True(skillTarget.GetLifeStats().GetCurrentHp() < hpBeforeSkill);

		session.BeginStep("s05", "dismiss-summon");
		int firstSummonId = firstSummon.GetObjectId();
		await session.SendPacketAsync(
			session.Api.SummonCommand((byte)SummonMode.RELEASE, targetObjectId: 0), token);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(3_001), token);
		await session.WaitForPacketAsync(typeof(SM_RESET_SKILL_COOLDOWN), token);
		Assert.Null(player.GetSummon());
		Assert.Null(fixture.World.FindVisibleObject(firstSummonId));

		session.BeginStep("s06", "summon-dies");
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(5_001), token);
		await CastAndFinishAsync(session, player, 3706, player.GetObjectId(), token);
		Summon doomedSummon = Assert.IsType<Summon>(player.GetSummon());
		int doomedSummonId = doomedSummon.GetObjectId();
		Npc killer = FindLivingNpc(player, 210119);
		doomedSummon.GetLifeStats().SetCurrentHp(1);
		for (int attack = 0; attack < 10 && !doomedSummon.IsDead(); attack++)
		{
			killer.GetController().AttackTarget(doomedSummon, 0, true);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(2_500), token);
		}
		Assert.True(doomedSummon.IsDead());
		await session.WaitForPacketAsync(typeof(SM_RESET_SKILL_COOLDOWN), token);
		Assert.Null(player.GetSummon());
		Assert.Null(fixture.World.FindVisibleObject(doomedSummonId));
		policy.AssertClean();
	}

	private async Task RunC13Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session priestSession = await EnterCombatWorldAsync(
			policy, accountId: 32, "Asimcap", Race.ELYOS, PlayerClass.PRIEST, token);
		await using SimulationL0Session victimSession = await EnterCombatWorldAsync(
			policy, accountId: 33, "Asimcaq", Race.ELYOS, PlayerClass.WARRIOR, token);
		Player priest = fixture.World.GetPlayer(priestSession.CharacterId);
		Player victim = fixture.World.GetPlayer(victimSession.CharacterId);
		Assert.True(ClassChangeService.SetClass(priest, PlayerClass.CLERIC, false, true));
		priest.GetCommonData().SetLevel(10);
		Assert.True(priest.GetSkillList().IsSkillPresent(1699));

		Npc attacker = FindLivingNpc(victim, 210119);
		await PlaceBesideNpcAsync(victimSession, victim, attacker, token);
		await TeleportForSetupAsync(priestSession, priest, attacker.GetWorldId(),
			attacker.GetX() - 2, attacker.GetY(), attacker.GetZ(), token);

		priestSession.BeginStep("s02", "cleric-offers-skill-revive");
		await KillPlayerAsync(victimSession, victim, attacker, token);
		await priestSession.SendPacketAsync(priestSession.Api.Target(victim.GetObjectId()), token);
		await CastAndFinishAsync(priestSession, priest, 1699, victim.GetObjectId(), token);
		Assert.True(victim.GetResStatus());
		await victimSession.SendPacketAsync(victimSession.Api.Revive(BotReviveType.Skill), token);
		await victimSession.WaitForPacketAsync(typeof(SM_EMOTION), token);
		Assert.False(victim.IsDead());
		Assert.InRange(victim.GetLifeStats().GetCurrentHp(),
			victim.GetLifeStats().GetMaxHp() * 35 / 100,
			(int)Math.Ceiling(victim.GetLifeStats().GetMaxHp() * 0.35));

		victimSession.BeginStep("s03", "item-self-revive");
		Assert.Equal(0, ItemService.AddItem(victim, 161001001, 1));
		Item selfRevive = Assert.IsType<Item>(victim.GetInventory().GetFirstItemByItemId(161001001));
		await KillPlayerAsync(victimSession, victim, attacker, token);
		await victimSession.SendPacketAsync(victimSession.Api.Revive(BotReviveType.SelfReviveItem), token);
		await victimSession.WaitForPacketAsync(typeof(SM_ITEM_USAGE_ANIMATION), token);
		await victimSession.WaitForPacketAsync(typeof(SM_EMOTION), token);
		Assert.False(victim.IsDead());
		Assert.Null(victim.GetInventory().GetItemByObjId(selfRevive.GetObjectId()));
		Assert.InRange(victim.GetLifeStats().GetCurrentHp(),
			victim.GetLifeStats().GetMaxHp() * 15 / 100,
			(int)Math.Ceiling(victim.GetLifeStats().GetMaxHp() * 0.15));

		victimSession.BeginStep("s04", "place-and-bind-personal-kisk");
		Assert.Equal(0, ItemService.AddItem(victim, 184000017, 1));
		Item kiskItem = Assert.IsType<Item>(victim.GetInventory().GetFirstItemByItemId(184000017));
		await victimSession.SendPacketAsync(
			victimSession.Api.UseItem(kiskItem.GetObjectId(), kiskItem.GetItemTemplate()), token);
		await victimSession.WaitForPacketAsync(typeof(SM_ITEM_USAGE_ANIMATION), token);
		await victimSession.AdvanceAsync(TimeSpan.FromMilliseconds(10_001), token);
		await victimSession.WaitForPacketAsync(typeof(SM_ITEM_USAGE_ANIMATION), token);
		Kisk kisk = Assert.IsType<Kisk>(victim.GetKisk());
		Assert.True(kisk.IsActive());
		Assert.Contains(victim.GetObjectId(), kisk.GetCurrentMemberIds());
		int remainingResurrections = kisk.GetRemainingResurrects();

		victimSession.BeginStep("s05", "kisk-revive");
		await KillPlayerAsync(victimSession, victim, attacker, token);
		DecodedBotServerPacket kiskDeath = victimSession.PacketHistory.Last(
			packet => packet.PacketType == typeof(SM_DIE));
		Assert.True(kiskDeath.Get<int>("remainingKiskTimeSeconds") > 0);
		await victimSession.SendPacketAsync(victimSession.Api.Revive(BotReviveType.Kisk), token);
		await victimSession.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
		Assert.False(victim.IsDead());
		Assert.Equal(remainingResurrections - 1, kisk.GetRemainingResurrects());
		Assert.InRange(PositionUtil.GetDistance(victim, kisk), 0, 1.5f);

		victimSession.BeginStep("s06", "bind-at-obelisk");
		Npc obelisk = victim.GetPosition().GetWorldMapInstance().GetNpcs(700013).Single();
		await PlaceBesideNpcAsync(victimSession, victim, obelisk, token);
		victim.GetInventory().IncreaseKinah(47);
		await victimSession.SendPacketAsync(victimSession.Api.TalkTo(obelisk.GetObjectId()), token);
		DecodedBotServerPacket bindQuestion = await victimSession.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token);
		await victimSession.SendPacketAsync(GameClientPackets.QuestionResponse(
			bindQuestion.Get<int>("code"), response: 1, bindQuestion.Get<int>("senderId")), token);
		await victimSession.WaitForPacketAsync(typeof(SM_BIND_POINT_INFO), token);
		var bindPoint = Assert.IsType<BindPointPosition>(victim.GetBindPoint());
		Assert.Equal(210010000, bindPoint.GetMapId());
		Assert.InRange(PositionUtil.GetDistance(bindPoint.GetX(), bindPoint.GetY(), bindPoint.GetZ(),
			victim.GetX(), victim.GetY(), victim.GetZ()), 0, 0.1f);

		victimSession.BeginStep("s07", "obelisk-revive");
		Npc finalAttacker = FindLivingNpc(victim, 210119);
		await PlaceBesideNpcAsync(victimSession, victim, finalAttacker, token);
		await KillPlayerAsync(victimSession, victim, finalAttacker, token);
		await victimSession.SendPacketAsync(victimSession.Api.Revive(BotReviveType.Obelisk), token);
		await victimSession.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
		Assert.False(victim.IsDead());
		Assert.InRange(PositionUtil.GetDistance(bindPoint.GetX(), bindPoint.GetY(), bindPoint.GetZ(),
			victim.GetX(), victim.GetY(), victim.GetZ()), 0, 1.5f);
		policy.AssertClean();
	}

	private static async Task KillPlayerAsync(
		SimulationL0Session session,
		Player player,
		Npc attacker,
		CancellationToken token)
	{
		player.GetLifeStats().SetCurrentHp(1);
		for (int attack = 0; attack < 10 && !player.IsDead(); attack++)
		{
			attacker.GetController().AttackTarget(player, 0, true);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
		}
		Assert.True(player.IsDead());
		await session.WaitForPacketAsync(typeof(SM_DIE), token);
	}

	private async Task RunC14Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using SimulationL0Session session = await EnterCombatWorldAsync(
			policy, accountId: 34, "Asimcar", Race.ELYOS, PlayerClass.WARRIOR, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		PlayerCommonData commonData = player.GetCommonData();
		commonData.SetLevel(6);
		long startExp = DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(6);
		long expNeed = commonData.GetExpNeed();
		commonData.SetExp(startExp + expNeed / 2);
		long expBeforeDeath = commonData.GetExp();
		long totalLoss = Aion.GameServer.Utils.Stats.XPLossEnumExtensions.GetExpLoss(6, expNeed);
		long unrecoverableLoss = (int)(totalLoss * 0.33333333);
		long expectedRecoverable = totalLoss - unrecoverableLoss;

		session.BeginStep("s02", "level-six-death-loses-experience");
		Npc attacker = FindLivingNpc(player, 210119);
		await PlaceBesideNpcAsync(session, player, attacker, token);
		await KillPlayerAsync(session, player, attacker, token);
		Assert.Equal(expectedRecoverable, commonData.GetExpRecoverable());
		Assert.Equal(expBeforeDeath - totalLoss, commonData.GetExp());

		session.BeginStep("s03", "bind-revive-with-soul-sickness");
		await session.SendPacketAsync(session.Api.Revive(), token);
		await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
		Assert.True(player.GetEffectController().HasAbnormalEffect(8291));
		Assert.True(commonData.GetDeathCount() > 0);

		session.BeginStep("s04", "recover-at-soul-healer");
		Npc healer = player.GetPosition().GetWorldMapInstance().GetNpcs(203064).Single();
		await PlaceBesideNpcAsync(session, player, healer, token);
		int price = (int)(expectedRecoverable * (0.25 - 0.00000015 * expectedRecoverable));
		player.GetInventory().IncreaseKinah(price);
		long kinahBefore = player.GetInventory().GetKinah();
		await session.SendPacketAsync(session.Api.SelectDialog(healer.GetObjectId(), DialogAction.RECOVERY), token);
		DecodedBotServerPacket question = await session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token,
			packet => packet.Get<int>("code") == SM_QUESTION_WINDOW.STR_ASK_RECOVER_EXPERIENCE);
		Assert.Equal(price.ToString(), question.Get<string?[]>("params")[0]);
		await session.SendPacketAsync(GameClientPackets.QuestionResponse(
			question.Get<int>("code"), response: 1, question.Get<int>("senderId")), token);
		await session.WaitForPacketAsync(typeof(SM_STATUPDATE_EXP), token);
		Assert.Equal(0, commonData.GetExpRecoverable());
		Assert.Equal(expBeforeDeath - unrecoverableLoss, commonData.GetExp());
		Assert.Equal(kinahBefore - price, player.GetInventory().GetKinah());
		Assert.False(player.GetEffectController().HasAbnormalEffect(8291));
		Assert.Equal(0, commonData.GetDeathCount());
		policy.AssertClean();
	}

	private async Task RunC15Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		bool originalSimpleSecondClass = CustomConfig.ENABLE_SIMPLE_2NDCLASS;
		CustomConfig.ENABLE_SIMPLE_2NDCLASS = true;
		try
		{
			await using SimulationL0Session session = await EnterCombatWorldAsync(
				policy, accountId: 35, "Asimcas", Race.ELYOS, PlayerClass.SCOUT, token);
			Player player = fixture.World.GetPlayer(session.CharacterId);
			player.GetCommonData().SetLevel(9);
			Assert.Equal(9, player.GetLevel());

			session.BeginStep("s02", "choose-ranger-at-level-nine");
			ClassChangeService.ShowClassChangeDialog(player);
			await session.SendPacketAsync(
				session.Api.SelectDialog(0, DialogAction.SELECT6_2, questId: 1006), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
				packet => packet.Get<ushort>("dialogPageId") == 0);
			DecodedBotServerPacket selection = session.PacketHistory.Last(packet =>
				packet.PacketType == typeof(SM_DIALOG_WINDOW) &&
				packet.Get<ushort>("dialogPageId") ==
					ClassChangeService.GetClassSelectionDialogPageId(Race.ELYOS, PlayerClass.SCOUT));
			Assert.Equal(1006, selection.Get<int>("questId"));
			Assert.Equal(PlayerClass.RANGER, player.GetPlayerClass());
			Assert.True(player.GetCommonData().IsDaeva());
			Assert.Equal(QuestStatus.COMPLETE,
				player.GetQuestStateList().GetQuestState(1006).GetStatus());

			session.BeginStep("s03", "learn-white-tiger-from-skill-book");
			player.GetCommonData().SetLevel(10);
			const int skillBookItemId = 169500916;
			const int learnedSkillId = 1;
			Assert.False(player.GetSkillList().IsSkillPresent(learnedSkillId));
			Assert.Equal(0, ItemService.AddItem(player, skillBookItemId, 1));
			Item skillBook = Assert.IsType<Item>(player.GetInventory().GetFirstItemByItemId(skillBookItemId));
			await session.SendPacketAsync(
				session.Api.UseItem(skillBook.GetObjectId(), skillBook.GetItemTemplate()), token);
			await session.WaitForPacketAsync(typeof(SM_SKILL_LIST), token);
			await session.WaitForPacketAsync(typeof(SM_ITEM_USAGE_ANIMATION), token);
			Assert.True(player.GetSkillList().IsSkillPresent(learnedSkillId));
			Assert.Null(player.GetInventory().GetItemByObjId(skillBook.GetObjectId()));
			policy.AssertClean();
		}
		finally
		{
			CustomConfig.ENABLE_SIMPLE_2NDCLASS = originalSimpleSecondClass;
		}
	}

	private static async Task CastAndFinishAsync(
		SimulationL0Session session,
		Player player,
		int skillId,
		int targetId,
		CancellationToken token)
	{
		var skillEntry = player.GetSkillList().GetSkillEntry(skillId);
		var template = skillEntry.GetSkillTemplate();
		var cast = new SpellCastData((ushort)skillId, (byte)skillEntry.GetSkillLevel(), 0)
		{
			TargetObjectId = targetId,
		};
		await session.SendPacketAsync(GameClientPackets.CastSpell(cast), token);
		using var castTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
		castTimeout.CancelAfter(TimeSpan.FromSeconds(5));
		try
		{
			if (template.GetDuration() > 0)
			{
				await session.WaitForPacketAsync(typeof(SM_CASTSPELL), castTimeout.Token,
					packet => packet.Get<ushort>("spellId") == skillId);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(template.GetDuration() + 1), token);
			}
			DecodedBotServerPacket result = await session.WaitForPacketAsync(typeof(SM_CASTSPELL_RESULT), castTimeout.Token,
				packet => packet.Get<ushort>("skillId") == skillId);
			ushort hitTime = result.Get<ushort>("hitTime");
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(hitTime + 1), token);
		}
		catch (OperationCanceledException) when (!token.IsCancellationRequested)
		{
			DecodedBotServerPacket? message = session.PacketHistory.LastOrDefault(
				packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE));
			throw new Xunit.Sdk.XunitException(
				$"Skill {skillId} did not complete; system message " +
				$"{message?.Get<string?>("name") ?? "none"}. Packets: {string.Join(", ", session.PacketTypes.TakeLast(12))}");
		}
	}

	private async Task KillNpcAsync(
		SimulationL0Session session,
		Player player,
		Npc npc,
		CancellationToken token)
	{
		npc.GetLifeStats().SetCurrentHp(1);
		await session.SendPacketAsync(session.Api.Target(npc.GetObjectId()), token);
		await session.SendPacketAsync(
			session.Api.Attack(npc.GetObjectId(), player.GetGameStats().GetAttackSpeed().GetCurrent()), token);
		await session.WaitForPacketAsync(typeof(SM_LOOT_STATUS), token,
			packet => packet.Get<int>("targetObjectId") == npc.GetObjectId() &&
				packet.Get<byte>("status") == (byte)SM_LOOT_STATUS.Status.LOOT_ENABLE);
		Assert.True(npc.IsDead());
	}

	private async Task<SimulationL0Session> EnterCombatWorldAsync(
		SimulationLogPolicy policy,
		int accountId,
		string characterName,
		Race race,
		PlayerClass playerClass,
		CancellationToken token, string bot = "b01")
	{
		var session = new SimulationL0Session(fixture, policy, bot, accountId, characterName, race);
		try
		{
			session.BeginStep("s01", $"login-create-{playerClass.ToString().ToLowerInvariant()}-enter");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token, playerClass);
			await session.EnterWorldAsync(token);
			await session.WaitForPrologueAsync(token);
			return session;
		}
		catch
		{
			await session.DisposeAsync();
			throw;
		}
	}

	private static Npc FindLivingNpc(Player player, int templateId) =>
		player.GetPosition().GetWorldMapInstance().GetNpcs(templateId)
			.Where(npc => npc.IsSpawned() && !npc.IsDead())
			.OrderBy(npc => MathF.Pow(npc.GetX() - player.GetX(), 2) + MathF.Pow(npc.GetY() - player.GetY(), 2))
			.First();

	private async Task PlaceBesideNpcAsync(
		SimulationL0Session session,
		Player player,
		Npc npc,
		CancellationToken token)
	{
		await TeleportForSetupAsync(session, player, npc.GetWorldId(), npc.GetX() - 1, npc.GetY(), npc.GetZ(), token);
	}
}
