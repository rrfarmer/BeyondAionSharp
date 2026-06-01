using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public enum GroupDataExchangeFanoutSocketAdapterStatus
{
	NoPacket,
	DisabledNoSend,
	MissingRegistry,
	Completed,
	Failed,
}

public enum GroupDataExchangeFanoutSocketRecipientStatus
{
	NotAttemptedDisabled,
	Sent,
	MissingConnection,
	Failed,
}

public sealed record GroupDataExchangeFanoutSocketRecipientResult(
	int RecipientObjectId,
	GroupDataExchangeFanoutSocketRecipientStatus Status,
	bool AttemptedSend,
	bool SentPacket,
	string JavaSource,
	string? FailureReason);

public sealed record GroupDataExchangeFanoutSocketAdapterResult(
	GroupDataExchangeFanoutSocketAdapterStatus Status,
	GroupDataExchangeFanoutPlan FanoutPlan,
	IReadOnlyList<GroupDataExchangeFanoutSocketRecipientResult> RecipientResults,
	int SentCount,
	bool WouldCallBroadcastToVisiblePlayersAsync,
	bool DidCallBroadcastToVisiblePlayersAsync,
	bool WouldCallSendPacketToPlayerAsync,
	bool DidCallSendPacketToPlayerAsync,
	string JavaSource,
	bool IsLive);

