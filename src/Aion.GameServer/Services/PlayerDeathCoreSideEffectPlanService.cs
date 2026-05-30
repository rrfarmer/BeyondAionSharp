namespace Aion.GameServer.Services;

public enum PlayerDeathCoreSideEffectPlanStatus
{
	Planned,
}

public enum PlayerDeathCoreSideEffectPlanStep
{
	AbortMove,
	ClearCasting,
	RemoveAllEffects,
}

public sealed record PlayerDeathCoreSideEffectPlan(
	PlayerDeathCoreSideEffectPlanStatus Status,
	int OwnerObjectId,
	bool WouldAbortMove,
	bool WouldClearCasting,
	bool WouldRemoveAllEffects,
	bool MutatedMovement,
	bool MutatedCasting,
	bool MutatedEffects,
	IReadOnlyList<PlayerDeathCoreSideEffectPlanStep> Steps,
	string JavaSource,
	bool IsLive
);

public static class PlayerDeathCoreSideEffectPlanService
{
	public static PlayerDeathCoreSideEffectPlan CreatePlan(int ownerObjectId)
	{
		// Java parity:
		// CreatureController.onDie performs these side effects before death-state
		// selection, observer notification, death emotion fanout, and aggro cleanup.
		return new PlayerDeathCoreSideEffectPlan(
			PlayerDeathCoreSideEffectPlanStatus.Planned,
			ownerObjectId,
			WouldAbortMove: true,
			WouldClearCasting: true,
			WouldRemoveAllEffects: true,
			MutatedMovement: false,
			MutatedCasting: false,
			MutatedEffects: false,
			new[]
			{
				PlayerDeathCoreSideEffectPlanStep.AbortMove,
				PlayerDeathCoreSideEffectPlanStep.ClearCasting,
				PlayerDeathCoreSideEffectPlanStep.RemoveAllEffects,
			},
			"com.aionemu.gameserver.controllers.CreatureController.onDie -> getMoveController().abortMove(); setCasting(null); getEffectController().removeAllEffects()",
			IsLive: false
		);
	}
}
