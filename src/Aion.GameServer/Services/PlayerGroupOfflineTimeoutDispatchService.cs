using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public sealed class PlayerGroupOfflineTimeoutDispatchService(
	PlayerGroupRuntime groupRuntime,
	IGameClientConnectionRegistry connectionRegistry)
{
	public async Task<PlayerGroupOfflineTimeoutDispatchResult?> DispatchNextExpiredAsync(
		DateTimeOffset now,
		int groupRemoveTimeSeconds = 600,
		CancellationToken cancellationToken = default)
	{
		// Java parity: PlayerGroupService.OfflinePlayerChecker fires one PlayerGroupLeavedEvent(LEAVE_TIMEOUT)
		// per expired offline member observed in a scan; callers can loop this method for a complete run.
		var timeoutPlan = groupRuntime.RemoveNextExpiredOfflineMemberWithLeavePlan(
			now,
			groupRemoveTimeSeconds);
		if (timeoutPlan == null)
			return null;

		var sentPackets = await DispatchLeavePlanAsync(timeoutPlan.LeavePlan, cancellationToken);
		return new PlayerGroupOfflineTimeoutDispatchResult(timeoutPlan, sentPackets);
	}

	public async Task<PlayerGroupOfflineTimeoutScanResult> DispatchExpiredScanAsync(
		DateTimeOffset now,
		int groupRemoveTimeSeconds = 600,
		CancellationToken cancellationToken = default)
	{
		// Java parity: OfflinePlayerChecker.run iterates groups and removes every expired offline member
		// found during the scheduled scan.
		var dispatchResults = new List<PlayerGroupOfflineTimeoutDispatchResult>();
		var totalSentPackets = 0;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var result = await DispatchNextExpiredAsync(now, groupRemoveTimeSeconds, cancellationToken);
			if (result == null)
				break;

			dispatchResults.Add(result);
			totalSentPackets += result.SentPacketCount;
		}

		return new PlayerGroupOfflineTimeoutScanResult(
			now,
			groupRemoveTimeSeconds,
			dispatchResults,
			totalSentPackets);
	}

	private async Task<int> DispatchLeavePlanAsync(
		PlayerGroupLeavePlan plan,
		CancellationToken cancellationToken)
	{
		var sentPackets = 0;
		foreach (var intent in plan.PacketIntents.OrderBy(intent => intent.Sequence))
			sentPackets += await SendGroupPacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

		foreach (var intent in plan.BaseLeavePlan.PacketIntents.OrderBy(intent => intent.Sequence))
			sentPackets += await SendGroupPacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

		return sentPackets;
	}

	private async Task<int> SendGroupPacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return await connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet) ? 1 : 0;
	}
}

public sealed record PlayerGroupOfflineTimeoutDispatchResult(
	PlayerGroupOfflineTimeoutPlan TimeoutPlan,
	int SentPacketCount);

public sealed record PlayerGroupOfflineTimeoutScanResult(
	DateTimeOffset ScanTime,
	int GroupRemoveTimeSeconds,
	IReadOnlyList<PlayerGroupOfflineTimeoutDispatchResult> DispatchResults,
	int SentPacketCount)
{
	public int TimedOutMemberCount => DispatchResults.Count;
}
