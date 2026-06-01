namespace Aion.GameServer.Services;

public static class FindGroupSideEffectDispatchAuditService
{
	public static FindGroupSideEffectDispatchAuditPlan CreateAuditPlan(
		IEnumerable<FindGroupDirectPacketIntent>? directPacketIntents = null,
		IEnumerable<FindGroupWorldBroadcastIntent?>? worldBroadcastIntents = null)
	{
		// Java parity: FindGroupService uses PacketSendUtility.sendPacket and
		// PacketSendUtility.broadcastToWorld for these side-effect records. This audit
		// contract does not call the C# connection registry; it only makes the live
		// boundaries explicit before any future opt-in executor is wired.
		var directPackets = (directPacketIntents ?? [])
			.Select(intent => new FindGroupDirectPacketDispatchAudit(
				intent.RecipientObjectId,
				intent.Packet.GetType().Name,
				intent.JavaSource))
			.ToArray();
		var worldBroadcasts = (worldBroadcastIntents ?? [])
			.Where(intent => intent != null)
			.Cast<FindGroupWorldBroadcastIntent>()
			.Select(intent => new FindGroupWorldBroadcastDispatchAudit(
				intent.Race,
				intent.Packet.GetType().Name,
				intent.JavaSource,
				"p -> p.getRace() == recorded race"))
			.ToArray();

		return new FindGroupSideEffectDispatchAuditPlan(
			directPackets,
			worldBroadcasts,
			DispatchLiveSideEffects: false);
	}
}

public sealed record FindGroupSideEffectDispatchAuditPlan(
	IReadOnlyList<FindGroupDirectPacketDispatchAudit> DirectPackets,
	IReadOnlyList<FindGroupWorldBroadcastDispatchAudit> WorldBroadcasts,
	bool DispatchLiveSideEffects);

public sealed record FindGroupDirectPacketDispatchAudit(
	int RecipientObjectId,
	string PacketType,
	string JavaSource);

public sealed record FindGroupWorldBroadcastDispatchAudit(
	string Race,
	string PacketType,
	string JavaSource,
	string JavaFilter);
