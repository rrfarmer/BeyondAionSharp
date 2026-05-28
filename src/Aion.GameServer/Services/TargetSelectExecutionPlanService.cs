namespace Aion.GameServer.Services;

public static class TargetSelectExecutionPlanService
{
	public static TargetSelectExecutionPlan CreatePlan(
		TargetSelectResolutionPlan resolutionPlan,
		int currentPlayerTargetObjectId)
	{
		ArgumentNullException.ThrowIfNull(resolutionPlan);

		// Java parity breadcrumb: CM_TARGET_SELECT.runImpl either returns early after an
		// assist-key system message or calls player.setTarget(newTarget), which can then
		// trigger PlayerController.onTargetChanged and its target packets.
		if (!resolutionPlan.ShouldCallSetTarget)
		{
			return TargetSelectExecutionPlan.ReturnedEarly(
				resolutionPlan,
				currentPlayerTargetObjectId);
		}

		return TargetSelectExecutionPlan.WithTargetChange(
			resolutionPlan,
			currentPlayerTargetObjectId,
			PlayerTargetChangePacketPlanService.CreatePlan(
				resolutionPlan.PlayerObjectId,
				currentPlayerTargetObjectId,
				resolutionPlan.ResolvedTarget));
	}
}

public sealed record TargetSelectExecutionPlan(
	TargetSelectExecutionPlanStatus Status,
	TargetSelectResolutionPlan ResolutionPlan,
	int PreviousTargetObjectId,
	int NewTargetObjectId,
	TargetSelectSystemMessage SystemMessage,
	PlayerTargetChangePacketPlan? TargetChangePacketPlan,
	string JavaSource)
{
	public bool ShouldMutatePlayerTargetObjectId =>
		TargetChangePacketPlan?.ShouldUpdatePlayerTargetObjectId == true;

	public bool ShouldSendOwnerPacket =>
		TargetChangePacketPlan?.ShouldSendOwnerPacket == true;

	public bool ShouldBroadcastToSightedPlayers =>
		TargetChangePacketPlan?.ShouldBroadcastToSightedPlayers == true;

	public static TargetSelectExecutionPlan ReturnedEarly(
		TargetSelectResolutionPlan resolutionPlan,
		int currentPlayerTargetObjectId)
	{
		return new TargetSelectExecutionPlan(
			TargetSelectExecutionPlanStatus.ReturnedEarlyWithSystemMessage,
			resolutionPlan,
			currentPlayerTargetObjectId,
			currentPlayerTargetObjectId,
			resolutionPlan.SystemMessage,
			TargetChangePacketPlan: null,
			"CM_TARGET_SELECT.runImpl returned before player.setTarget after assist-key system message");
	}

	public static TargetSelectExecutionPlan WithTargetChange(
		TargetSelectResolutionPlan resolutionPlan,
		int currentPlayerTargetObjectId,
		PlayerTargetChangePacketPlan targetChangePacketPlan)
	{
		return new TargetSelectExecutionPlan(
			targetChangePacketPlan.Status == PlayerTargetChangePacketPlanStatus.PacketsCreated
				? TargetSelectExecutionPlanStatus.TargetChangePacketsCreated
				: TargetSelectExecutionPlanStatus.TargetUnchanged,
			resolutionPlan,
			currentPlayerTargetObjectId,
			targetChangePacketPlan.NewTargetObjectId,
			resolutionPlan.SystemMessage,
			targetChangePacketPlan,
			"CM_TARGET_SELECT.runImpl -> player.setTarget(newTarget) -> PlayerController.onTargetChanged packet plan");
	}
}

public enum TargetSelectExecutionPlanStatus
{
	TargetChangePacketsCreated,
	TargetUnchanged,
	ReturnedEarlyWithSystemMessage,
}
