using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class QuestRewardServiceTests
{
	[Fact]
	public async Task ApplyDpRewardAsync_AddsQuestDpThroughPacketedBoundary()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1300, playerClass: "RANGER", dp: 3500);

		var result = await service.ApplyDpRewardAsync(player, rewardDp: 600, maxDp: 4000);

		Assert.Equal(QuestDpRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(600, result.RewardDp);
		Assert.Equal(3500, result.PreviousDp);
		Assert.Equal(4000, result.CurrentDp);
		Assert.Equal(4000, player.Dp);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, result.Change.Status);
		Assert.Equal(500, result.Change.AppliedValue);
		Assert.NotNull(result.Change.DpInfoPacket);
		Assert.NotNull(result.Change.VisualStatsUpdate);
		Assert.NotNull(result.Change.VisualStatsUpdate.StatsPacket);
		Assert.NotNull(result.Change.VisualStatsUpdate.SpeedPacket);
		Assert.NotNull(result.Change.DpStatUpdatePacket);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Same(result.Change.DpInfoPacket, registry.Broadcasts[0].Packet);
		Assert.Same(result.Change.VisualStatsUpdate.SpeedPacket, registry.Broadcasts[1].Packet);
		Assert.Collection(
			registry.SentPackets,
			delivery =>
			{
				Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
				Assert.Same(result.Change.VisualStatsUpdate!.StatsPacket, delivery.Packet);
			},
			delivery =>
			{
				Assert.Equal(player.ObjectId, delivery.PlayerObjectId);
				Assert.Same(result.Change.DpStatUpdatePacket, delivery.Packet);
			});
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.Change.DpInfoPacket, packet),
			packet => Assert.Same(result.Change.VisualStatsUpdate!.StatsPacket, packet),
			packet => Assert.Same(result.Change.VisualStatsUpdate!.SpeedPacket, packet),
			packet => Assert.Same(result.Change.DpStatUpdatePacket, packet));
	}

	[Fact]
	public async Task ApplyDpRewardAsync_SkipsZeroDpRewardWithoutMutationOrPackets()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1301, playerClass: "RANGER", dp: 500);

		var result = await service.ApplyDpRewardAsync(player, rewardDp: 0, maxDp: 4000);

		Assert.Equal(QuestDpRewardStatus.NoDpReward, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(0, result.RewardDp);
		Assert.Equal(500, result.PreviousDp);
		Assert.Equal(500, result.CurrentDp);
		Assert.Equal(500, player.Dp);
		Assert.Null(result.Change);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task ApplyDpRewardAsync_RequiresPlayerAndUsesOnlineMaxDp()
	{
		var service = CreateService(out var registry);
		var onlinePlayer = CreatePlayer(objectId: 1302, playerClass: "RANGER", dp: 500);

		var missingPlayer = await service.ApplyDpRewardAsync(player: null, rewardDp: 100, maxDp: 4000);
		var liveMax = await service.ApplyDpRewardAsync(onlinePlayer, rewardDp: 100);

		Assert.Equal(QuestDpRewardStatus.MissingPlayer, missingPlayer.Status);
		Assert.Equal(100, missingPlayer.RewardDp);
		Assert.Equal(QuestDpRewardStatus.Applied, liveMax.Status);
		Assert.NotNull(liveMax.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Increased, liveMax.Change.Status);
		Assert.Equal(4000, liveMax.Change.MaxValue);
		Assert.Equal(600, onlinePlayer.Dp);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Equal(2, registry.SentPackets.Count);
	}

	[Fact]
	public async Task ApplyDpRewardAsync_PreservesStartingClassGuard()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1303, playerClass: "WARRIOR", dp: 500);

		var result = await service.ApplyDpRewardAsync(player, rewardDp: 100, maxDp: 4000);

		Assert.Equal(QuestDpRewardStatus.DpBoundarySkipped, result.Status);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.StartingClass, result.Change.Status);
		Assert.Equal(500, player.Dp);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public void ApplyApReward_AppliesConfiguredQuestRateAndAddsApThroughPlanner()
	{
		var service = CreateService(
			out _,
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					ApQuestRates = [1f, 1.75f],
				},
			});
		var player = CreatePlayer(objectId: 1304, playerClass: "RANGER", dp: 500, ap: 900, membership: 1);

		var result = service.ApplyApReward(player, rewardAp: 200);

		Assert.Equal(QuestApRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(200, result.RewardAp);
		Assert.Equal(350, result.AppliedRewardAp);
		Assert.False(result.IsNonCountQuest);
		Assert.Equal(900, result.PreviousAp);
		Assert.Equal(1_250, result.CurrentAp);
		Assert.Equal(1_250, player.AbyssRank.Ap);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(350, result.AbyssPointsPlan.Added);
		Assert.Collection(
			result.AbyssPointsPlan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1320000, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Fact]
	public void ApplyApReward_SkipsQuestRateForJavaNonCountCategory()
	{
		var service = CreateService(
			out _,
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					ApQuestRates = [1f, 3f],
				},
			});
		var player = CreatePlayer(objectId: 1305, playerClass: "RANGER", dp: 500, ap: 100, membership: 1);

		var result = service.ApplyApReward(player, rewardAp: 200, isNonCountQuest: true);

		Assert.Equal(QuestApRewardStatus.Applied, result.Status);
		Assert.Equal(200, result.RewardAp);
		Assert.Equal(200, result.AppliedRewardAp);
		Assert.True(result.IsNonCountQuest);
		Assert.Equal(300, result.CurrentAp);
		Assert.Equal(300, player.AbyssRank.Ap);
	}

	[Fact]
	public void ApplyApReward_SkipsMissingPlayerAndZeroApReward()
	{
		var service = CreateService(out _);
		var player = CreatePlayer(objectId: 1306, playerClass: "RANGER", dp: 500, ap: 700);

		var missingPlayer = service.ApplyApReward(null, rewardAp: 200);
		var zeroReward = service.ApplyApReward(player, rewardAp: 0);

		Assert.Equal(QuestApRewardStatus.MissingPlayer, missingPlayer.Status);
		Assert.Equal(QuestApRewardStatus.NoApReward, zeroReward.Status);
		Assert.Null(missingPlayer.AbyssPointsPlan);
		Assert.Null(zeroReward.AbyssPointsPlan);
		Assert.Equal(700, player.AbyssRank.Ap);
	}

	[Fact]
	public void ApplyQuestApRate_MatchesJavaMembershipFallbacksAndOverflowBehavior()
	{
		var clampedMembership = QuestRewardService.ApplyQuestApRate(
			membershipLevel: 7,
			rewardAp: 200,
			apQuestRates: [1f, 1.5f]);
		var emptyRates = QuestRewardService.ApplyQuestApRate(
			membershipLevel: 7,
			rewardAp: 200,
			apQuestRates: []);
		var overflowFallback = QuestRewardService.ApplyQuestApRate(
			membershipLevel: 1,
			rewardAp: int.MaxValue,
			apQuestRates: [1f, 2f]);

		Assert.Equal(300, clampedMembership);
		Assert.Equal(200, emptyRates);
		Assert.Equal(int.MaxValue, overflowFallback);
	}

	[Fact]
	public void ApplyGpReward_AppliesConfiguredGpRateAndAddsGpThroughPlanner()
	{
		var service = CreateService(
			out _,
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					GpRates = [1f, 1.5f],
				},
			});
		var player = CreatePlayer(objectId: 1307, playerClass: "RANGER", dp: 500, gp: 100, membership: 1);

		var result = service.ApplyGpReward(player, rewardGp: 40);

		Assert.Equal(QuestGpRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(40, result.RewardGp);
		Assert.Equal(60, result.AppliedRewardGp);
		Assert.Equal(100, result.PreviousGp);
		Assert.Equal(160, result.CurrentGp);
		Assert.Equal(160, player.AbyssRank.Gp);
		Assert.Equal(60, player.AbyssRank.DailyGp);
		Assert.Equal(60, player.AbyssRank.WeeklyGp);
		Assert.NotNull(result.GloryPointsPlan);
		Assert.Equal(60, result.GloryPointsPlan.Added);
		Assert.Collection(
			result.GloryPointsPlan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1402081, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Fact]
	public void ApplyGpReward_SkipsMissingPlayerAndZeroGpReward()
	{
		var service = CreateService(out _);
		var player = CreatePlayer(objectId: 1308, playerClass: "RANGER", dp: 500, gp: 700);

		var missingPlayer = service.ApplyGpReward(null, rewardGp: 200);
		var zeroReward = service.ApplyGpReward(player, rewardGp: 0);

		Assert.Equal(QuestGpRewardStatus.MissingPlayer, missingPlayer.Status);
		Assert.Equal(QuestGpRewardStatus.NoGpReward, zeroReward.Status);
		Assert.Null(missingPlayer.GloryPointsPlan);
		Assert.Null(zeroReward.GloryPointsPlan);
		Assert.Equal(700, player.AbyssRank.Gp);
	}

	[Fact]
	public void ApplyQuestGpRate_MatchesJavaMembershipFallbacksAndOverflowBehavior()
	{
		var clampedMembership = QuestRewardService.ApplyQuestGpRate(
			membershipLevel: 7,
			rewardGp: 200,
			gpRates: [1f, 1.5f]);
		var emptyRates = QuestRewardService.ApplyQuestGpRate(
			membershipLevel: 7,
			rewardGp: 200,
			gpRates: []);
		var overflowFallback = QuestRewardService.ApplyQuestGpRate(
			membershipLevel: 1,
			rewardGp: int.MaxValue,
			gpRates: [1f, 2f]);

		Assert.Equal(300, clampedMembership);
		Assert.Equal(200, emptyRates);
		Assert.Equal(int.MaxValue, overflowFallback);
	}

	[Fact]
	public void CreateXpRewardPlan_AppliesJavaQuestRateReposeAndSalvationWithoutMutatingPlayer()
	{
		var service = CreateService(
			out _,
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					XpQuestRates = [1f, 2f],
				},
			});
		var player = CreatePlayer(
			objectId: 1309,
			playerClass: "RANGER",
			dp: 500,
			membership: 1,
			level: 15,
			exp: 14_000,
			reposeEnergy: 50);
		var table = CreateLinearExperienceTable();

		var result = service.CreateXpRewardPlan(
			player,
			table,
			rewardXp: 100,
			npcName: "quest npc",
			salvationPercent: 10);

		Assert.Equal(QuestXpRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(100, result.RewardXp);
		Assert.Equal(200, result.AppliedBaseXp);
		Assert.Equal(50, result.ReposeUsed);
		Assert.Equal(20, result.ReposeBonus);
		Assert.Equal(20, result.SalvationBonus);
		Assert.Equal(240, result.FinalRewardXp);
		Assert.Equal(14_000, result.PreviousExp);
		Assert.Equal(14_240, result.CurrentExp);
		Assert.Equal(15, result.PreviousLevel);
		Assert.Equal(15, result.CurrentLevel);
		Assert.Equal(50, result.PreviousReposeEnergy);
		Assert.Equal(0, result.CurrentReposeEnergy);
		Assert.Equal(250, result.MaxReposeEnergy);
		Assert.Equal(QuestXpRewardMessageKind.NamedReposeAndSalvationBonus, result.MessageKind);
		Assert.Equal(
		[
			QuestXpRewardPacketIntent.StatUpdateExp,
			QuestXpRewardPacketIntent.XpSystemMessage,
		], result.PacketIntents);
		Assert.False(result.RequiresAscensionLimitMessage);
		Assert.Equal(14_000, player.Exp);
		Assert.Equal(50, player.ReposeEnergy);
	}

	[Fact]
	public void ApplyXpReward_UsesRuntimeLegionBonusAndMutatesPlayerState()
	{
		var runtimeContext = new GameServerRuntimeContext();
		Assert.True(runtimeContext.LegionBonuses.TryActivate(77, LegionBonusRuntime.OnlineMemberThreshold));
		var service = CreateService(
			out _,
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					XpQuestRates = [1f],
				},
			},
			runtimeContext);
		var player = CreatePlayer(
			objectId: 1315,
			playerClass: "RANGER",
			dp: 500,
			level: 15,
			exp: 14_000,
			legionId: 77);

		var result = service.ApplyXpReward(
			player,
			CreateLinearExperienceTable(),
			rewardXp: 200,
			npcName: "legion quest");

		Assert.Equal(QuestXpRewardStatus.Applied, result.Status);
		Assert.True(result.DidMutatePlayer);
		Assert.Equal(220, result.Plan.AppliedBaseXp);
		Assert.Equal(220, result.Plan.FinalRewardXp);
		Assert.Equal(14_220, result.CurrentExp);
		Assert.Equal(14_220, player.Exp);
		Assert.Equal(result.CurrentLevel, player.Level);
		Assert.Collection(
			result.Packets,
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet));
	}

	[Fact]
	public void ApplyXpReward_LeavesJavaRateUnboostedWhenRuntimeLegionBonusIsInactive()
	{
		var service = CreateService(
			out _,
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					XpQuestRates = [1f],
				},
			},
			new GameServerRuntimeContext());
		var player = CreatePlayer(
			objectId: 1316,
			playerClass: "RANGER",
			dp: 500,
			level: 15,
			exp: 14_000,
			legionId: 77);

		var result = service.ApplyXpReward(
			player,
			CreateLinearExperienceTable(),
			rewardXp: 200);

		Assert.True(result.DidMutatePlayer);
		Assert.Equal(200, result.Plan.AppliedBaseXp);
		Assert.Equal(14_200, player.Exp);
		Assert.Collection(
			result.Packets,
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet));
	}

	[Fact]
	public void CreateXpRewardPlan_RecordsJavaGuardsAndNonDaevaLevelCap()
	{
		var service = CreateService(out _);
		var table = CreateLinearExperienceTable();
		var player = CreatePlayer(
			objectId: 1310,
			playerClass: "RANGER",
			dp: 500,
			level: 9,
			exp: 8_900,
			position: new WorldPosition(210010000, 10, 20, 30, 0));
		var zero = service.CreateXpRewardPlan(player, table, rewardXp: 0);
		var noExp = service.CreateXpRewardPlan(player, table, rewardXp: 100, noExp: true);
		var nightmare = service.CreateXpRewardPlan(
			CreatePlayer(
				objectId: 1311,
				playerClass: "RANGER",
				dp: 500,
				level: 9,
				exp: 8_900,
				position: new WorldPosition(301200000, 10, 20, 30, 0)),
			table,
			rewardXp: 100);
		var capped = service.CreateXpRewardPlan(player, table, rewardXp: 500, isDaeva: false);

		Assert.Equal(QuestXpRewardStatus.NoXpReward, zero.Status);
		Assert.Equal(QuestXpRewardStatus.NoExp, noExp.Status);
		Assert.Equal(QuestXpRewardStatus.NightmareCircus, nightmare.Status);
		Assert.Empty(zero.PacketIntents);
		Assert.Empty(noExp.PacketIntents);
		Assert.Empty(nightmare.PacketIntents);

		Assert.Equal(QuestXpRewardStatus.Applied, capped.Status);
		Assert.Equal(9_000, capped.CurrentExp);
		Assert.Equal(9, capped.CurrentLevel);
		Assert.True(capped.RequiresAscensionLimitMessage);
		Assert.Equal(
		[
			QuestXpRewardPacketIntent.StatUpdateExp,
			QuestXpRewardPacketIntent.XpSystemMessage,
		], capped.PacketIntents);
	}

	[Fact]
	public void CreateXpSystemMessagePackets_MapsPlanMessageKindsAndAscensionWarningInJavaOrder()
	{
		var service = CreateService(
			out _,
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					XpQuestRates = [1f, 2f],
				},
			});
		var table = CreateLinearExperienceTable();
		var namedBonus = service.CreateXpRewardPlan(
			CreatePlayer(
				objectId: 1312,
				playerClass: "RANGER",
				dp: 500,
				membership: 1,
				level: 15,
				exp: 14_000,
				reposeEnergy: 50),
			table,
			rewardXp: 100,
			npcName: "quest npc",
			salvationPercent: 10);
		var ascension = service.CreateXpRewardPlan(
			CreatePlayer(
				objectId: 1313,
				playerClass: "RANGER",
				dp: 500,
				level: 9,
				exp: 8_900,
				position: new WorldPosition(210010000, 10, 20, 30, 0)),
			table,
			rewardXp: 500,
			isDaeva: false);
		var skipped = service.CreateXpRewardPlan(
			CreatePlayer(objectId: 1314, playerClass: "RANGER", dp: 500),
			table,
			rewardXp: 0);

		var namedBonusPackets = QuestRewardService.CreateXpSystemMessagePackets(namedBonus);
		var ascensionPackets = QuestRewardService.CreateXpSystemMessagePackets(ascension);
		var skippedPackets = QuestRewardService.CreateXpSystemMessagePackets(skipped);

		Assert.Equal([1400344], namedBonusPackets.Select(packet => packet.MessageId));
		Assert.Equal([1370002, 1400545], ascensionPackets.Select(packet => packet.MessageId));
		Assert.Empty(skippedPackets);
	}

	[Fact]
	public void ApplyQuestXpRate_MatchesJavaFloatRateBoostLegionFallbacksAndOverflow()
	{
		var clampedMembership = QuestRewardService.ApplyQuestXpRate(
			membershipLevel: 7,
			rewardXp: 200,
			xpQuestRates: [1f, 1.5f],
			questXpBoostStat: 150);
		var legionBonus = QuestRewardService.ApplyQuestXpRate(
			membershipLevel: 1,
			rewardXp: 200,
			xpQuestRates: [1f, 1.5f],
			questXpBoostStat: 100,
			hasLegionBonus: true);
		var emptyRates = QuestRewardService.ApplyQuestXpRate(
			membershipLevel: 7,
			rewardXp: 200,
			xpQuestRates: []);
		var floatRounded = QuestRewardService.ApplyQuestXpRate(
			membershipLevel: 0,
			rewardXp: 16_777_217,
			xpQuestRates: [1f]);
		var overflowSaturates = QuestRewardService.ApplyQuestXpRate(
			membershipLevel: 0,
			rewardXp: long.MaxValue,
			xpQuestRates: [2f]);

		Assert.Equal(450, clampedMembership);
		Assert.Equal(330, legionBonus);
		Assert.Equal(200, emptyRates);
		Assert.Equal(16_777_216, floatRounded);
		Assert.Equal(long.MaxValue, overflowSaturates);
	}

	[Fact]
	public void CreateKinahRewardPlan_CreatesMissingKinahItemWithQuestPacketMask()
	{
		var service = CreateService(
			out _,
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					QuestKinahRates = [1f, 2.5f],
				},
			});
		var player = CreatePlayer(objectId: 1310, playerClass: "RANGER", dp: 500, membership: 1);

		var result = service.CreateKinahRewardPlan(
			player,
			Array.Empty<InventoryItem>(),
			rewardKinah: 400,
			nextObjectId: () => 9001);

		Assert.Equal(QuestKinahRewardStatus.CreatedKinahItem, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(400, result.RewardKinah);
		Assert.Equal(1000, result.AppliedKinah);
		Assert.Equal(0, result.PreviousKinah);
		Assert.Equal(1000, result.CurrentKinah);
		Assert.True(result.CreatesMissingKinahItem);
		Assert.Equal(SmInventoryUpdateItem.IncreaseKinahQuest, result.PacketUpdateType);
		Assert.NotNull(result.KinahItemUpdate);
		Assert.Equal(InventoryItemFactory.KinahItemId, result.KinahItemUpdate.ItemId);
		Assert.Equal(9001, result.KinahItemUpdate.ObjectId);
		Assert.Equal(1000, result.KinahItemUpdate.Count);
		Assert.Equal(QuestKinahRewardPlan.CubeStorageId, result.KinahItemUpdate.Location);
		Assert.Equal(QuestKinahRewardPlan.FirstAvailableSlot, result.KinahItemUpdate.Slot);
		Assert.Equal(0, result.OverflowRemainder);
	}

	[Fact]
	public void CreateKinahRewardPlan_UpdatesExistingKinahAndAppliesJavaCap()
	{
		var service = CreateService(out _);
		var player = CreatePlayer(objectId: 1311, playerClass: "RANGER", dp: 500);
		var kinah = new InventoryItem
		{
			ObjectId = 77,
			ItemId = InventoryItemFactory.KinahItemId,
			Count = 900,
			OwnerId = player.ObjectId,
			Location = QuestKinahRewardPlan.CubeStorageId,
		};

		var result = service.CreateKinahRewardPlan(
			player,
			[kinah],
			rewardKinah: 200,
			kinahMaxStackCount: 1000);

		Assert.Equal(QuestKinahRewardStatus.UpdatedExistingKinahItem, result.Status);
		Assert.False(result.CreatesMissingKinahItem);
		Assert.Equal(900, result.PreviousKinah);
		Assert.Equal(1000, result.CurrentKinah);
		Assert.Equal(200, result.AppliedKinah);
		Assert.Equal(100, result.OverflowRemainder);
		Assert.NotNull(result.KinahItemUpdate);
		Assert.Equal(77, result.KinahItemUpdate.ObjectId);
		Assert.Equal(1000, result.KinahItemUpdate.Count);
	}

	[Fact]
	public void CreateKinahRewardPlan_PreservesZeroAndNegativeJavaGuards()
	{
		var service = CreateService(out _);
		var player = CreatePlayer(objectId: 1312, playerClass: "RANGER", dp: 500);

		var zero = service.CreateKinahRewardPlan(
			player,
			Array.Empty<InventoryItem>(),
			rewardKinah: 0,
			nextObjectId: () => throw new InvalidOperationException("raw zero should skip Java branch"));
		var negativeMissing = service.CreateKinahRewardPlan(
			player,
			Array.Empty<InventoryItem>(),
			rewardKinah: -50,
			nextObjectId: () => 9002);
		var existing = new InventoryItem
		{
			ObjectId = 78,
			ItemId = InventoryItemFactory.KinahItemId,
			Count = 300,
			OwnerId = player.ObjectId,
			Location = QuestKinahRewardPlan.CubeStorageId,
		};
		var negativeExisting = service.CreateKinahRewardPlan(player, [existing], rewardKinah: -50);

		Assert.Equal(QuestKinahRewardStatus.NoReward, zero.Status);
		Assert.Null(zero.KinahItemUpdate);
		Assert.Equal(QuestKinahRewardStatus.NonPositiveAppliedAmountCreatedKinahItem, negativeMissing.Status);
		Assert.True(negativeMissing.CreatesMissingKinahItem);
		Assert.Equal(-50, negativeMissing.AppliedKinah);
		Assert.Equal(0, negativeMissing.CurrentKinah);
		Assert.NotNull(negativeMissing.KinahItemUpdate);
		Assert.Equal(0, negativeMissing.KinahItemUpdate.Count);
		Assert.Equal(QuestKinahRewardStatus.NonPositiveAppliedAmountExistingKinahItem, negativeExisting.Status);
		Assert.False(negativeExisting.CreatesMissingKinahItem);
		Assert.Equal(300, negativeExisting.PreviousKinah);
		Assert.Equal(300, negativeExisting.CurrentKinah);
		Assert.Equal(-50, negativeExisting.AppliedKinah);
	}

	[Fact]
	public void ApplyQuestKinahRate_MatchesJavaFloatTruncationMembershipFallbacksAndOverflow()
	{
		var clampedMembership = QuestRewardService.ApplyQuestKinahRate(
			membershipLevel: 7,
			rewardKinah: 200,
			questKinahRates: [1f, 1.5f]);
		var emptyRates = QuestRewardService.ApplyQuestKinahRate(
			membershipLevel: 7,
			rewardKinah: 200,
			questKinahRates: []);
		var floatRounded = QuestRewardService.ApplyQuestKinahRate(
			membershipLevel: 0,
			rewardKinah: 16_777_217,
			questKinahRates: [1f]);
		var overflowSaturates = QuestRewardService.ApplyQuestKinahRate(
			membershipLevel: 0,
			rewardKinah: long.MaxValue,
			questKinahRates: [2f]);

		Assert.Equal(300, clampedMembership);
		Assert.Equal(200, emptyRates);
		Assert.Equal(16_777_216, floatRounded);
		Assert.Equal(long.MaxValue, overflowSaturates);
	}

	private static QuestRewardService CreateService(
		out CapturingConnectionRegistry registry,
		GameServerOptions? options = null,
		GameServerRuntimeContext? runtimeContext = null)
	{
		registry = new CapturingConnectionRegistry();
		var resourceStats = new WorldNpcResourceStatsService(
			new WorldNpcLifeStatsService(new WorldNpcDeathDropWorkflowService(null!, null!)),
			registry,
			new PlayerVisualStatsUpdateService(registry));
		return new QuestRewardService(resourceStats, options, runtimeContext);
	}

	private static Player CreatePlayer(
		int objectId,
		string playerClass,
		int dp,
		int ap = 0,
		int gp = 0,
		byte membership = 0,
		int level = 10,
		long exp = 0,
		long reposeEnergy = 0,
		int legionId = 0,
		WorldPosition? position = null)
	{
		return new Player
		{
			ObjectId = objectId,
			LegionId = legionId,
			Race = "ELYOS",
			PlayerClass = playerClass,
			Level = level,
			Exp = exp,
			Dp = dp,
			ReposeEnergy = reposeEnergy,
			IsOnline = true,
			AccountMembership = membership,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = ap, Gp = gp },
			Position = position ?? new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}

	private static PlayerExperienceTable CreateLinearExperienceTable()
	{
		return new PlayerExperienceTable(Enumerable.Range(0, 70).Select(level => (long)level * 1000).ToArray());
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
			SentPackets.Add(new PacketDelivery(playerObjectId, packet));
			PacketOrder.Add(packet);
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
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			PacketOrder.Add(packet);
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

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);
}
