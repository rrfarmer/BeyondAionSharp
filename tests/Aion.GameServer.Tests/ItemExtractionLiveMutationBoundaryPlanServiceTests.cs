using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemExtractionLiveMutationBoundaryPlanServiceTests
{
	[Fact]
	public void CreatePlan_RecordsJavaTargetDeleteBeforeToolDecreaseBoundary()
	{
		var plan = ItemExtractionLiveMutationBoundaryPlanService.CreatePlan();

		Assert.False(plan.RuntimeParityReady);
		Assert.True(plan.RequiresLiveStorageMutationBoundary);
		Assert.Contains("ExtractAction.java", plan.JavaExtractActionSource, StringComparison.Ordinal);
		Assert.Contains("EnchantService.java#breakItem", plan.JavaEnchantServiceSource, StringComparison.Ordinal);
		Assert.Contains("CreateBreakItemPlan", plan.CSharpPlannerSource, StringComparison.Ordinal);
		Assert.Equal(
			[
				ItemExtractionLiveMutationBoundaryStepKind.InitialInventoryPresenceGuard,
				ItemExtractionLiveMutationBoundaryStepKind.CompatibilityGuard,
				ItemExtractionLiveMutationBoundaryStepKind.RewardStoneSelection,
				ItemExtractionLiveMutationBoundaryStepKind.DeleteTarget,
				ItemExtractionLiveMutationBoundaryStepKind.DecreaseTool,
				ItemExtractionLiveMutationBoundaryStepKind.AddRewardWhenToolDecreaseSucceeds,
				ItemExtractionLiveMutationBoundaryStepKind.FinalAnimationResult,
			],
			plan.JavaMutationOrder.Select(step => step.Kind).ToArray());
		Assert.Contains(
			plan.MissingRuntimeBoundaries,
			boundary => boundary.Contains("Storage.delete", StringComparison.Ordinal)
				&& boundary.Contains("Storage.decreaseByObjectId", StringComparison.Ordinal));
		Assert.Contains(
			plan.MissingRuntimeBoundaries,
			boundary => boundary.Contains("Storage.delete", StringComparison.Ordinal)
				&& boundary.Contains("success-animation", StringComparison.Ordinal));
		Assert.Contains(
			plan.KnownCSharpLimitations,
			limitation => limitation.Contains("snapshots inventory", StringComparison.Ordinal));
		Assert.Contains(
			plan.KnownCSharpLimitations,
			limitation => limitation.Contains("delete failure", StringComparison.Ordinal));
		Assert.Contains(
			plan.KnownCSharpLimitations,
			limitation => limitation.Contains("golden trace", StringComparison.Ordinal));
	}
}
