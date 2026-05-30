using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus
{
	NoPacket,
	DisabledNoSend,
	MissingRegistry,
	Completed,
}

public enum PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus
{
	NotAttemptedDisabled,
	NotAttemptedSourceFailure,
	Sent,
	MissingConnection,
	FailedAndStopped,
	FailedAndContinued,
}

public sealed record PlayerProtectionActiveTaskSightedRecipientSocketRecipientResult(
	PlayerProtectionActiveTaskSightedRecipient Recipient,
	PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus Status,
	bool AttemptedSend,
	bool SentPacket,
	string JavaSource,
	string? FailureReason
);

public sealed record PlayerProtectionActiveTaskSightedRecipientSocketExecutorResult(
	PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus Status,
	PlayerProtectionActiveTaskSightedRecipientTrace Trace,
	IReadOnlyList<PlayerProtectionActiveTaskSightedRecipientSocketRecipientResult> Recipients,
	int SentCount,
	bool SendsPackets,
	bool IsEnabled,
	bool SourceFailureStopsKnownListTraversal,
	bool KnownListFailureContinuesTraversal,
	string JavaSource,
	bool IsLive
);

public sealed class PlayerProtectionActiveTaskSightedRecipientSocketExecutorService
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly bool _enabled;

	public PlayerProtectionActiveTaskSightedRecipientSocketExecutorService(
		IGameClientConnectionRegistry? connectionRegistry = null,
		bool enabled = false
	)
	{
		_connectionRegistry = connectionRegistry;
		_enabled = enabled;
	}

	public async Task<PlayerProtectionActiveTaskSightedRecipientSocketExecutorResult> ExecuteAsync(
		PlayerProtectionActiveTaskSightedRecipientTrace trace,
		GameServerPacket? packet,
		CancellationToken cancellationToken = default
	)
	{
		// Java parity: PacketSendUtility.broadcastToSightedPlayers sends to self first and then walks the
		// source known list, filtering recipients whose own known list still sees the source. This executor
		// models that socket-send boundary and failure behavior through the opt-in connection registry.
		if (trace.Status == PlayerProtectionActiveTaskSightedRecipientTraceStatus.NoBroadcast || packet == null)
		{
			return CreateResult(
				PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.NoPacket,
				trace,
				Array.Empty<PlayerProtectionActiveTaskSightedRecipientSocketRecipientResult>(),
				sendsPackets: false,
				isLive: false
			);
		}

		if (!_enabled)
		{
			return CreateResult(
				PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.DisabledNoSend,
				trace,
				trace
					.Recipients.Select(recipient => new PlayerProtectionActiveTaskSightedRecipientSocketRecipientResult(
						recipient,
						PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.NotAttemptedDisabled,
						AttemptedSend: false,
						SentPacket: false,
						"PacketSendUtility.broadcastToSightedPlayers socket boundary identified; disabled C# executor did not call SendPacketAsync",
						FailureReason: null
					))
					.ToArray(),
				sendsPackets: false,
				isLive: false
			);
		}

		if (_connectionRegistry == null)
		{
			return CreateResult(
				PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.MissingRegistry,
				trace,
				trace
					.Recipients.Select(recipient => new PlayerProtectionActiveTaskSightedRecipientSocketRecipientResult(
						recipient,
						PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.MissingConnection,
						AttemptedSend: false,
						SentPacket: false,
						"PacketSendUtility.sendPacket could not execute because the C# connection registry was missing",
						FailureReason: null
					))
					.ToArray(),
				sendsPackets: false,
				isLive: true
			);
		}

		var results = new List<PlayerProtectionActiveTaskSightedRecipientSocketRecipientResult>();
		var stopAfterSourceFailure = false;

		foreach (var recipient in trace.Recipients)
		{
			if (stopAfterSourceFailure)
			{
				results.Add(
					new PlayerProtectionActiveTaskSightedRecipientSocketRecipientResult(
						recipient,
						PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.NotAttemptedSourceFailure,
						AttemptedSend: false,
						SentPacket: false,
						"Known-list traversal was not reached because the projected source self-send failed first",
						FailureReason: null
					)
				);
				continue;
			}

			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				var sent = await _connectionRegistry.SendPacketToPlayerAsync(recipient.PlayerObjectId, packet);
				results.Add(
					new PlayerProtectionActiveTaskSightedRecipientSocketRecipientResult(
						recipient,
						sent
							? PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.Sent
							: PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.MissingConnection,
						AttemptedSend: true,
						SentPacket: sent,
						"PacketSendUtility.sendPacket(player, packet) executed through the opt-in C# connection registry",
						FailureReason: null
					)
				);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				var sourceSelfFailure = recipient.Kind == PlayerProtectionActiveTaskSightedRecipientKind.SourceSelf;
				stopAfterSourceFailure = sourceSelfFailure;
				results.Add(
					new PlayerProtectionActiveTaskSightedRecipientSocketRecipientResult(
						recipient,
						sourceSelfFailure
							? PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.FailedAndStopped
							: PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.FailedAndContinued,
						AttemptedSend: true,
						SentPacket: false,
						sourceSelfFailure
							? "PacketSendUtility.broadcastToSightedPlayers(..., true) sends source before known-list traversal; source failure prevents traversal"
							: "KnownList.forEachPlayer delegates through CollectionUtil.forEach; known-list recipient failure continues traversal",
						FailureReason: ex.Message
					)
				);
			}
		}

		return CreateResult(PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.Completed, trace, results, sendsPackets: true, isLive: true);
	}

	private static PlayerProtectionActiveTaskSightedRecipientSocketExecutorResult CreateResult(
		PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus status,
		PlayerProtectionActiveTaskSightedRecipientTrace trace,
		IReadOnlyList<PlayerProtectionActiveTaskSightedRecipientSocketRecipientResult> recipients,
		bool sendsPackets,
		bool isLive
	) =>
		new(
			status,
			trace,
			recipients,
			recipients.Count(recipient => recipient.SentPacket),
			sendsPackets,
			status == PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.Completed,
			SourceFailureStopsKnownListTraversal: true,
			KnownListFailureContinuesTraversal: true,
			"PacketSendUtility.broadcastToSightedPlayers(player, packet, true) -> sendPacket(source) -> KnownList.forEachPlayer(filter sees(source), sendPacket)",
			isLive
		);
}
