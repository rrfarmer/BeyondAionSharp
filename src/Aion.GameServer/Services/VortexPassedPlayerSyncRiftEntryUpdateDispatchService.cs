using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus
{
	NoUpdate,
	NoPacketIntent,
	NoTargets,
	DisabledNoSend,
	MissingRegistry,
	Completed,
}

public enum VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus
{
	NotAttemptedDisabled,
	Sent,
	MissingConnection,
	FailedAndStopped,
}

public sealed record VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetResult(
	int PlayerObjectId,
	int Sequence,
	VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus Status,
	bool AttemptedSend,
	bool SentPacket,
	string JavaSource,
	string? FailureReason);

public sealed record VortexPassedPlayerSyncRiftEntryUpdateDispatchResult(
	VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus Status,
	VortexPassedPlayerSyncRiftEntryUpdateResult Update,
	IReadOnlyList<VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetResult> Targets,
	int SentCount,
	bool SendsPackets,
	bool IsEnabled,
	bool IsLive,
	bool StopsAfterFirstFailure,
	string JavaSource);

public sealed class VortexPassedPlayerSyncRiftEntryUpdateDispatchService
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly bool _enabled;

	public VortexPassedPlayerSyncRiftEntryUpdateDispatchService(
		IGameClientConnectionRegistry? connectionRegistry = null,
		bool enabled = false)
	{
		_connectionRegistry = connectionRegistry;
		_enabled = enabled;
	}

	public async Task<VortexPassedPlayerSyncRiftEntryUpdateDispatchResult> DispatchAsync(
		VortexPassedPlayerSyncRiftEntryUpdateResult update,
		IReadOnlyList<int> targetPlayerObjectIds,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(update);
		ArgumentNullException.ThrowIfNull(targetPlayerObjectIds);

		if (update.Status != VortexPassedPlayerSyncRiftEntryUpdateStatus.Updated || !update.AppliedPortalSync)
		{
			return CreateResult(
				VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.NoUpdate,
				update,
				[],
				sendsPackets: false,
				isLive: false);
		}

		if (!update.HasPacketIntent || update.Packet == null)
		{
			return CreateResult(
				VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.NoPacketIntent,
				update,
				[],
				sendsPackets: false,
				isLive: false);
		}

		if (targetPlayerObjectIds.Count == 0)
		{
			return CreateResult(
				VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.NoTargets,
				update,
				[],
				sendsPackets: false,
				isLive: false);
		}

		if (!_enabled)
		{
			return CreateResult(
				VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.DisabledNoSend,
				update,
				CreateDisabledTargets(targetPlayerObjectIds),
				sendsPackets: false,
				isLive: false);
		}

		if (_connectionRegistry == null)
		{
			return CreateResult(
				VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.MissingRegistry,
				update,
				CreateMissingRegistryTargets(targetPlayerObjectIds),
				sendsPackets: false,
				isLive: true);
		}

		var results = new List<VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetResult>();
		for (var i = 0; i < targetPlayerObjectIds.Count; i++)
		{
			var playerObjectId = targetPlayerObjectIds[i];
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				var sent = await _connectionRegistry.SendPacketToPlayerAsync(playerObjectId, update.Packet);
				results.Add(new VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetResult(
					playerObjectId,
					i,
					sent
						? VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.Sent
						: VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.MissingConnection,
					AttemptedSend: true,
					SentPacket: sent,
					"RiftInformer.syncRiftsState(player, packets) executed through the opt-in C# connection registry",
					FailureReason: null));
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				results.Add(new VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetResult(
					playerObjectId,
					i,
					VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.FailedAndStopped,
					AttemptedSend: true,
					SentPacket: false,
					"Java RiftInformer sends rift packets sequentially; a send failure stops this focused C# adapter before later targets",
					FailureReason: ex.Message));
				break;
			}
		}

		return CreateResult(
			VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.Completed,
			update,
			results,
			sendsPackets: true,
			isLive: true);
	}

	private static IReadOnlyList<VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetResult> CreateDisabledTargets(
		IReadOnlyList<int> targetPlayerObjectIds)
	{
		return targetPlayerObjectIds
			.Select((playerObjectId, index) => new VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetResult(
				playerObjectId,
				index,
				VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.NotAttemptedDisabled,
				AttemptedSend: false,
				SentPacket: false,
				"RiftInformer.syncRiftsState(player, packets) socket boundary identified; disabled C# adapter did not call SendPacketAsync",
				FailureReason: null))
			.ToArray();
	}

	private static IReadOnlyList<VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetResult> CreateMissingRegistryTargets(
		IReadOnlyList<int> targetPlayerObjectIds)
	{
		return targetPlayerObjectIds
			.Select((playerObjectId, index) => new VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetResult(
				playerObjectId,
				index,
				VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetStatus.MissingConnection,
				AttemptedSend: false,
				SentPacket: false,
				"RiftInformer.syncRiftsState could not execute because the C# connection registry was missing",
				FailureReason: null))
			.ToArray();
	}

	private static VortexPassedPlayerSyncRiftEntryUpdateDispatchResult CreateResult(
		VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus status,
		VortexPassedPlayerSyncRiftEntryUpdateResult update,
		IReadOnlyList<VortexPassedPlayerSyncRiftEntryUpdateDispatchTargetResult> targets,
		bool sendsPackets,
		bool isLive)
	{
		return new VortexPassedPlayerSyncRiftEntryUpdateDispatchResult(
			status,
			update,
			targets,
			targets.Count(target => target.SentPacket),
			sendsPackets,
			status == VortexPassedPlayerSyncRiftEntryUpdateDispatchStatus.Completed,
			isLive,
			StopsAfterFirstFailure: true,
			"controllers/RVController.syncPassed(true) -> services/rift/RiftInformer.sendRiftInfo -> PacketSendUtility.sendPacket(player, SM_RIFT_ANNOUNCE)");
	}
}
