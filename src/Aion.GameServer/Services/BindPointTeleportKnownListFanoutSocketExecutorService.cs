using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum BindPointTeleportKnownListFanoutSocketExecutorStatus
{
	NoPacket,
	DisabledNoSend,
	MissingRegistry,
	Completed,
}

public enum BindPointTeleportKnownListFanoutSocketRecipientStatus
{
	NotAttemptedDisabled,
	SkippedOffline,
	Sent,
	MissingConnection,
	FailedAndStopped,
	FailedAndContinued,
}

public sealed record BindPointTeleportKnownListFanoutSocketRecipientResult(
	BindPointTeleportKnownListFanoutRecipient Recipient,
	BindPointTeleportKnownListFanoutSocketRecipientStatus Status,
	bool AttemptedSend,
	bool SentPacket,
	string JavaSource,
	string? FailureReason);

public sealed record BindPointTeleportKnownListFanoutSocketExecutorResult(
	BindPointTeleportKnownListFanoutSocketExecutorStatus Status,
	BindPointTeleportKnownListFanoutExecutionPlan ExecutionPlan,
	IReadOnlyList<BindPointTeleportKnownListFanoutSocketRecipientResult> Recipients,
	int SentCount,
	bool SendsPackets,
	bool IsEnabled,
	bool SourceFailureStopsKnownListTraversal,
	bool KnownListFailureContinuesTraversal,
	string JavaSource,
	bool IsLive);

