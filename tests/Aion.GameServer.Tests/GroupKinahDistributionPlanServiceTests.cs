using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class GroupKinahDistributionPlanServiceTests
{
	[Fact]
	public void NotAMember_IsIgnored()
	{
		// Java parity: TeamKinahDistributionEvent.checkCondition() == false -> handleEvent never runs.
		var plan = GroupKinahDistributionPlanService.Plan(amount: 100, distributorKinah: 1000, onlineMemberCount: 3, isTeamMember: false);
		Assert.Equal(GroupKinahDistributionOutcome.Ignored, plan.Outcome);
		Assert.Equal(0, plan.RewardPerPlayer);
	}

	[Fact]
	public void InsufficientKinah_IsNotEnoughMoney()
	{
		// Java parity: getInventory().getKinah() < amount -> STR_NOT_ENOUGH_MONEY, return.
		var plan = GroupKinahDistributionPlanService.Plan(amount: 100, distributorKinah: 99, onlineMemberCount: 3, isTeamMember: true);
		Assert.Equal(GroupKinahDistributionOutcome.NotEnoughMoney, plan.Outcome);
		Assert.Equal(0, plan.RewardPerPlayer);
	}

	[Fact]
	public void SingleOnlineMember_NoDistribution()
	{
		// Java parity: onlineMembers.size() > 1 is false -> nothing happens (no packet).
		var plan = GroupKinahDistributionPlanService.Plan(amount: 100, distributorKinah: 1000, onlineMemberCount: 1, isTeamMember: true);
		Assert.Equal(GroupKinahDistributionOutcome.NoDistribution, plan.Outcome);
	}

	[Fact]
	public void AmountLessThanMemberCount_NoDistribution()
	{
		// Java parity: amount >= onlineMembers.size() is false -> nothing happens.
		var plan = GroupKinahDistributionPlanService.Plan(amount: 2, distributorKinah: 1000, onlineMemberCount: 3, isTeamMember: true);
		Assert.Equal(GroupKinahDistributionOutcome.NoDistribution, plan.Outcome);
	}

	[Fact]
	public void EvenSplit_DistributesRewardPerPlayer()
	{
		// Java parity: rewardPerPlayer = amount / onlineMembers.size().
		var plan = GroupKinahDistributionPlanService.Plan(amount: 90, distributorKinah: 1000, onlineMemberCount: 3, isTeamMember: true);
		Assert.Equal(GroupKinahDistributionOutcome.Distribute, plan.Outcome);
		Assert.Equal(30, plan.RewardPerPlayer);
		Assert.Equal(3, plan.OnlineMemberCount);
		Assert.Equal(90, plan.Amount);
	}

	[Fact]
	public void UnevenSplit_TruncatesLikeJavaIntegerDivision()
	{
		// Java parity: long division truncates (100 / 3 = 33); the remainder is not redistributed.
		var plan = GroupKinahDistributionPlanService.Plan(amount: 100, distributorKinah: 1000, onlineMemberCount: 3, isTeamMember: true);
		Assert.Equal(GroupKinahDistributionOutcome.Distribute, plan.Outcome);
		Assert.Equal(33, plan.RewardPerPlayer);
	}

	[Fact]
	public void AmountEqualToMemberCount_DistributesOnePerPlayer()
	{
		// Java parity: amount >= onlineMembers.size() boundary (amount == count) distributes 1 each.
		var plan = GroupKinahDistributionPlanService.Plan(amount: 3, distributorKinah: 1000, onlineMemberCount: 3, isTeamMember: true);
		Assert.Equal(GroupKinahDistributionOutcome.Distribute, plan.Outcome);
		Assert.Equal(1, plan.RewardPerPlayer);
	}

	[Fact]
	public void KinahExactlyEqualToAmount_IsNotInsufficient()
	{
		// Java parity: the guard is strict (<), so kinah == amount proceeds to distribution.
		var plan = GroupKinahDistributionPlanService.Plan(amount: 60, distributorKinah: 60, onlineMemberCount: 2, isTeamMember: true);
		Assert.Equal(GroupKinahDistributionOutcome.Distribute, plan.Outcome);
		Assert.Equal(30, plan.RewardPerPlayer);
	}

	[Fact]
	public void MsgSplitMeToB_HasCorrectIdAndOrderedParameters()
	{
		// Java parity: STR_MSG_SPLIT_ME_TO_B(1390247, amount, people, rewardPerPlayer).
		var packet = SmSystemMessage.MsgSplitMeToB(amount: 90, people: 3, rewardPerPlayer: 30);
		Assert.Equal(1390247, packet.MessageId);
		Assert.Equal(new[] { "90", "3", "30" }, packet.Parameters);
	}

	[Fact]
	public void MsgSplitBToMe_HasCorrectIdAndOrderedParameters()
	{
		// Java parity: STR_MSG_SPLIT_B_TO_ME(1390248, distributorName, amount, people, rewardPerPlayer).
		var packet = SmSystemMessage.MsgSplitBToMe(distributorName: "Boss", amount: 90, people: 3, rewardPerPlayer: 30);
		Assert.Equal(1390248, packet.MessageId);
		Assert.Equal(new[] { "Boss", "90", "3", "30" }, packet.Parameters);
	}
}
