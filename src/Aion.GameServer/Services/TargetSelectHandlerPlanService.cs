using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class TargetSelectHandlerPlanService
{
	public static TargetSelectHandlerPlan CreatePlan(Player player, TargetSelectHandlerInput input)
	{
		ArgumentNullException.ThrowIfNull(player);

		// Java parity breadcrumb: GameServerConnection.HandleTargetSelect currently handles
		// CM_TARGET_SELECT in C#. This non-live adapter stages the Java runImpl chain
		// without mutating Player.TargetObjectId or dispatching packets.
		var resolution = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: player.ObjectId,
			RequestedTargetObjectId: input.RequestedTargetObjectId,
			SelectTargetOfTarget: input.SelectTargetOfTarget,
			CurrentTargetObjectId: player.TargetObjectId,
			TargetOfTargetObjectId: input.CurrentTargetTargetObjectId,
			TargetOfTargetKnownByPlayer: input.CurrentTargetTargetKnownByPlayer,
			TargetOfTargetSeenByPlayer: input.CurrentTargetTargetSeenByPlayer,
			KnownTargetObjectId: input.KnownTargetObjectId,
			KnownTargetSeenByPlayer: input.KnownTargetSeenByPlayer,
			TeamMemberObjectId: input.TeamMemberObjectId));
		var execution = TargetSelectExecutionPlanService.CreatePlan(resolution, player.TargetObjectId);

		return new TargetSelectHandlerPlan(
			TargetSelectHandlerPlanStatus.Created,
			player.ObjectId,
			player.TargetObjectId,
			input,
			execution,
			"GameServerConnection.HandleTargetSelect staged CM_TARGET_SELECT.runImpl without live mutation or dispatch");
	}
}

public sealed record TargetSelectHandlerInput(
	int RequestedTargetObjectId,
	bool SelectTargetOfTarget,
	int CurrentTargetTargetObjectId = 0,
	bool CurrentTargetTargetKnownByPlayer = false,
	bool CurrentTargetTargetSeenByPlayer = false,
	int KnownTargetObjectId = 0,
	bool KnownTargetSeenByPlayer = false,
	int TeamMemberObjectId = 0);

public sealed record TargetSelectHandlerPlan(
	TargetSelectHandlerPlanStatus Status,
	int PlayerObjectId,
	int CurrentTargetObjectId,
	TargetSelectHandlerInput Input,
	TargetSelectExecutionPlan ExecutionPlan,
	string JavaSource)
{
	public int PlannedTargetObjectId => ExecutionPlan.NewTargetObjectId;

	public bool WouldMutatePlayerTargetObjectId => ExecutionPlan.ShouldMutatePlayerTargetObjectId;

	public bool WouldSendOwnerPacket => ExecutionPlan.ShouldSendOwnerPacket;

	public bool WouldBroadcastToSightedPlayers => ExecutionPlan.ShouldBroadcastToSightedPlayers;

	public TargetSelectSystemMessage SystemMessage => ExecutionPlan.SystemMessage;
}

public enum TargetSelectHandlerPlanStatus
{
	Created,
}