public sealed class BindPointTeleportKnownListFanoutSocketExecutorService
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly bool _enabled;

	public BindPointTeleportKnownListFanoutSocketExecutorService(
		IGameClientConnectionRegistry? connectionRegistry = null,
		bool enabled = false)
	{
		_connectionRegistry = connectionRegistry;
		_enabled = enabled;
	}

	public async Task<BindPointTeleportKnownListFanoutSocketExecutorResult> ExecuteAsync(
		BindPointTeleportKnownListFanoutExecutionPlan executionPlan,
		CancellationToken cancellationToken = default)
	{
		var packet = executionPlan.Trace.FanoutPlan?.Packet;
		if (executionPlan.Status == BindPointTeleportKnownListFanoutExecutionPlanStatus.NoPacket || packet == null)
		{
			return CreateResult(
				BindPointTeleportKnownListFanoutSocketExecutorStatus.NoPacket,
				executionPlan,
				Array.Empty<BindPointTeleportKnownListFanoutSocketRecipientResult>(),
				sendsPackets: false,
				isLive: false);
		}

		if (!_enabled)
		{
			return CreateResult(
				BindPointTeleportKnownListFanoutSocketExecutorStatus.DisabledNoSend,
				executionPlan,
				executionPlan.SendPolicy.Recipients.Select(recipient => new BindPointTeleportKnownListFanoutSocketRecipientResult(
					recipient.Recipient,
					BindPointTeleportKnownListFanoutSocketRecipientStatus.NotAttemptedDisabled,
					AttemptedSend: false,
					SentPacket: false,
					"PacketSendUtility.broadcastPacket(player, packet, true) socket boundary identified; disabled C# executor did not call SendPacketAsync",
					FailureReason: null)).ToArray(),
				sendsPackets: false,
				isLive: false);
		}

		if (_connectionRegistry == null)
		{
			return CreateResult(
				BindPointTeleportKnownListFanoutSocketExecutorStatus.MissingRegistry,
				executionPlan,
				executionPlan.SendPolicy.Recipients.Select(recipient => new BindPointTeleportKnownListFanoutSocketRecipientResult(
					recipient.Recipient,
					recipient.Status == BindPointTeleportKnownListFanoutRecipientSendStatus.SkippedOffline
						? BindPointTeleportKnownListFanoutSocketRecipientStatus.SkippedOffline
						: BindPointTeleportKnownListFanoutSocketRecipientStatus.MissingConnection,
					AttemptedSend: false,
					SentPacket: false,
					"PacketSendUtility.sendPacket could not execute because the C# connection registry was missing",
					FailureReason: null)).ToArray(),
				sendsPackets: false,
				isLive: true);
		}

		var results = new List<BindPointTeleportKnownListFanoutSocketRecipientResult>();
		var stopAfterSourceFailure = false;

		foreach (var policy in executionPlan.SendPolicy.Recipients)
		{
			if (stopAfterSourceFailure)
			{
				results.Add(new BindPointTeleportKnownListFanoutSocketRecipientResult(
					policy.Recipient,
					BindPointTeleportKnownListFanoutSocketRecipientStatus.NotAttemptedDisabled,
					AttemptedSend: false,
					SentPacket: false,
					"Known-list traversal was not reached because the projected source self-send failed first",
					FailureReason: null));
				continue;
			}

			if (policy.Status == BindPointTeleportKnownListFanoutRecipientSendStatus.SkippedOffline)
			{
				results.Add(new BindPointTeleportKnownListFanoutSocketRecipientResult(
					policy.Recipient,
					BindPointTeleportKnownListFanoutSocketRecipientStatus.SkippedOffline,
					AttemptedSend: false,
					SentPacket: false,
					policy.JavaSource,
					FailureReason: null));
				continue;
			}

			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				var sent = await _connectionRegistry.SendPacketToPlayerAsync(policy.Recipient.PlayerObjectId, packet);
				results.Add(new BindPointTeleportKnownListFanoutSocketRecipientResult(
					policy.Recipient,
					sent
						? BindPointTeleportKnownListFanoutSocketRecipientStatus.Sent
						: BindPointTeleportKnownListFanoutSocketRecipientStatus.MissingConnection,
					AttemptedSend: true,
					SentPacket: sent,
					"PacketSendUtility.sendPacket(player, packet) executed through the opt-in C# connection registry",
					FailureReason: null));
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				var sourceSelfFailure = policy.Recipient.Kind == BindPointTeleportKnownListFanoutRecipientKind.SourceSelf;
				stopAfterSourceFailure = sourceSelfFailure;
				results.Add(new BindPointTeleportKnownListFanoutSocketRecipientResult(
					policy.Recipient,
					sourceSelfFailure
						? BindPointTeleportKnownListFanoutSocketRecipientStatus.FailedAndStopped
						: BindPointTeleportKnownListFanoutSocketRecipientStatus.FailedAndContinued,
					AttemptedSend: true,
					SentPacket: false,
					sourceSelfFailure
						? "PacketSendUtility.broadcastPacket(player, packet, true) sends source before KnownList.forEachPlayer; source failure prevents traversal"
						: "KnownList.forEachPlayer delegates through CollectionUtil.forEach; known-list recipient failure continues traversal",
					FailureReason: ex.Message));
			}
		}

		return CreateResult(
			BindPointTeleportKnownListFanoutSocketExecutorStatus.Completed,
			executionPlan,
			results,
			sendsPackets: true,
			isLive: true);
	}

	private static BindPointTeleportKnownListFanoutSocketExecutorResult CreateResult(
		BindPointTeleportKnownListFanoutSocketExecutorStatus status,
		BindPointTeleportKnownListFanoutExecutionPlan executionPlan,
		IReadOnlyList<BindPointTeleportKnownListFanoutSocketRecipientResult> recipients,
		bool sendsPackets,
		bool isLive) =>
		new(
			status,
			executionPlan,
			recipients,
			recipients.Count(recipient => recipient.SentPacket),
			sendsPackets,
			status == BindPointTeleportKnownListFanoutSocketExecutorStatus.Completed,
			SourceFailureStopsKnownListTraversal: true,
			KnownListFailureContinuesTraversal: true,
			"PacketSendUtility.broadcastPacket(player, packet, true) -> sendPacket(source) -> KnownList.forEachPlayer(sendPacket)",
			isLive);
}
