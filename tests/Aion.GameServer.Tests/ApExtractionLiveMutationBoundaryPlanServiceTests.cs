using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ApExtractionLiveMutationBoundaryPlanServiceTests
{
	[Fact]
	public void CreatePlan_RecordsJavaTargetDeleteBeforeToolDecreaseAndApAddBoundary()
	{
		var plan = ApExtractionLiveMutationBoundaryPlanService.CreatePlan();

		Assert.False(plan.RuntimeParityReady);
		Assert.True(plan.RequiresLiveStorageMutationBoundary);
		Assert.Contains("ApExtractAction.java", plan.JavaApExtractActionSource, StringComparison.Ordinal);
		Assert.Contains("AbyssPointsService.java#addAp", plan.JavaAbyssPointsServiceSource, StringComparison.Ordinal);
		Assert.Contains("CreateMutationPlan", plan.CSharpPlannerSource, StringComparison.Ordinal);
		Assert.Equal(
			[
				ApExtractionLiveMutationBoundaryStepKind.InitialCanActGuards,
				ApExtractionLiveMutationBoundaryStepKind.AcquisitionGuard,
				ApExtractionLiveMutationBoundaryStepKind.ApAmountCalculation,
				ApExtractionLiveMutationBoundaryStepKind.DeleteTarget,
				ApExtractionLiveMutationBoundaryStepKind.DecreaseTool,
				ApExtractionLiveMutationBoundaryStepKind.AddAbyssPoints,
			],
			plan.JavaMutationOrder.Select(step => step.Kind).ToArray());
		Assert.Contains(
			plan.MissingRuntimeBoundaries,
			boundary => boundary.Contains("Storage.delete", StringComparison.Ordinal)
				&& boundary.Contains("Storage.decreaseByObjectId", StringComparison.Ordinal));
		Assert.Contains(
			plan.MissingRuntimeBoundaries,
			boundary => boundary.Contains("AbyssPointsService.addAp", StringComparison.Ordinal));
		Assert.Contains(
			plan.MissingRuntimeBoundaries,
			boundary => boundary.Contains("Storage delete/update packets", StringComparison.Ordinal));
		Assert.Contains(
			plan.KnownCSharpLimitations,
			limitation => limitation.Contains("before target deletion", StringComparison.Ordinal));
		Assert.Contains(
			plan.KnownCSharpLimitations,
			limitation => limitation.Contains("AP plan failure", StringComparison.Ordinal));
		Assert.Contains(
			plan.KnownCSharpLimitations,
			limitation => limitation.Contains("persistence transaction", StringComparison.Ordinal));
	}
}
