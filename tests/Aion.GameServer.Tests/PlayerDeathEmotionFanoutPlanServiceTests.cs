using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerDeathEmotionFanoutPlanServiceTests
{
	[Fact]
	public void CreatePlan_OrdinaryAttackerPlansDieEmotionTargetAndKnownCreatureCleanup()
	{
		var plan = PlayerDeathEmotionFanoutPlanService.CreatePlan(
			OwnerObjectId,
			LastAttackerObjectId,
			new[] { KnownCreatureOneObjectId, KnownCreatureTwoObjectId });

		Assert.Equal(PlayerDeathEmotionFanoutPlanStatus.Planned, plan.Status);
		Assert.Equal(OwnerObjectId, plan.OwnerObjectId);
		Assert.Equal(LastAttackerObjectId, plan.LastAttackerObjectId);
		Assert.Equal(LastAttackerObjectId, plan.EmotionTargetObjectId);
		Assert.Equal(EmotionType.Die, plan.EmotionType);
		Assert.Equal(18, plan.EmotionTypeId);
		Assert.Equal(0, plan.EmotionActionId);
		Assert.Equal(SmEmotion.PacketOpCode, plan.SmEmotionPacketOpcode);
		Assert.True(plan.UsesBroadcastPacketAndReceive);
		Assert.True(plan.NotifiesDeathObservers);
		Assert.False(plan.MutatesKnownCreatureAggro);
		Assert.False(plan.SentPackets);
		Assert.False(plan.IsLive);
		Assert.Equal(
			new[] { KnownCreatureOneObjectId, KnownCreatureTwoObjectId },
			plan.KnownCreatureAggroCleanupIntents.Select(intent => intent.CreatureObjectId));
		Assert.All(plan.KnownCreatureAggroCleanupIntents, intent =>
		{
			Assert.Equal(OwnerObjectId, intent.HatedOwnerObjectId);
			Assert.True(intent.ShouldStopHating);
		});
		Assert.Contains("broadcastPacketAndReceive", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_SelfDeathUsesZeroEmotionTargetLikeJava()
	{
		var plan = PlayerDeathEmotionFanoutPlanService.CreatePlan(
			OwnerObjectId,
			OwnerObjectId,
			Array.Empty<int>());

		Assert.Equal(0, plan.EmotionTargetObjectId);
		Assert.Empty(plan.KnownCreatureAggroCleanupIntents);
		Assert.False(plan.SentPackets);
		Assert.False(plan.MutatesKnownCreatureAggro);
	}

	[Fact]
	public void CreatePlan_OrdersObserverBeforeBroadcastBeforeKnownListCleanup()
	{
		var plan = PlayerDeathEmotionFanoutPlanService.CreatePlan(
			OwnerObjectId,
			LastAttackerObjectId,
			new[] { KnownCreatureOneObjectId });

		AssertOrdered(
			plan.Steps,
			PlayerDeathEmotionFanoutPlanStep.NotifyDeathObservers,
			PlayerDeathEmotionFanoutPlanStep.BroadcastDieEmotion,
			PlayerDeathEmotionFanoutPlanStep.StopKnownCreaturesHatingOwner);
	}

	private static void AssertOrdered(IReadOnlyList<PlayerDeathEmotionFanoutPlanStep> actual, params PlayerDeathEmotionFanoutPlanStep[] expected)
	{
		var previousIndex = -1;
		foreach (var step in expected)
		{
			var currentIndex = Array.IndexOf(actual.ToArray(), step);
			Assert.True(currentIndex > previousIndex, $"Expected {step} after index {previousIndex}, actual order: {string.Join(", ", actual)}");
			previousIndex = currentIndex;
		}
	}

	private const int OwnerObjectId = 1001;
	private const int LastAttackerObjectId = 2002;
	private const int KnownCreatureOneObjectId = 3003;
	private const int KnownCreatureTwoObjectId = 3004;
}
