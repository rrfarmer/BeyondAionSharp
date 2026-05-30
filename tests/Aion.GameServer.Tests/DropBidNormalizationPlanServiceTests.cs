using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class DropBidNormalizationPlanServiceTests
{
	[Fact]
	public void CreatePlan_ValidBidWithEnoughKinahPassesThrough()
	{
		var plan = DropBidNormalizationPlanService.CreatePlan(requestedBid: 1000, playerKinah: 5000);

		Assert.Equal(DropBidNormalizationPlanStatus.BidValid, plan.Status);
		Assert.Equal(1000, plan.NormalizedBid);
		Assert.False(plan.BidNormalized);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_ZeroBidPassesThrough()
	{
		// bid = 0 means passing; not invalid
		var plan = DropBidNormalizationPlanService.CreatePlan(requestedBid: 0, playerKinah: 100);

		Assert.Equal(DropBidNormalizationPlanStatus.BidValid, plan.Status);
		Assert.Equal(0, plan.NormalizedBid);
	}

	[Fact]
	public void CreatePlan_NegativeBidNormalizesToZero()
	{
		var plan = DropBidNormalizationPlanService.CreatePlan(requestedBid: -1, playerKinah: 1000);

		Assert.Equal(DropBidNormalizationPlanStatus.BidNormalizedToZero, plan.Status);
		Assert.Equal(0, plan.NormalizedBid);
		Assert.True(plan.BidNormalized);
	}

	[Fact]
	public void CreatePlan_BidExceedingMaxNormalizesToZero()
	{
		var plan = DropBidNormalizationPlanService.CreatePlan(requestedBid: 1_000_000_000, playerKinah: 2_000_000_000);

		Assert.Equal(DropBidNormalizationPlanStatus.BidNormalizedToZero, plan.Status);
		Assert.Equal(0, plan.NormalizedBid);
	}

	[Fact]
	public void CreatePlan_MaxBidWithEnoughKinahIsValid()
	{
		var plan = DropBidNormalizationPlanService.CreatePlan(
			requestedBid: DropBidNormalizationPlanService.MaxBid,
			playerKinah: DropBidNormalizationPlanService.MaxBid);

		Assert.Equal(DropBidNormalizationPlanStatus.BidValid, plan.Status);
		Assert.Equal(999_999_999, plan.NormalizedBid);
	}

	[Fact]
	public void CreatePlan_BidAboveKinahNormalizesToZero()
	{
		var plan = DropBidNormalizationPlanService.CreatePlan(requestedBid: 5000, playerKinah: 100);

		Assert.Equal(DropBidNormalizationPlanStatus.BidNormalizedToZero, plan.Status);
		Assert.Equal(0, plan.NormalizedBid);
	}

	[Fact]
	public void MaxBidConstantIs999999999()
	{
		Assert.Equal(999_999_999L, DropBidNormalizationPlanService.MaxBid);
	}
}
