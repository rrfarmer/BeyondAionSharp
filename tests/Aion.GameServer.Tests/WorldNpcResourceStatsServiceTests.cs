using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcResourceStatsServiceTests
{
	[Fact]
	public async Task ReduceNpcMpAsync_ReducesMpAndBroadcastsDamagePacket()
	{
		var service = CreateService(out var lifeStats, out var registry);
		var npc = CreateNpc(objectId: 10, maxHp: 100);
		lifeStats.Initialize(npc, maxHp: 100, maxMp: 80);

		var result = await service.ReduceNpcMpAsync(npc, value: 25, skillId: 7301);

		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Mp, result.ResourceType);
		Assert.Equal(WorldNpcResourceChangeKind.Reduce, result.ChangeKind);
		Assert.Equal(80, result.PreviousValue);
		Assert.Equal(55, result.CurrentValue);
		Assert.Equal(25, result.AppliedValue);
		Assert.Equal(80, result.MaxValue);
		Assert.NotNull(result.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.DamageMp, result.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.MpAttack, result.AttackStatusPacket.Log);
		Assert.Equal(25, result.AttackStatusPacket.Value);
		Assert.Equal(68, result.AttackStatusPacket.HpOrMpPercentage);
		Assert.Equal(1, result.AttackStatusBroadcastCount);
		Assert.Single(registry.Broadcasts);
		Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
		Assert.Equal(55, stored!.CurrentMp);
	}

	[Fact]
	public async Task IncreaseNpcMpAsync_CapsToMaxAndSendsSkillPacketWhenValueDoesNotChange()
	{
		var service = CreateService(out var lifeStats, out var registry);
		var npc = CreateNpc(objectId: 11, maxHp: 100);
		lifeStats.Initialize(npc, maxHp: 100, maxMp: 100);
		await service.ReduceNpcMpAsync(npc, value: 20);

		var capped = await service.IncreaseNpcMpAsync(npc, value: 50, skillId: 7302);

		Assert.Equal(WorldNpcResourceChangeStatus.Increased, capped.Status);
		Assert.Equal(80, capped.PreviousValue);
		Assert.Equal(100, capped.CurrentValue);
		Assert.Equal(20, capped.AppliedValue);
		Assert.NotNull(capped.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Mp, capped.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.MpHeal, capped.AttackStatusPacket.Log);
		Assert.Equal(20, capped.AttackStatusPacket.Value);
		Assert.Equal(100, capped.AttackStatusPacket.HpOrMpPercentage);

		var packetOnly = await service.IncreaseNpcMpAsync(npc, value: 5, skillId: 7303);

		Assert.Equal(WorldNpcResourceChangeStatus.NoChange, packetOnly.Status);
		Assert.Equal(0, packetOnly.AppliedValue);
		Assert.NotNull(packetOnly.AttackStatusPacket);
		Assert.Equal(0, packetOnly.AttackStatusPacket.Value);
		Assert.Equal(7303, packetOnly.AttackStatusPacket.SkillId);
		Assert.Equal(3, registry.Broadcasts.Count);
	}

	[Fact]
	public async Task ReducePlayerFpAsync_ClampsToZeroAndUsesHpPercentageWithFlyTimeIntent()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1001, currentHp: 80, currentMp: 40, currentFp: 10);

		var result = await service.ReducePlayerFpAsync(player, maxHp: 100, maxFp: 100, value: 25, skillId: 7304);

		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Fp, result.ResourceType);
		Assert.Equal(10, result.PreviousValue);
		Assert.Equal(0, result.CurrentValue);
		Assert.Equal(10, result.AppliedValue);
		Assert.True(result.SendFlyTimeUpdate);
		Assert.NotNull(result.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.FpDamage, result.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.FpAttack, result.AttackStatusPacket.Log);
		Assert.Equal(10, result.AttackStatusPacket.Value);
		Assert.Equal(80, result.AttackStatusPacket.HpOrMpPercentage);
		Assert.Equal(0, player.LifeStats!.CurrentFp);
		Assert.Single(registry.Broadcasts);
	}

	[Fact]
	public async Task IncreasePlayerFpAsync_CapsToMaxAndSkipsPacketWhenFull()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1002, currentHp: 100, currentMp: 50, currentFp: 90);

		var capped = await service.IncreasePlayerFpAsync(player, maxHp: 100, maxFp: 100, value: 25, skillId: 7305);

		Assert.Equal(WorldNpcResourceChangeStatus.Increased, capped.Status);
		Assert.Equal(90, capped.PreviousValue);
		Assert.Equal(100, capped.CurrentValue);
		Assert.Equal(10, capped.AppliedValue);
		Assert.True(capped.SendFlyTimeUpdate);
		Assert.NotNull(capped.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Fp, capped.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.FpHeal, capped.AttackStatusPacket.Log);
		Assert.Equal(10, capped.AttackStatusPacket.Value);

		var full = await service.IncreasePlayerFpAsync(player, maxHp: 100, maxFp: 100, value: 5, skillId: 7306);

		Assert.Equal(WorldNpcResourceChangeStatus.NoChange, full.Status);
		Assert.False(full.SendFlyTimeUpdate);
		Assert.Null(full.AttackStatusPacket);
		Assert.Single(registry.Broadcasts);
	}

	[Fact]
	public void AddPlayerDp_CapsOnlinePlayerAndRecordsDpPacketIntents()
	{
		var service = CreateService(out _, out _);
		var player = CreatePlayer(objectId: 1003, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "RANGER", dp: 3900);
		player.IsOnline = true;

		var result = service.AddPlayerDp(player, value: 250, maxDp: 4000);

		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Dp, result.ResourceType);
		Assert.Equal(3900, result.PreviousValue);
		Assert.Equal(4000, result.CurrentValue);
		Assert.Equal(100, result.AppliedValue);
		Assert.True(result.BroadcastDpInfo);
		Assert.True(result.SendDpStatUpdate);
		Assert.True(result.UpdateStatsAndSpeedVisually);
		Assert.Equal(4000, player.Dp);
	}

	[Fact]
	public void AddPlayerDp_SkipsStartingClassAndRequiresOnlineMaxDp()
	{
		var service = CreateService(out _, out _);
		var startingClass = CreatePlayer(objectId: 1004, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "WARRIOR", dp: 500);
		startingClass.IsOnline = true;

		var skipped = service.AddPlayerDp(startingClass, value: 100, maxDp: 4000);

		Assert.Equal(WorldNpcResourceChangeStatus.StartingClass, skipped.Status);
		Assert.Equal(500, startingClass.Dp);

		var missingMax = CreatePlayer(objectId: 1005, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "RANGER", dp: 500);
		missingMax.IsOnline = true;

		var unresolved = service.AddPlayerDp(missingMax, value: 100);

		Assert.Equal(WorldNpcResourceChangeStatus.MissingMaxResource, unresolved.Status);
		Assert.Equal(500, missingMax.Dp);
	}

	[Fact]
	public async Task ApplyResourceOverTimePeriodicResultAsync_ReducesNpcMpFromStagedMpAttack()
	{
		var service = CreateService(out var lifeStats, out _);
		var skillDamage = CreateSkillDamageService();
		var npc = CreateNpc(objectId: 12, maxHp: 100);
		lifeStats.Initialize(npc, maxHp: 100, maxMp: 250);
		var staged = skillDamage.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
			WorldNpcSkillResourceOverTimeEffectKind.MpAttack,
			Value: 20,
			SkillId: 7401,
			CurrentResource: 250,
			MaxResource: 250,
			Percent: true));

		var result = await service.ApplyResourceOverTimePeriodicResultAsync(
			staged,
			new WorldNpcResourceMutationTarget(Npc: npc));

		Assert.Equal(WorldNpcResourceEffectApplicationStatus.Applied, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Mp, result.ResourceType);
		Assert.Equal(7401, result.SkillId);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, result.Change.Status);
		Assert.Equal(50, result.Change.AppliedValue);
		Assert.Equal(200, result.Change.CurrentValue);
		Assert.NotNull(result.Change.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.DamageMp, result.Change.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.MpAttack, result.Change.AttackStatusPacket.Log);
		Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
		Assert.Equal(200, stored!.CurrentMp);
	}

	[Fact]
	public async Task ApplyResourceOverTimePeriodicResultAsync_IncreasesPlayerFpFromStagedFpHeal()
	{
		var service = CreateService(out _, out _);
		var skillDamage = CreateSkillDamageService();
		var player = CreatePlayer(objectId: 1006, currentHp: 100, currentMp: 40, currentFp: 80);
		var staged = skillDamage.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
			WorldNpcSkillResourceOverTimeEffectKind.FpHeal,
			Value: 25,
			SkillId: 7402,
			CurrentResource: 80,
			MaxResource: 100,
			TargetIsPlayer: true));

		var result = await service.ApplyResourceOverTimePeriodicResultAsync(
			staged,
			new WorldNpcResourceMutationTarget(Player: player, MaxHp: 100, MaxFp: 100));

		Assert.Equal(WorldNpcResourceEffectApplicationStatus.Applied, result.Status);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Change.Status);
		Assert.Equal(20, result.Change.AppliedValue);
		Assert.Equal(100, player.LifeStats!.CurrentFp);
		Assert.True(result.Change.SendFlyTimeUpdate);
		Assert.NotNull(result.Change.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Fp, result.Change.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.FpHeal, result.Change.AttackStatusPacket.Log);
		Assert.Equal(7402, result.Change.AttackStatusPacket.SkillId);
	}

	[Fact]
	public async Task ApplyInstantResourceResultAsync_ReducesPlayerMpFromStagedMpAttack()
	{
		var service = CreateService(out _, out _);
		var skillDamage = CreateSkillDamageService();
		var player = CreatePlayer(objectId: 1007, currentHp: 100, currentMp: 120, currentFp: 100);
		var staged = skillDamage.CalculateInstantResourceEffect(new WorldNpcSkillInstantResourceEffectRequest(
			WorldNpcSkillInstantResourceEffectKind.MpAttackInstant,
			Value: 25,
			SkillId: 7403,
			Position: 1,
			MaxResource: 200,
			Percent: true));

		var result = await service.ApplyInstantResourceResultAsync(
			staged,
			new WorldNpcResourceMutationTarget(Player: player, MaxMp: 200));

		Assert.Equal(WorldNpcResourceEffectApplicationStatus.Applied, result.Status);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, result.Change.Status);
		Assert.Equal(50, result.Change.AppliedValue);
		Assert.Equal(70, player.LifeStats!.CurrentMp);
		Assert.NotNull(result.Change.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.DamageMp, result.Change.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.MpAttack, result.Change.AttackStatusPacket.Log);
		Assert.Equal(35, result.Change.AttackStatusPacket.HpOrMpPercentage);
	}

	[Fact]
	public async Task ApplyInstantResourceResultAsync_RequiresPlayerForStagedFpAttack()
	{
		var service = CreateService(out _, out _);
		var skillDamage = CreateSkillDamageService();
		var npc = CreateNpc(objectId: 13, maxHp: 100);
		var staged = skillDamage.CalculateInstantResourceEffect(new WorldNpcSkillInstantResourceEffectRequest(
			WorldNpcSkillInstantResourceEffectKind.FpAttackInstant,
			Value: 10,
			SkillId: 7404,
			Position: 1,
			MaxResource: 100,
			TargetIsPlayer: true));

		var result = await service.ApplyInstantResourceResultAsync(
			staged,
			new WorldNpcResourceMutationTarget(Npc: npc));

		Assert.Equal(WorldNpcResourceEffectApplicationStatus.MissingTarget, result.Status);
		Assert.Null(result.Change);
	}

	private static WorldNpcResourceStatsService CreateService(
		out WorldNpcLifeStatsService lifeStats,
		out CapturingConnectionRegistry registry)
	{
		lifeStats = new WorldNpcLifeStatsService(new WorldNpcDeathDropWorkflowService(null!, null!));
		registry = new CapturingConnectionRegistry();
		return new WorldNpcResourceStatsService(lifeStats, registry);
	}

	private static WorldNpcSkillDamageService CreateSkillDamageService()
	{
		return new WorldNpcSkillDamageService(null!);
	}

	private static WorldNpc CreateNpc(int objectId, int maxHp)
	{
		return new WorldNpc(
			objectId,
			203110,
			CreateTemplate(203110, maxHp),
			new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static Player CreatePlayer(
		int objectId,
		int currentHp,
		int currentMp,
		int currentFp,
		string playerClass = "RANGER",
		int dp = 0)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = playerClass,
			Level = 10,
			Dp = dp,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(currentHp, currentMp, currentFp),
		};
	}

	private static NpcTemplateSummary CreateTemplate(int templateId, int maxHp)
	{
		return new NpcTemplateSummary(
			templateId,
			$"npc-{templateId}",
			NameId: templateId,
			Level: 10,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "ELYOS",
			Tribe: "GENERAL",
			Type: "GENERAL",
			MaxHp: maxHp);
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<BroadcastRecord> Broadcasts { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			return Task.FromResult(false);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			return Task.FromResult(1);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);
}
