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
	public async Task ReducePlayerMpAsync_SendsMpStatUpdateAndRestoreIntent()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1012, currentHp: 100, currentMp: 80, currentFp: 100);
		player.IsOnline = true;
		player.TeamMembership = PlayerTeamMembership.Group;

		var result = await service.ReducePlayerMpAsync(player, maxMp: 100, value: 30, skillId: 7508);

		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Mp, result.ResourceType);
		Assert.Equal(WorldNpcResourceChangeKind.Reduce, result.ChangeKind);
		Assert.Equal(80, result.PreviousValue);
		Assert.Equal(50, result.CurrentValue);
		Assert.Equal(30, result.AppliedValue);
		Assert.True(result.SendMpStatUpdate);
		Assert.True(result.MpStatUpdateSent);
		Assert.NotNull(result.MpStatUpdatePacket);
		Assert.Equal(50, result.MpStatUpdatePacket.CurrentMp);
		Assert.Equal(100, result.MpStatUpdatePacket.MaxMp);
		Assert.True(result.SendGroupStatUpdate);
		Assert.True(result.TriggerRestoreTask);
		Assert.NotNull(result.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.DamageMp, result.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.MpAttack, result.AttackStatusPacket.Log);
		Assert.Equal(30, result.AttackStatusPacket.Value);
		Assert.Equal(50, player.LifeStats!.CurrentMp);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
		Assert.Same(result.MpStatUpdatePacket, delivery.Packet);
	}

	[Fact]
	public async Task IncreasePlayerMpAsync_SendsMpStatUpdateWithoutRestoreIntent()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1013, currentHp: 100, currentMp: 50, currentFp: 100);
		player.IsOnline = true;
		player.TeamMembership = PlayerTeamMembership.Group;

		var result = await service.IncreasePlayerMpAsync(player, maxMp: 100, value: 25, skillId: 7509);

		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Mp, result.ResourceType);
		Assert.Equal(WorldNpcResourceChangeKind.Increase, result.ChangeKind);
		Assert.Equal(50, result.PreviousValue);
		Assert.Equal(75, result.CurrentValue);
		Assert.Equal(25, result.AppliedValue);
		Assert.True(result.SendMpStatUpdate);
		Assert.True(result.MpStatUpdateSent);
		Assert.NotNull(result.MpStatUpdatePacket);
		Assert.Equal(75, result.MpStatUpdatePacket.CurrentMp);
		Assert.Equal(100, result.MpStatUpdatePacket.MaxMp);
		Assert.True(result.SendGroupStatUpdate);
		Assert.False(result.TriggerRestoreTask);
		Assert.NotNull(result.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Mp, result.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.MpHeal, result.AttackStatusPacket.Log);
		Assert.Equal(25, result.AttackStatusPacket.Value);
		Assert.Equal(75, player.LifeStats!.CurrentMp);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
		Assert.Same(result.MpStatUpdatePacket, delivery.Packet);
	}

	[Fact]
	public async Task IncreaseNpcHpAsync_CapsToMaxAndBroadcastsHealPacket()
	{
		var service = CreateService(out var lifeStats, out var registry);
		var npc = CreateNpc(objectId: 14, maxHp: 100);
		lifeStats.Initialize(npc, maxHp: 100, maxMp: 40);
		await lifeStats.ReduceHpAsync(npc, damage: 30, maxHp: 100, maxMp: 40, attacker: CreatePlayer(objectId: 2001, currentHp: 100, currentMp: 50, currentFp: 100));

		var result = await service.IncreaseNpcHpAsync(npc, value: 50, skillId: 7501, killingBlow: 90);

		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Hp, result.ResourceType);
		Assert.Equal(WorldNpcResourceChangeKind.Increase, result.ChangeKind);
		Assert.Equal(70, result.PreviousValue);
		Assert.Equal(100, result.CurrentValue);
		Assert.Equal(30, result.AppliedValue);
		Assert.Equal(100, result.MaxValue);
		Assert.True(result.NotifyHpObservers);
		Assert.True(result.KillingBlowReset);
		Assert.NotNull(result.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Hp, result.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.Heal, result.AttackStatusPacket.Log);
		Assert.Equal(30, result.AttackStatusPacket.Value);
		Assert.Equal(100, result.AttackStatusPacket.HpOrMpPercentage);
		Assert.Equal(1, result.AttackStatusBroadcastCount);
		Assert.Single(registry.Broadcasts);
		Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
		Assert.Equal(100, stored!.CurrentHp);
	}

	[Fact]
	public async Task IncreaseNpcHpAsync_BlocksDiseaseBeforePacketEvenForSkillHeal()
	{
		var service = CreateService(out var lifeStats, out var registry);
		var npc = CreateNpc(objectId: 15, maxHp: 100);
		lifeStats.Initialize(npc, maxHp: 100, maxMp: 40);
		await lifeStats.ReduceHpAsync(npc, damage: 30, maxHp: 100, maxMp: 40, attacker: CreatePlayer(objectId: 2002, currentHp: 100, currentMp: 50, currentFp: 100));

		var result = await service.IncreaseNpcHpAsync(npc, value: 25, skillId: 7502, targetHasDisease: true);

		Assert.Equal(WorldNpcResourceChangeStatus.BlockedByDisease, result.Status);
		Assert.Equal(70, result.PreviousValue);
		Assert.Equal(70, result.CurrentValue);
		Assert.Equal(0, result.AppliedValue);
		Assert.False(result.NotifyHpObservers);
		Assert.Null(result.AttackStatusPacket);
		Assert.Empty(registry.Broadcasts);
		Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
		Assert.Equal(70, stored!.CurrentHp);
	}

	[Fact]
	public async Task IncreaseNpcHpAsync_RoutesNegativeHealToHpDamage()
	{
		var service = CreateService(out var lifeStats, out var registry);
		var npc = CreateNpc(objectId: 16, maxHp: 100);
		lifeStats.Initialize(npc, maxHp: 100, maxMp: 40);

		var result = await service.IncreaseNpcHpAsync(npc, value: -25, skillId: 7503);

		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Hp, result.ResourceType);
		Assert.Equal(WorldNpcResourceChangeKind.Reduce, result.ChangeKind);
		Assert.Equal(100, result.PreviousValue);
		Assert.Equal(75, result.CurrentValue);
		Assert.Equal(25, result.AppliedValue);
		Assert.True(result.NotifyHpObservers);
		Assert.True(result.RoutedNegativeHealToDamage);
		Assert.NotNull(result.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Hp, result.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.Heal, result.AttackStatusPacket.Log);
		Assert.Equal(25, result.AttackStatusPacket.Value);
		Assert.Equal(75, result.AttackStatusPacket.HpOrMpPercentage);
		Assert.Single(registry.Broadcasts);
		Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
		Assert.Equal(75, stored!.CurrentHp);
	}

	[Fact]
	public async Task IncreaseNpcHpAsync_SendsSkillPacketWhenHealDoesNotChangeHp()
	{
		var service = CreateService(out var lifeStats, out var registry);
		var npc = CreateNpc(objectId: 17, maxHp: 100);
		lifeStats.Initialize(npc, maxHp: 100, maxMp: 40);

		var result = await service.IncreaseNpcHpAsync(npc, value: 5, skillId: 7506);

		Assert.Equal(WorldNpcResourceChangeStatus.NoChange, result.Status);
		Assert.Equal(100, result.PreviousValue);
		Assert.Equal(100, result.CurrentValue);
		Assert.Equal(0, result.AppliedValue);
		Assert.False(result.NotifyHpObservers);
		Assert.NotNull(result.AttackStatusPacket);
		Assert.Equal(0, result.AttackStatusPacket.Value);
		Assert.Equal(7506, result.AttackStatusPacket.SkillId);
		Assert.Single(registry.Broadcasts);
	}

	[Fact]
	public async Task IncreasePlayerHpAsync_CapsHealAndRecordsPlayerSideEffectIntents()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1000, currentHp: 80, currentMp: 40, currentFp: 100);
		player.IsOnline = true;
		player.TeamMembership = PlayerTeamMembership.Group;

		var result = await service.IncreasePlayerHpAsync(player, maxHp: 100, value: 30, skillId: 7504, killingBlow: 90);

		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Hp, result.ResourceType);
		Assert.Equal(WorldNpcResourceChangeKind.Increase, result.ChangeKind);
		Assert.Equal(80, result.PreviousValue);
		Assert.Equal(100, result.CurrentValue);
		Assert.Equal(20, result.AppliedValue);
		Assert.True(result.SendHpStatUpdate);
		Assert.True(result.SendGroupStatUpdate);
		Assert.True(result.NotifyHpObservers);
		Assert.True(result.ClearAggroOnFullHp);
		Assert.True(result.KillingBlowReset);
		Assert.True(result.HpStatUpdateSent);
		Assert.NotNull(result.HpStatUpdatePacket);
		Assert.Equal(100, result.HpStatUpdatePacket.CurrentHp);
		Assert.Equal(100, result.HpStatUpdatePacket.MaxHp);
		Assert.Equal(100, player.LifeStats!.CurrentHp);
		Assert.NotNull(result.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Hp, result.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.Heal, result.AttackStatusPacket.Log);
		Assert.Equal(20, result.AttackStatusPacket.Value);
		Assert.Single(registry.Broadcasts);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
		Assert.Same(result.HpStatUpdatePacket, delivery.Packet);
	}

	[Fact]
	public async Task IncreasePlayerHpAsync_RoutesNegativeHealToHpDamage()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1011, currentHp: 80, currentMp: 40, currentFp: 100);
		player.IsOnline = true;

		var result = await service.IncreasePlayerHpAsync(player, maxHp: 100, value: -90, skillId: 7507);

		Assert.Equal(WorldNpcResourceChangeStatus.Died, result.Status);
		Assert.Equal(WorldNpcResourceChangeKind.Reduce, result.ChangeKind);
		Assert.Equal(80, result.PreviousValue);
		Assert.Equal(0, result.CurrentValue);
		Assert.Equal(80, result.AppliedValue);
		Assert.True(result.RoutedNegativeHealToDamage);
		Assert.True(result.SendHpStatUpdate);
		Assert.True(result.TriggerRestoreTask);
		Assert.True(result.NotifyHpObservers);
		Assert.True(result.HpStatUpdateSent);
		Assert.NotNull(result.HpStatUpdatePacket);
		Assert.Equal(0, result.HpStatUpdatePacket.CurrentHp);
		Assert.Equal(100, result.HpStatUpdatePacket.MaxHp);
		Assert.Equal(0, player.LifeStats!.CurrentHp);
		Assert.Equal(0, player.LifeStats.CurrentMp);
		Assert.NotNull(result.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Hp, result.AttackStatusPacket.Type);
		Assert.Equal(0, result.AttackStatusPacket.HpOrMpPercentage);
		Assert.Single(registry.Broadcasts);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
		Assert.Same(result.HpStatUpdatePacket, delivery.Packet);
	}

	[Fact]
	public async Task IncreasePlayerHpAsync_BlocksDiseaseAndDoesNotMutate()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1010, currentHp: 80, currentMp: 40, currentFp: 100);
		player.AbnormalState = PlayerAbnormalState.Disease;

		var result = await service.IncreasePlayerHpAsync(player, maxHp: 100, value: 30, skillId: 7505);

		Assert.Equal(WorldNpcResourceChangeStatus.BlockedByDisease, result.Status);
		Assert.Equal(80, result.PreviousValue);
		Assert.Equal(80, result.CurrentValue);
		Assert.Equal(0, result.AppliedValue);
		Assert.Equal(80, player.LifeStats!.CurrentHp);
		Assert.False(result.SendHpStatUpdate);
		Assert.Null(result.HpStatUpdatePacket);
		Assert.False(result.HpStatUpdateSent);
		Assert.False(result.NotifyHpObservers);
		Assert.Null(result.AttackStatusPacket);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task ReducePlayerFpAsync_ClampsToZeroAndUsesHpPercentageWithFlyTimeIntent()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1001, currentHp: 80, currentMp: 40, currentFp: 10);
		player.IsOnline = true;

		var result = await service.ReducePlayerFpAsync(player, maxHp: 100, maxFp: 100, value: 25, skillId: 7304);

		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Fp, result.ResourceType);
		Assert.Equal(10, result.PreviousValue);
		Assert.Equal(0, result.CurrentValue);
		Assert.Equal(10, result.AppliedValue);
		Assert.True(result.SendFlyTimeUpdate);
		Assert.True(result.FlyTimeSent);
		Assert.NotNull(result.FlyTimePacket);
		Assert.Equal(0, result.FlyTimePacket.CurrentFp);
		Assert.Equal(100, result.FlyTimePacket.MaxFp);
		Assert.NotNull(result.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.FpDamage, result.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.FpAttack, result.AttackStatusPacket.Log);
		Assert.Equal(10, result.AttackStatusPacket.Value);
		Assert.Equal(80, result.AttackStatusPacket.HpOrMpPercentage);
		Assert.Equal(0, player.LifeStats!.CurrentFp);
		Assert.Single(registry.Broadcasts);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
		Assert.Same(result.FlyTimePacket, delivery.Packet);
	}

	[Fact]
	public async Task IncreasePlayerFpAsync_CapsToMaxAndSkipsPacketWhenFull()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1002, currentHp: 100, currentMp: 50, currentFp: 90);
		player.IsOnline = true;

		var capped = await service.IncreasePlayerFpAsync(player, maxHp: 100, maxFp: 100, value: 25, skillId: 7305);

		Assert.Equal(WorldNpcResourceChangeStatus.Increased, capped.Status);
		Assert.Equal(90, capped.PreviousValue);
		Assert.Equal(100, capped.CurrentValue);
		Assert.Equal(10, capped.AppliedValue);
		Assert.True(capped.SendFlyTimeUpdate);
		Assert.True(capped.FlyTimeSent);
		Assert.NotNull(capped.FlyTimePacket);
		Assert.Equal(100, capped.FlyTimePacket.CurrentFp);
		Assert.Equal(100, capped.FlyTimePacket.MaxFp);
		Assert.NotNull(capped.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Fp, capped.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.FpHeal, capped.AttackStatusPacket.Log);
		Assert.Equal(10, capped.AttackStatusPacket.Value);

		var full = await service.IncreasePlayerFpAsync(player, maxHp: 100, maxFp: 100, value: 5, skillId: 7306);

		Assert.Equal(WorldNpcResourceChangeStatus.NoChange, full.Status);
		Assert.False(full.SendFlyTimeUpdate);
		Assert.Null(full.FlyTimePacket);
		Assert.False(full.FlyTimeSent);
		Assert.Null(full.AttackStatusPacket);
		Assert.Single(registry.Broadcasts);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
		Assert.Same(capped.FlyTimePacket, delivery.Packet);
	}

	[Fact]
	public async Task AddPlayerDpAsync_CapsOnlinePlayerAndSendsDpPacketsInJavaOrder()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1003, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "RANGER", dp: 3900);
		player.IsOnline = true;

		var result = await service.AddPlayerDpAsync(player, value: 250, maxDp: 4000);

		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Dp, result.ResourceType);
		Assert.Equal(3900, result.PreviousValue);
		Assert.Equal(4000, result.CurrentValue);
		Assert.Equal(100, result.AppliedValue);
		Assert.True(result.BroadcastDpInfo);
		Assert.NotNull(result.DpInfoPacket);
		Assert.Equal(player.ObjectId, result.DpInfoPacket.PlayerObjectId);
		Assert.Equal(4000, result.DpInfoPacket.CurrentDp);
		Assert.Equal(1, result.DpInfoBroadcastCount);
		Assert.True(result.SendDpStatUpdate);
		Assert.True(result.DpStatUpdateSent);
		Assert.NotNull(result.DpStatUpdatePacket);
		Assert.Equal(4000, result.DpStatUpdatePacket.CurrentDp);
		Assert.True(result.UpdateStatsAndSpeedVisually);
		Assert.Equal(4000, player.Dp);
		var broadcast = Assert.Single(registry.Broadcasts);
		Assert.Equal(player.ObjectId, broadcast.SourceObjectId);
		Assert.True(broadcast.IncludeSourcePlayer);
		Assert.Same(result.DpInfoPacket, broadcast.Packet);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
		Assert.Same(result.DpStatUpdatePacket, delivery.Packet);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.DpInfoPacket, packet),
			packet => Assert.Same(result.DpStatUpdatePacket, packet));
	}

	[Fact]
	public async Task AddPlayerDpAsync_SkipsStartingClassAndRequiresOnlineMaxDp()
	{
		var service = CreateService(out _, out var registry);
		var startingClass = CreatePlayer(objectId: 1004, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "WARRIOR", dp: 500);
		startingClass.IsOnline = true;

		var skipped = await service.AddPlayerDpAsync(startingClass, value: 100, maxDp: 4000);

		Assert.Equal(WorldNpcResourceChangeStatus.StartingClass, skipped.Status);
		Assert.Equal(500, startingClass.Dp);
		Assert.Null(skipped.DpInfoPacket);
		Assert.Null(skipped.DpStatUpdatePacket);

		var missingMax = CreatePlayer(objectId: 1005, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "RANGER", dp: 500);
		missingMax.IsOnline = true;

		var unresolved = await service.AddPlayerDpAsync(missingMax, value: 100);

		Assert.Equal(WorldNpcResourceChangeStatus.MissingMaxResource, unresolved.Status);
		Assert.Equal(500, missingMax.Dp);
		Assert.Null(unresolved.DpInfoPacket);
		Assert.Null(unresolved.DpStatUpdatePacket);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task SpendPlayerDpForSkillAsync_SpendsDpThroughPacketedBoundary()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1014, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "RANGER", dp: 1200);
		player.IsOnline = true;

		var result = await service.SpendPlayerDpForSkillAsync(player, value: 450, maxDp: 4000);

		Assert.Equal(WorldNpcDpUseActionStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(450, result.RequestedValue);
		Assert.Equal(750, result.CurrentDp);
		Assert.Null(result.SystemMessagePacket);
		Assert.False(result.SystemMessageSent);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, result.Change.Status);
		Assert.Equal(1200, result.Change.PreviousValue);
		Assert.Equal(750, result.Change.CurrentValue);
		Assert.Equal(450, result.Change.AppliedValue);
		Assert.Equal(750, player.Dp);
		Assert.NotNull(result.Change.DpInfoPacket);
		Assert.NotNull(result.Change.DpStatUpdatePacket);
		Assert.Same(result.Change.DpInfoPacket, Assert.Single(registry.Broadcasts).Packet);
		Assert.Same(result.Change.DpStatUpdatePacket, Assert.Single(registry.SentPackets).Packet);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.Change.DpInfoPacket, packet),
			packet => Assert.Same(result.Change.DpStatUpdatePacket, packet));
	}

	[Fact]
	public async Task SpendPlayerDpForSkillAsync_SendsNotEnoughDpMessageWithoutMutation()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1015, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "RANGER", dp: 300);
		player.IsOnline = true;

		var result = await service.SpendPlayerDpForSkillAsync(player, value: 450, maxDp: 4000);

		Assert.Equal(WorldNpcDpUseActionStatus.NotEnoughDp, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(450, result.RequestedValue);
		Assert.Equal(300, result.CurrentDp);
		Assert.Null(result.Change);
		Assert.NotNull(result.SystemMessagePacket);
		Assert.Equal(1300016, result.SystemMessagePacket.MessageId);
		Assert.True(result.SystemMessageSent);
		Assert.Equal(300, player.Dp);
		Assert.Empty(registry.Broadcasts);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
		Assert.Same(result.SystemMessagePacket, delivery.Packet);
	}

	[Fact]
	public async Task TransferPlayerDpAsync_MovesReservedDpInEffectedThenEffectorOrder()
	{
		var service = CreateService(out _, out var registry);
		var effector = CreatePlayer(objectId: 1016, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "RANGER", dp: 1200);
		var effected = CreatePlayer(objectId: 1017, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "CLERIC", dp: 200);
		effector.IsOnline = true;
		effected.IsOnline = true;

		var result = await service.TransferPlayerDpAsync(effector, effected, reservedDp: 500, effectorMaxDp: 4000, effectedMaxDp: 4000);

		Assert.Equal(WorldNpcDpTransferEffectStatus.Applied, result.Status);
		Assert.Equal(500, result.ReservedDp);
		Assert.Equal(effected.ObjectId, result.EffectedObjectId);
		Assert.Equal(effector.ObjectId, result.EffectorObjectId);
		Assert.NotNull(result.EffectedChange);
		Assert.NotNull(result.EffectorChange);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.EffectedChange.Status);
		Assert.Equal(200, result.EffectedChange.PreviousValue);
		Assert.Equal(700, result.EffectedChange.CurrentValue);
		Assert.Equal(500, result.EffectedChange.AppliedValue);
		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, result.EffectorChange.Status);
		Assert.Equal(1200, result.EffectorChange.PreviousValue);
		Assert.Equal(700, result.EffectorChange.CurrentValue);
		Assert.Equal(500, result.EffectorChange.AppliedValue);
		Assert.Equal(700, effected.Dp);
		Assert.Equal(700, effector.Dp);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Equal(2, registry.SentPackets.Count);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.EffectedChange.DpInfoPacket, packet),
			packet => Assert.Same(result.EffectedChange.DpStatUpdatePacket, packet),
			packet => Assert.Same(result.EffectorChange.DpInfoPacket, packet),
			packet => Assert.Same(result.EffectorChange.DpStatUpdatePacket, packet));
	}

	[Fact]
	public async Task TransferPlayerDpAsync_RequiresEffectedAndEffectorPlayers()
	{
		var service = CreateService(out _, out var registry);
		var player = CreatePlayer(objectId: 1018, currentHp: 100, currentMp: 50, currentFp: 100, playerClass: "RANGER", dp: 1200);

		var missingEffected = await service.TransferPlayerDpAsync(player, effected: null, reservedDp: 500, effectorMaxDp: 4000);
		var missingEffector = await service.TransferPlayerDpAsync(effector: null, player, reservedDp: 500, effectedMaxDp: 4000);

		Assert.Equal(WorldNpcDpTransferEffectStatus.MissingEffected, missingEffected.Status);
		Assert.Equal(WorldNpcDpTransferEffectStatus.MissingEffector, missingEffector.Status);
		Assert.Equal(player.ObjectId, missingEffector.EffectedObjectId);
		Assert.Equal(1200, player.Dp);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
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
		player.IsOnline = true;
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
		Assert.True(result.Change.FlyTimeSent);
		Assert.NotNull(result.Change.FlyTimePacket);
		Assert.Equal(100, result.Change.FlyTimePacket.CurrentFp);
		Assert.Equal(100, result.Change.FlyTimePacket.MaxFp);
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

	[Fact]
	public async Task ApplyResourceOverTimePeriodicResultAsync_AddsDpFromStagedDpHealWithPacketIntents()
	{
		var service = CreateService(out _, out var registry);
		var skillDamage = CreateSkillDamageService();
		var player = CreatePlayer(objectId: 1008, currentHp: 100, currentMp: 40, currentFp: 100, dp: 3800);
		player.IsOnline = true;
		var staged = skillDamage.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
			WorldNpcSkillResourceOverTimeEffectKind.DpHeal,
			Value: 300,
			SkillId: 7405,
			CurrentResource: 3800,
			MaxResource: 4000,
			TargetIsPlayer: true));

		var result = await service.ApplyResourceOverTimePeriodicResultAsync(
			staged,
			new WorldNpcResourceMutationTarget(Player: player, MaxDp: 4000));

		Assert.Equal(WorldNpcResourceEffectApplicationStatus.Applied, result.Status);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcEffectResourceType.Dp, result.ResourceType);
		Assert.Equal(7405, result.SkillId);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Change.Status);
		Assert.Equal(200, result.Change.AppliedValue);
		Assert.Equal(4000, result.Change.CurrentValue);
		Assert.Equal(4000, player.Dp);
		Assert.True(result.Change.BroadcastDpInfo);
		Assert.NotNull(result.Change.DpInfoPacket);
		Assert.Equal(player.ObjectId, result.Change.DpInfoPacket.PlayerObjectId);
		Assert.Equal(4000, result.Change.DpInfoPacket.CurrentDp);
		Assert.Equal(1, result.Change.DpInfoBroadcastCount);
		Assert.True(result.Change.SendDpStatUpdate);
		Assert.True(result.Change.DpStatUpdateSent);
		Assert.NotNull(result.Change.DpStatUpdatePacket);
		Assert.Equal(4000, result.Change.DpStatUpdatePacket.CurrentDp);
		Assert.True(result.Change.UpdateStatsAndSpeedVisually);
		Assert.Null(result.Change.AttackStatusPacket);
		Assert.Same(result.Change.DpInfoPacket, Assert.Single(registry.Broadcasts).Packet);
		Assert.Same(result.Change.DpStatUpdatePacket, Assert.Single(registry.SentPackets).Packet);
	}

	[Fact]
	public async Task ApplyResourceOverTimePeriodicResultAsync_IncreasesNpcHpFromStagedHpHeal()
	{
		var service = CreateService(out var lifeStats, out _);
		var skillDamage = CreateSkillDamageService();
		var npc = CreateNpc(objectId: 18, maxHp: 100);
		lifeStats.Initialize(npc, maxHp: 100, maxMp: 40);
		await lifeStats.ReduceHpAsync(npc, damage: 40, maxHp: 100, maxMp: 40, attacker: CreatePlayer(objectId: 2003, currentHp: 100, currentMp: 50, currentFp: 100));
		var staged = skillDamage.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
			WorldNpcSkillResourceOverTimeEffectKind.HpHeal,
			Value: 50,
			SkillId: 7406,
			CurrentResource: 60,
			MaxResource: 100));

		var result = await service.ApplyResourceOverTimePeriodicResultAsync(
			staged,
			new WorldNpcResourceMutationTarget(Npc: npc, KillingBlow: 90));

		Assert.Equal(WorldNpcResourceEffectApplicationStatus.Applied, result.Status);
		Assert.Equal(WorldNpcEffectResourceType.Hp, result.ResourceType);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Change.Status);
		Assert.Equal(60, result.Change.PreviousValue);
		Assert.Equal(100, result.Change.CurrentValue);
		Assert.Equal(40, result.Change.AppliedValue);
		Assert.True(result.Change.KillingBlowReset);
		Assert.NotNull(result.Change.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Hp, result.Change.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.Heal, result.Change.AttackStatusPacket.Log);
		Assert.Equal(7406, result.Change.AttackStatusPacket.SkillId);
		Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
		Assert.Equal(100, stored!.CurrentHp);
	}

	[Fact]
	public async Task ApplyResourceOverTimePeriodicResultAsync_IncreasesPlayerHpFromStagedHpHeal()
	{
		var service = CreateService(out _, out var registry);
		var skillDamage = CreateSkillDamageService();
		var player = CreatePlayer(objectId: 1009, currentHp: 80, currentMp: 40, currentFp: 100);
		player.IsOnline = true;
		player.TeamMembership = PlayerTeamMembership.Group;
		var staged = skillDamage.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
			WorldNpcSkillResourceOverTimeEffectKind.HpHeal,
			Value: 30,
			SkillId: 7407,
			CurrentResource: 80,
			MaxResource: 100));

		var result = await service.ApplyResourceOverTimePeriodicResultAsync(
			staged,
			new WorldNpcResourceMutationTarget(Player: player, MaxHp: 100, KillingBlow: 90));

		Assert.Equal(WorldNpcResourceEffectApplicationStatus.Applied, result.Status);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Change.Status);
		Assert.Equal(20, result.Change.AppliedValue);
		Assert.Equal(100, player.LifeStats!.CurrentHp);
		Assert.True(result.Change.SendHpStatUpdate);
		Assert.True(result.Change.SendGroupStatUpdate);
		Assert.True(result.Change.NotifyHpObservers);
		Assert.True(result.Change.ClearAggroOnFullHp);
		Assert.True(result.Change.KillingBlowReset);
		Assert.True(result.Change.HpStatUpdateSent);
		Assert.NotNull(result.Change.HpStatUpdatePacket);
		Assert.Equal(100, result.Change.HpStatUpdatePacket.CurrentHp);
		Assert.Equal(100, result.Change.HpStatUpdatePacket.MaxHp);
		Assert.NotNull(result.Change.AttackStatusPacket);
		Assert.Equal(SmAttackStatusType.Hp, result.Change.AttackStatusPacket.Type);
		Assert.Equal(SmAttackStatusLog.Heal, result.Change.AttackStatusPacket.Log);
		Assert.Equal(7407, result.Change.AttackStatusPacket.SkillId);
		var delivery = Assert.Single(registry.SentPackets);
		Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
		Assert.Same(result.Change.HpStatUpdatePacket, delivery.Packet);
	}

	[Fact]
	public async Task ApplyResourceOverTimePeriodicResultAsync_BlocksNpcHpHealWhenDiseaseContextIsProvided()
	{
		var service = CreateService(out var lifeStats, out var registry);
		var skillDamage = CreateSkillDamageService();
		var npc = CreateNpc(objectId: 19, maxHp: 100);
		lifeStats.Initialize(npc, maxHp: 100, maxMp: 40);
		await lifeStats.ReduceHpAsync(npc, damage: 20, maxHp: 100, maxMp: 40, attacker: CreatePlayer(objectId: 2004, currentHp: 100, currentMp: 50, currentFp: 100));
		var staged = skillDamage.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
			WorldNpcSkillResourceOverTimeEffectKind.HpHeal,
			Value: 20,
			SkillId: 7408,
			CurrentResource: 80,
			MaxResource: 100));

		var result = await service.ApplyResourceOverTimePeriodicResultAsync(
			staged,
			new WorldNpcResourceMutationTarget(Npc: npc, TargetHasDisease: true));

		Assert.Equal(WorldNpcResourceEffectApplicationStatus.EffectSkipped, result.Status);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.BlockedByDisease, result.Change.Status);
		Assert.Equal(80, result.Change.CurrentValue);
		Assert.Null(result.Change.AttackStatusPacket);
		Assert.Empty(registry.Broadcasts);
		Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
		Assert.Equal(80, stored!.CurrentHp);
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

		public List<PacketDelivery> SentPackets { get; } = [];

		public List<GameServerPacket> PacketOrder { get; } = [];

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
			PacketOrder.Add(packet);
			SentPackets.Add(new PacketDelivery(playerObjectId, packet));
			return Task.FromResult(true);
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
			PacketOrder.Add(packet);
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

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);
}
