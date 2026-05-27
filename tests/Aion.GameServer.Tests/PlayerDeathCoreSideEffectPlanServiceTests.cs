using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerDeathCoreSideEffectPlanServiceTests
{
	[Fact]
	public void CreatePlan_ModelsJavaPreStateSideEffectsWithoutMutatingRuntime()
	{
		var plan = PlayerDeathCoreSideEffectPlanService.CreatePlan(OwnerObjectId);

		Assert.Equal(PlayerDeathCoreSideEffectPlanStatus.Planned, plan.Status);
		Assert.Equal(OwnerObjectId, plan.OwnerObjectId);
		Assert.True(plan.WouldAbortMove);
		Assert.True(plan.WouldClearCasting);
		Assert.True(plan.WouldRemoveAllEffects);
		Assert.False(plan.MutatedMovement);
		Assert.False(plan.MutatedCasting);
		Assert.False(plan.MutatedEffects);
		Assert.False(plan.IsLive);
		Assert.Contains("CreatureController.onDie", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_OrdersAbortMoveBeforeClearCastingBeforeEffectRemoval()
	{
		var plan = PlayerDeathCoreSideEffectPlanService.CreatePlan(OwnerObjectId);

		AssertOrdered(
			plan.Steps,
			PlayerDeathCoreSideEffectPlanStep.AbortMove,
			PlayerDeathCoreSideEffectPlanStep.ClearCasting,
			PlayerDeathCoreSideEffectPlanStep.RemoveAllEffects);
	}

	private static void AssertOrdered(IReadOnlyList<PlayerDeathCoreSideEffectPlanStep> actual, params PlayerDeathCoreSideEffectPlanStep[] expected)
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
}
