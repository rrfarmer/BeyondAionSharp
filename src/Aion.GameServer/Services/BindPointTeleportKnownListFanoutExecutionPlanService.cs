namespace Aion.GameServer.Services;

public enum BindPointTeleportKnownListFanoutExecutionPlanStatus
{
	Disabled,
	NoPacket,
}

public sealed record BindPointTeleportKnownListFanoutExecutionPlan(
	BindPointTeleportKnownListFanoutExecutionPlanStatus Status,
	BindPointTeleportKnownListFanoutTrace Trace,
	BindPointTeleportKnownListFanoutSendPolicy SendPolicy,
	bool SendsPackets,
	bool UsesMembershipSnapshot,
	bool UsesSourceFirstOrdering,
	bool UsesPlayerIsOnlineGate,
	bool ContinuesAfterRecipientFailure,
	string JavaSource,
	bool IsLive
);

public static class BindPointTeleportKnownListFanoutExecutionPlanService
{
	public static BindPointTeleportKnownListFanoutExecutionPlan CreateDisabledPlan(
		BindPointTeleportFanoutPlan? fanoutPlan,
		PlayerKnownListMembershipSnapshot? membershipSnapshot,
		IEnumerable<int>? onlinePlayerObjectIds,
		IEnumerable<int>? failingPlayerObjectIds = null,
		string? failureReason = null
	)
	{
		// Java parity: BindPointTeleportService.teleport broadcasts the teleport packet to the source
		// and nearby players through PacketSendUtility.broadcastPacket(player, packet, true); this planner
		// preserves the staged recipient ordering and failure policy without sending packets.
		var trace = BindPointTeleportKnownListFanoutMembershipAdapterService.CreateTrace(fanoutPlan, membershipSnapshot);
		var sendPolicy = BindPointTeleportKnownListFanoutSendPolicyService.CreatePolicy(
			trace,
			onlinePlayerObjectIds,
			failingPlayerObjectIds,
			failureReason
		);

		return new BindPointTeleportKnownListFanoutExecutionPlan(
			trace.Status == BindPointTeleportKnownListFanoutTraceStatus.NoPacket
				? BindPointTeleportKnownListFanoutExecutionPlanStatus.NoPacket
				: BindPointTeleportKnownListFanoutExecutionPlanStatus.Disabled,
			trace,
			sendPolicy,
			SendsPackets: false,
			UsesMembershipSnapshot: membershipSnapshot != null,
			UsesSourceFirstOrdering: trace.SendsSourceFirst,
			UsesPlayerIsOnlineGate: sendPolicy.UsesPlayerIsOnlineGate,
			ContinuesAfterRecipientFailure: sendPolicy.ContinuesAfterRecipientFailure,
			"BindPointTeleportService.teleport -> PacketSendUtility.broadcastPacket(player, packet, true)",
			IsLive: false
		);
	}
}
