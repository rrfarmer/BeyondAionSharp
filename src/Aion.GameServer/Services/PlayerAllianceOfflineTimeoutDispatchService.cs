using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceOfflineTimeoutDispatchService(
	PlayerAllianceRuntime allianceRuntime,
	PlayerLeagueRuntime? leagueRuntime,
	IGameClientConnectionRegistry connectionRegistry,
	VortexInvasionRuntime? vortexInvasionRuntime = null)
{
	public async Task<PlayerAllianceOfflineTimeoutDispatchResult?> DispatchNextExpiredAsync(
		DateTimeOffset now,
		int allianceRemoveTimeSeconds = 600,
		CancellationToken cancellationToken = default)
	{
		// Java parity: PlayerAllianceService.OfflinePlayerAllianceChecker fires one PlayerAllianceLeavedEvent(LEAVE_TIMEOUT)
		// per expired offline member; callers can loop this method to emulate the scheduled scan.
		var timeoutPlan = allianceRuntime.RemoveNextExpiredOfflineMemberWithLeaveWorkflow(
			now,
			allianceRemoveTimeSeconds);
		if (timeoutPlan == null)
			return null;

		var vortexRemoval = timeoutPlan.WouldRemoveOffenceInvader
			? vortexInvasionRuntime?.RemoveInvaderPlayer(timeoutPlan.TimedOutPlayer)
			: null;

		var sentPackets = await DispatchLeaveWorkflowAsync(
			timeoutPlan.LeaveWorkflowPlan,
			timeoutPlan.LeagueId,
			cancellationToken);

		return new PlayerAllianceOfflineTimeoutDispatchResult(
			timeoutPlan,
			sentPackets,
			timeoutPlan.WouldRemoveOffenceInvader,
			vortexRemoval);
	}

	public async Task<PlayerAllianceOfflineTimeoutScanResult> DispatchExpiredScanAsync(
		DateTimeOffset now,
		int allianceRemoveTimeSeconds = 600,
		CancellationToken cancellationToken = default)
	{
		// Java parity: OfflinePlayerAllianceChecker.run iterates alliances and fires LEAVE_TIMEOUT for
		// every expired offline member observed during the scheduled scan.
		var dispatchResults = new List<PlayerAllianceOfflineTimeoutDispatchResult>();
		var totalSentPackets = 0;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var result = await DispatchNextExpiredAsync(now, allianceRemoveTimeSeconds, cancellationToken);
			if (result == null)
				break;

			dispatchResults.Add(result);
			totalSentPackets += result.SentPacketCount;
		}

		return new PlayerAllianceOfflineTimeoutScanResult(
			now,
			allianceRemoveTimeSeconds,
			dispatchResults,
			totalSentPackets);
	}

	private async Task<int> DispatchLeaveWorkflowAsync(
		PlayerAllianceLeaveWorkflowPlan plan,
		int leagueId,
		CancellationToken cancellationToken)
	{
		if (leagueId != 0 && leagueRuntime == null)
			throw new InvalidOperationException("League runtime is required to dispatch in-league alliance timeout leaves.");

		var sentPackets = 0;
		if (leagueId != 0 && plan.AllianceLeavePlan.WouldDisband)
		{
			var (preDisbandIntents, disbandIntents, postDisbandIntents) = SplitAllianceDisbandIntents(plan);
			var leagueAllianceInfoByRecipient = CreateLeagueAllianceInfoByRecipient(plan, leagueId);
			foreach (var intent in preDisbandIntents)
				sentPackets += await SendAlliancePacketAsync(
					intent.RecipientObjectId,
					CreateAllianceLeavePacket(intent, leagueAllianceInfoByRecipient),
					cancellationToken);

			if (plan.AllianceLeavePlan.WouldBroadcastLeague)
				sentPackets += await DispatchLeagueBroadcastAsync(leagueId, plan.AllianceId, cancellationToken);

			var leagueLeavePlan = leagueRuntime!.RemoveAlliance(plan.AllianceId, allianceRuntime);
			foreach (var intent in leagueLeavePlan.PacketIntents.OrderBy(intent => intent.Sequence))
				sentPackets += await SendAlliancePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

			allianceRuntime.CompleteDeferredDisbandAfterLeaveWorkflow(plan.AllianceId);

			foreach (var intent in disbandIntents)
				sentPackets += await SendAlliancePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

			foreach (var intent in postDisbandIntents)
				sentPackets += await SendAlliancePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
		}
		else
		{
			var leagueAllianceInfoByRecipient = leagueId != 0 && plan.AllianceLeavePlan.WouldBroadcastLeague
				? CreateLeagueAllianceInfoByRecipient(plan, leagueId)
				: null;
			var (preBroadcastIntents, postBroadcastIntents) = leagueId != 0 && plan.AllianceLeavePlan.WouldBroadcastLeague
				? SplitAlliancePostBroadcastIntents(plan)
				: (plan.AllianceLeavePlan.PacketIntents.OrderBy(intent => intent.Sequence).ToArray(), Array.Empty<PlayerAlliancePacketIntent>());

			foreach (var intent in preBroadcastIntents)
				sentPackets += await SendAlliancePacketAsync(
					intent.RecipientObjectId,
					CreateAllianceLeavePacket(intent, leagueAllianceInfoByRecipient),
					cancellationToken);

			if (leagueId != 0 && plan.AllianceLeavePlan.WouldBroadcastLeague)
				sentPackets += await DispatchLeagueBroadcastAsync(leagueId, plan.AllianceId, cancellationToken);

			foreach (var intent in postBroadcastIntents)
				sentPackets += await SendAlliancePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
		}

		foreach (var intent in plan.BaseLeavePlan.PacketIntents.OrderBy(intent => intent.Sequence))
			sentPackets += await SendAlliancePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);

		return sentPackets;
	}

	private async Task<int> DispatchLeagueBroadcastAsync(
		int leagueId,
		int allianceId,
		CancellationToken cancellationToken)
	{
		var leagueBroadcastPlan = leagueRuntime!.BroadcastAllianceInfoExceptAlliance(
			leagueId,
			allianceId,
			allianceRuntime);
		if (leagueBroadcastPlan == null)
			return 0;

		var sentPackets = 0;
		foreach (var intent in leagueBroadcastPlan.PacketIntents.OrderBy(intent => intent.Sequence))
			sentPackets += await SendAlliancePacketAsync(intent.RecipientObjectId, intent.CreatePacket(), cancellationToken);
		return sentPackets;
	}

	private IReadOnlyDictionary<int, GameServerPacket>? CreateLeagueAllianceInfoByRecipient(
		PlayerAllianceLeaveWorkflowPlan plan,
		int leagueId)
	{
		// Java parity: PlayerAllianceLeavedEvent sends new SM_ALLIANCE_INFO(team) to remaining alliance members.
		// In-league packets expand the real league id, loot rules, and league rows.
		var leagueInfoPlan = leagueRuntime!.CreateAllianceInfoFanout(
			leagueId,
			plan.AllianceId,
			messageId: 0,
			message: string.Empty,
			allianceRuntime);
		return leagueInfoPlan?.PacketIntents
			.Where(intent => intent.Kind == PlayerLeaguePacketIntentKind.AllianceInfo)
			.ToDictionary(intent => intent.RecipientObjectId, intent => intent.CreatePacket());
	}

	private async Task<int> SendAlliancePacketAsync(
		int recipientObjectId,
		GameServerPacket packet,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return await connectionRegistry.SendPacketToPlayerAsync(recipientObjectId, packet) ? 1 : 0;
	}

	private static GameServerPacket CreateAllianceLeavePacket(
		PlayerAlliancePacketIntent intent,
		IReadOnlyDictionary<int, GameServerPacket>? leagueAllianceInfoByRecipient)
	{
		if (intent.Kind == PlayerAlliancePacketIntentKind.AllianceInfo
			&& leagueAllianceInfoByRecipient != null
			&& leagueAllianceInfoByRecipient.TryGetValue(intent.RecipientObjectId, out var packet))
			return packet;

		return intent.CreatePacket();
	}

	private static (
		IReadOnlyList<PlayerAlliancePacketIntent> PreBroadcastIntents,
		IReadOnlyList<PlayerAlliancePacketIntent> PostBroadcastIntents) SplitAlliancePostBroadcastIntents(PlayerAllianceLeaveWorkflowPlan plan)
	{
		const int forceBanMeMessageId = 1300979;

		var orderedIntents = plan.AllianceLeavePlan.PacketIntents
			.OrderBy(intent => intent.Sequence)
			.ToArray();
		var postBroadcastStartIndex = Array.FindIndex(
			orderedIntents,
			intent => intent.Kind == PlayerAlliancePacketIntentKind.SystemMessage
				&& intent.SystemMessage?.MessageId == forceBanMeMessageId);
		if (postBroadcastStartIndex < 0)
			return (orderedIntents, []);

		return (
			orderedIntents.Take(postBroadcastStartIndex).ToArray(),
			orderedIntents.Skip(postBroadcastStartIndex).ToArray());
	}

	private static (
		IReadOnlyList<PlayerAlliancePacketIntent> PreDisbandIntents,
		IReadOnlyList<PlayerAlliancePacketIntent> DisbandIntents,
		IReadOnlyList<PlayerAlliancePacketIntent> PostDisbandIntents) SplitAllianceDisbandIntents(PlayerAllianceLeaveWorkflowPlan plan)
	{
		// Java parity: PlayerAllianceLeavedEvent sends timeout fanout, PlayerAllianceService.disband(..., true)
		// emits LeagueLeftEvent, then AllianceDisbandEvent emits the remaining disband packets.
		const int partyAllianceDispersedMessageId = 1300201;
		const int forceBanMeMessageId = 1300979;

		var orderedIntents = plan.AllianceLeavePlan.PacketIntents
			.OrderBy(intent => intent.Sequence)
			.ToArray();
		var disbandStartIndex = Array.FindIndex(
			orderedIntents,
			intent => intent.Kind == PlayerAlliancePacketIntentKind.SystemMessage
				&& intent.SystemMessage?.MessageId == partyAllianceDispersedMessageId);
		if (disbandStartIndex < 0)
			return (orderedIntents, [], []);

		var postDisbandStartIndex = Array.FindIndex(
			orderedIntents,
			disbandStartIndex,
			intent => intent.Kind == PlayerAlliancePacketIntentKind.SystemMessage
				&& intent.SystemMessage?.MessageId == forceBanMeMessageId);

		if (postDisbandStartIndex < 0)
		{
			return (
				orderedIntents.Take(disbandStartIndex).ToArray(),
				orderedIntents.Skip(disbandStartIndex).ToArray(),
				[]);
		}

		return (
			orderedIntents.Take(disbandStartIndex).ToArray(),
			orderedIntents.Skip(disbandStartIndex).Take(postDisbandStartIndex - disbandStartIndex).ToArray(),
			orderedIntents.Skip(postDisbandStartIndex).ToArray());
	}
}

public sealed record PlayerAllianceOfflineTimeoutDispatchResult(
	PlayerAllianceOfflineTimeoutPlan TimeoutPlan,
	int SentPacketCount,
	bool WouldRemoveOffenceInvader,
	VortexInvaderRemovalResult? VortexInvaderRemoval = null)
{
	public bool RemovedOffenceInvader => VortexInvaderRemoval?.Removed == true;
}

public sealed record PlayerAllianceOfflineTimeoutScanResult(
	DateTimeOffset ScanTime,
	int AllianceRemoveTimeSeconds,
	IReadOnlyList<PlayerAllianceOfflineTimeoutDispatchResult> DispatchResults,
	int SentPacketCount)
{
	public int TimedOutMemberCount => DispatchResults.Count;

	public bool WouldRemoveAnyOffenceInvader => DispatchResults.Any(result => result.WouldRemoveOffenceInvader);
}
