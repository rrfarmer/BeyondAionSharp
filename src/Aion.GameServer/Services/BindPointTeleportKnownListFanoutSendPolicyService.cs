namespace Aion.GameServer.Services;

public enum BindPointTeleportKnownListFanoutSendPolicyStatus
{
	Projected,
	NoPacket,
}

public enum BindPointTeleportKnownListFanoutRecipientSendStatus
{
	WouldSend,
	SkippedOffline,
	FailedAndContinued,
}

public sealed record BindPointTeleportKnownListFanoutRecipientSendPolicy(
	BindPointTeleportKnownListFanoutRecipient Recipient,
	BindPointTeleportKnownListFanoutRecipientSendStatus Status,
	bool UsesPlayerIsOnlineGate,
	bool ContinuesAfterFailure,
	string JavaSource,
	string? FailureReason);

public sealed record BindPointTeleportKnownListFanoutSendPolicy(
	BindPointTeleportKnownListFanoutSendPolicyStatus Status,
	BindPointTeleportKnownListFanoutTrace Trace,
	IReadOnlyList<BindPointTeleportKnownListFanoutRecipientSendPolicy> Recipients,
	bool UsesPacketSendUtilitySendPacket,
	bool UsesPlayerIsOnlineGate,
	bool ContinuesAfterRecipientFailure,
	string JavaSendMethod,
	string JavaIterationMethod,
	bool IsLive);

public static class BindPointTeleportKnownListFanoutSendPolicyService
{
	public static BindPointTeleportKnownListFanoutSendPolicy CreatePolicy(
		BindPointTeleportKnownListFanoutTrace trace,
		IEnumerable<int>? onlinePlayerObjectIds,
		IEnumerable<int>? failingPlayerObjectIds = null,
		string? failureReason = null)
	{
		if (trace.Status == BindPointTeleportKnownListFanoutTraceStatus.NoPacket)
		{
			return new BindPointTeleportKnownListFanoutSendPolicy(
				BindPointTeleportKnownListFanoutSendPolicyStatus.NoPacket,
				trace,
				Array.Empty<BindPointTeleportKnownListFanoutRecipientSendPolicy>(),
				UsesPacketSendUtilitySendPacket: false,
				UsesPlayerIsOnlineGate: true,
				ContinuesAfterRecipientFailure: true,
				"PacketSendUtility.sendPacket(Player,AionServerPacket)",
				"KnownList.forEachPlayer -> CollectionUtil.forEach",
				IsLive: false);
		}

		var onlinePlayers = onlinePlayerObjectIds?.ToHashSet() ?? [];
		var failingPlayers = failingPlayerObjectIds?.ToHashSet() ?? [];
		var recipients = new List<BindPointTeleportKnownListFanoutRecipientSendPolicy>();

		foreach (var recipient in trace.Recipients)
		{
			var status = BindPointTeleportKnownListFanoutRecipientSendStatus.WouldSend;
			var recipientFailureReason = (string?)null;

			// Java parity: PacketSendUtility.sendPacket no-ops when player.isOnline() is false.
			if (!onlinePlayers.Contains(recipient.PlayerObjectId))
			{
				status = BindPointTeleportKnownListFanoutRecipientSendStatus.SkippedOffline;
			}
			else if (failingPlayers.Contains(recipient.PlayerObjectId))
			{
				// Java parity: KnownList.forEach wraps recipient callbacks through CollectionUtil.forEach.
				status = BindPointTeleportKnownListFanoutRecipientSendStatus.FailedAndContinued;
				recipientFailureReason = failureReason ?? "Projected recipient send exception";
			}

			recipients.Add(new BindPointTeleportKnownListFanoutRecipientSendPolicy(
				recipient,
				status,
				UsesPlayerIsOnlineGate: true,
				ContinuesAfterFailure: true,
				"PacketSendUtility.sendPacket -> player.isOnline(); KnownList.forEachPlayer -> CollectionUtil.forEach",
				recipientFailureReason));
		}

		return new BindPointTeleportKnownListFanoutSendPolicy(
			BindPointTeleportKnownListFanoutSendPolicyStatus.Projected,
			trace,
			recipients,
			UsesPacketSendUtilitySendPacket: true,
			UsesPlayerIsOnlineGate: true,
			ContinuesAfterRecipientFailure: true,
			"PacketSendUtility.sendPacket(Player,AionServerPacket)",
			"KnownList.forEachPlayer -> CollectionUtil.forEach",
			IsLive: false);
	}
}
