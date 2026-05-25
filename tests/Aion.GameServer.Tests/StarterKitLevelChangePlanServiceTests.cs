using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class StarterKitLevelChangePlanServiceTests
{
	[Fact]
	public void CreatePlan_StagesJavaStarterKitLevelBucketsInInclusiveOrder()
	{
		var player = CreatePlayer();

		var plan = StarterKitLevelChangePlanService.CreatePlan(
			player,
			starterKitEnabled: true,
			fromLevel: 20,
			toLevel: 25);

		Assert.Equal(StarterKitLevelChangePlanStatus.Planned, plan.Status);
		Assert.True(plan.Applied);
		Assert.Equal(4701, plan.ObjectId);
		Assert.Equal("Starter", plan.PlayerName);
		Assert.Equal(11, plan.Descriptors.Count);
		Assert.Equal(
		[
			(20, 188054100, 1L),
			(20, 125001832, 1L),
			(20, 122000449, 1L),
			(20, 122000451, 1L),
			(20, 120015052, 1L),
			(20, 120015051, 1L),
			(20, 123000879, 1L),
			(25, 190100032, 1L),
			(25, 164002272, 25L),
			(25, 162000039, 25L),
			(25, 162002018, 25L),
		], plan.Descriptors.Select(descriptor => (descriptor.Level, descriptor.Reward.ItemId, descriptor.Reward.Count)));
		Assert.All(plan.Descriptors, descriptor =>
		{
			Assert.Equal(StarterKitLevelChangeDescriptorStatus.PlannedSystemMail, descriptor.Status);
			Assert.False(descriptor.IsLive);
			Assert.Equal("Beyond Aion", descriptor.Sender);
			Assert.Equal("Starter Kit", descriptor.Title);
			Assert.Equal("EXPRESS", descriptor.LetterType);
			Assert.Contains("additional item pack", descriptor.Body, StringComparison.Ordinal);
			Assert.Contains("SystemMailService.sendMail", descriptor.JavaSource, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void CreatePlan_RecordsDisabledEmptyNoMatchAndMissingPlayerBranches()
	{
		var player = CreatePlayer();

		var disabled = StarterKitLevelChangePlanService.CreatePlan(player, starterKitEnabled: false, fromLevel: 20, toLevel: 25);
		var emptyRange = StarterKitLevelChangePlanService.CreatePlan(player, starterKitEnabled: true, fromLevel: 26, toLevel: 25);
		var noMatch = StarterKitLevelChangePlanService.CreatePlan(player, starterKitEnabled: true, fromLevel: 2, toLevel: 19);
		var missingPlayer = StarterKitLevelChangePlanService.CreatePlan(null, starterKitEnabled: true, fromLevel: 1, toLevel: 1);

		Assert.Equal(StarterKitLevelChangePlanStatus.SkippedDisabled, disabled.Status);
		Assert.Equal(StarterKitLevelChangePlanStatus.EmptyLevelRange, emptyRange.Status);
		Assert.Equal(StarterKitLevelChangePlanStatus.NoMatchingRewards, noMatch.Status);
		Assert.Equal(StarterKitLevelChangePlanStatus.MissingPlayer, missingPlayer.Status);
		Assert.Empty(disabled.Descriptors);
		Assert.Empty(emptyRange.Descriptors);
		Assert.Empty(noMatch.Descriptors);
		Assert.Empty(missingPlayer.Descriptors);
		Assert.False(disabled.Applied);
		Assert.False(noMatch.Applied);
	}

	[Fact]
	public void RewardBuckets_MatchJavaStarterKitStaticItems()
	{
		var buckets = StarterKitLevelChangePlanService.RewardBuckets;

		Assert.Equal([1, 20, 25, 35, 50, 60], buckets.Keys);
		Assert.Equal(new StarterKitRewardItem(169610056, 1), Assert.Single(buckets[1]));
		Assert.Equal(
		[
			new StarterKitRewardItem(169620072, 1),
			new StarterKitRewardItem(162002030, 100),
			new StarterKitRewardItem(162002018, 50),
			new StarterKitRewardItem(188053526, 5),
			new StarterKitRewardItem(188053783, 5),
		], buckets[60]);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 4701,
			Name = "Starter",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 20,
		};
	}
}
