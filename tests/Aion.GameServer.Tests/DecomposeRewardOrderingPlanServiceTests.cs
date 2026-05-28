using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class DecomposeRewardOrderingPlanServiceTests
{
	[Fact]
	public void CreatePlan_RecordsJavaSuccessMessageBeforeRewardPackets()
	{
		var plan = DecomposeRewardOrderingPlanService.CreatePlan();

		Assert.False(plan.RuntimeParityReady);
		Assert.True(plan.RequiresRuntimeGoldenForFullParity);
		Assert.Contains("DecomposeAction.java", plan.JavaDecomposeActionSource, StringComparison.Ordinal);
		Assert.Contains("CompleteDecomposeUseItemAsync", plan.CSharpHandlerSource, StringComparison.Ordinal);
		Assert.Equal(
			[
				DecomposeRewardOrderingStepKind.StartAnimation,
				DecomposeRewardOrderingStepKind.DelayedPostValidate,
				DecomposeRewardOrderingStepKind.SourceConsumePacket,
				DecomposeRewardOrderingStepKind.SuccessMessage,
				DecomposeRewardOrderingStepKind.RewardAddPackets,
				DecomposeRewardOrderingStepKind.FinalAnimation,
			],
			plan.JavaVisibleOrder.Select(step => step.Kind).ToArray());
		Assert.Contains(
			plan.CoveredCSharpBehaviors,
			behavior => behavior.Contains("success message, then reward packets", StringComparison.Ordinal));
		Assert.Contains(
			plan.CoveredCSharpBehaviors,
			behavior => behavior.Contains("selectable decompose packet contracts", StringComparison.Ordinal));
		Assert.Contains(
			plan.RemainingRisks,
			risk => risk.Contains("reward add return values are ignored", StringComparison.Ordinal));
		Assert.Contains(
			plan.RemainingRisks,
			risk => risk.Contains("runtime packet artifacts", StringComparison.Ordinal));
	}
}