public sealed class GroupDataExchangeFanoutSocketAdapterService
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly bool _enabled;

	public GroupDataExchangeFanoutSocketAdapterService(IGameClientConnectionRegistry? connectionRegistry = null, bool enabled = false)
	{
		_connectionRegistry = connectionRegistry;
		_enabled = enabled;
	}

	public async Task<GroupDataExchangeFanoutSocketAdapterResult> ExecuteAsync(
		GroupDataExchangeFanoutPlan fanoutPlan,
		WorldPosition sourcePosition,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CM_GROUP_DATA_EXCHANGE.runImpl calls PacketSendUtility.broadcastPacketAndReceive
		// for action 1 and PacketSendUtility.sendPacket for selected team members. This adapter keeps
		// that socket boundary opt-in and is not wired into GameServerConnection dispatch.
		if (fanoutPlan.Packet == null)
			return CreateNoPacketResult(fanoutPlan);

		if (!_enabled)
			return CreateDisabledResult(fanoutPlan);

		if (_connectionRegistry == null)
			return CreateMissingRegistryResult(fanoutPlan);

		if (fanoutPlan.Status == GroupDataExchangeFanoutPlanStatus.NearbyBroadcastVisiblePlayersAndSelf)
			return await ExecuteNearbyBroadcastAsync(fanoutPlan, sourcePosition, cancellationToken);

		return await ExecuteDirectRecipientsAsync(fanoutPlan, cancellationToken);
	}

	private async Task<GroupDataExchangeFanoutSocketAdapterResult> ExecuteNearbyBroadcastAsync(
		GroupDataExchangeFanoutPlan fanoutPlan,
		WorldPosition sourcePosition,
		CancellationToken cancellationToken)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var sent = await _connectionRegistry!.BroadcastToVisiblePlayersAsync(
				sourcePosition,
				fanoutPlan.SourcePlayerObjectId,
				fanoutPlan.Packet!,
				includeSourcePlayer: fanoutPlan.IncludeSourcePlayer);

			return new GroupDataExchangeFanoutSocketAdapterResult(
				GroupDataExchangeFanoutSocketAdapterStatus.Completed,
				fanoutPlan,
				Array.Empty<GroupDataExchangeFanoutSocketRecipientResult>(),
				sent,
				WouldCallBroadcastToVisiblePlayersAsync: true,
				DidCallBroadcastToVisiblePlayersAsync: true,
				WouldCallSendPacketToPlayerAsync: false,
				DidCallSendPacketToPlayerAsync: false,
				"PacketSendUtility.broadcastPacketAndReceive(player, SM_GROUP_DATA_EXCHANGE) executed through the opt-in C# visible-player registry",
				IsLive: true);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return CreateFailedResult(
				fanoutPlan,
				"PacketSendUtility.broadcastPacketAndReceive(player, SM_GROUP_DATA_EXCHANGE) threw at the opt-in C# broadcast boundary",
				didBroadcast: true,
				didDirectSend: false,
				ex.Message);
		}
	}

	private async Task<GroupDataExchangeFanoutSocketAdapterResult> ExecuteDirectRecipientsAsync(
		GroupDataExchangeFanoutPlan fanoutPlan,
		CancellationToken cancellationToken)
	{
		var results = new List<GroupDataExchangeFanoutSocketRecipientResult>();
		foreach (var recipientObjectId in fanoutPlan.RecipientObjectIds)
		{
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				var sent = await _connectionRegistry!.SendPacketToPlayerAsync(recipientObjectId, fanoutPlan.Packet!);
				results.Add(new GroupDataExchangeFanoutSocketRecipientResult(
					recipientObjectId,
					sent
						? GroupDataExchangeFanoutSocketRecipientStatus.Sent
						: GroupDataExchangeFanoutSocketRecipientStatus.MissingConnection,
					AttemptedSend: true,
					SentPacket: sent,
					sent
						? "PacketSendUtility.sendPacket(member, SM_GROUP_DATA_EXCHANGE) executed through the opt-in C# connection registry"
						: "PacketSendUtility.sendPacket(member, SM_GROUP_DATA_EXCHANGE) could not run because the member connection was missing",
					FailureReason: null));
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				results.Add(new GroupDataExchangeFanoutSocketRecipientResult(
					recipientObjectId,
					GroupDataExchangeFanoutSocketRecipientStatus.Failed,
					AttemptedSend: true,
					SentPacket: false,
					"PacketSendUtility.sendPacket(member, SM_GROUP_DATA_EXCHANGE) threw at the opt-in C# send boundary",
					ex.Message));
			}
		}

		return new GroupDataExchangeFanoutSocketAdapterResult(
			GroupDataExchangeFanoutSocketAdapterStatus.Completed,
			fanoutPlan,
			results,
			results.Count(result => result.SentPacket),
			WouldCallBroadcastToVisiblePlayersAsync: false,
			DidCallBroadcastToVisiblePlayersAsync: false,
			WouldCallSendPacketToPlayerAsync: fanoutPlan.RecipientObjectIds.Count > 0,
			DidCallSendPacketToPlayerAsync: results.Any(result => result.AttemptedSend),
			"PacketSendUtility.sendPacket(member, SM_GROUP_DATA_EXCHANGE) executed for planned team recipients through the opt-in C# connection registry",
			IsLive: true);
	}

	private static GroupDataExchangeFanoutSocketAdapterResult CreateNoPacketResult(GroupDataExchangeFanoutPlan fanoutPlan)
	{
		return new GroupDataExchangeFanoutSocketAdapterResult(
			GroupDataExchangeFanoutSocketAdapterStatus.NoPacket,
			fanoutPlan,
			Array.Empty<GroupDataExchangeFanoutSocketRecipientResult>(),
			SentCount: 0,
			WouldCallBroadcastToVisiblePlayersAsync: false,
			DidCallBroadcastToVisiblePlayersAsync: false,
			WouldCallSendPacketToPlayerAsync: false,
			DidCallSendPacketToPlayerAsync: false,
			"CM_GROUP_DATA_EXCHANGE socket adapter found no packet intent; no Java send boundary is reached",
			IsLive: false);
	}

	private static GroupDataExchangeFanoutSocketAdapterResult CreateDisabledResult(GroupDataExchangeFanoutPlan fanoutPlan)
	{
		return new GroupDataExchangeFanoutSocketAdapterResult(
			GroupDataExchangeFanoutSocketAdapterStatus.DisabledNoSend,
			fanoutPlan,
			fanoutPlan.RecipientObjectIds
				.Select(recipientObjectId => new GroupDataExchangeFanoutSocketRecipientResult(
					recipientObjectId,
					GroupDataExchangeFanoutSocketRecipientStatus.NotAttemptedDisabled,
					AttemptedSend: false,
					SentPacket: false,
					"CM_GROUP_DATA_EXCHANGE socket boundary identified; disabled C# adapter did not call SendPacketAsync",
					FailureReason: null))
				.ToArray(),
			SentCount: 0,
			WouldCallBroadcastToVisiblePlayersAsync: fanoutPlan.Status == GroupDataExchangeFanoutPlanStatus.NearbyBroadcastVisiblePlayersAndSelf,
			DidCallBroadcastToVisiblePlayersAsync: false,
			WouldCallSendPacketToPlayerAsync: fanoutPlan.RecipientObjectIds.Count > 0,
			DidCallSendPacketToPlayerAsync: false,
			"CM_GROUP_DATA_EXCHANGE socket boundary identified, but live C# sends remain disabled",
			IsLive: false);
	}

	private static GroupDataExchangeFanoutSocketAdapterResult CreateMissingRegistryResult(GroupDataExchangeFanoutPlan fanoutPlan)
	{
		return new GroupDataExchangeFanoutSocketAdapterResult(
			GroupDataExchangeFanoutSocketAdapterStatus.MissingRegistry,
			fanoutPlan,
			fanoutPlan.RecipientObjectIds
				.Select(recipientObjectId => new GroupDataExchangeFanoutSocketRecipientResult(
					recipientObjectId,
					GroupDataExchangeFanoutSocketRecipientStatus.MissingConnection,
					AttemptedSend: false,
					SentPacket: false,
					"PacketSendUtility.sendPacket could not execute because the C# connection registry was missing",
					FailureReason: null))
				.ToArray(),
			SentCount: 0,
			WouldCallBroadcastToVisiblePlayersAsync: fanoutPlan.Status == GroupDataExchangeFanoutPlanStatus.NearbyBroadcastVisiblePlayersAndSelf,
			DidCallBroadcastToVisiblePlayersAsync: false,
			WouldCallSendPacketToPlayerAsync: fanoutPlan.RecipientObjectIds.Count > 0,
			DidCallSendPacketToPlayerAsync: false,
			"CM_GROUP_DATA_EXCHANGE socket adapter was enabled, but no connection registry was available",
			IsLive: true);
	}

	private static GroupDataExchangeFanoutSocketAdapterResult CreateFailedResult(
		GroupDataExchangeFanoutPlan fanoutPlan,
		string javaSource,
		bool didBroadcast,
		bool didDirectSend,
		string failureReason)
	{
		return new GroupDataExchangeFanoutSocketAdapterResult(
			GroupDataExchangeFanoutSocketAdapterStatus.Failed,
			fanoutPlan,
			Array.Empty<GroupDataExchangeFanoutSocketRecipientResult>(),
			SentCount: 0,
			WouldCallBroadcastToVisiblePlayersAsync: didBroadcast,
			DidCallBroadcastToVisiblePlayersAsync: didBroadcast,
			WouldCallSendPacketToPlayerAsync: didDirectSend,
			DidCallSendPacketToPlayerAsync: didDirectSend,
			$"{javaSource}: {failureReason}",
			IsLive: true);
	}
}
