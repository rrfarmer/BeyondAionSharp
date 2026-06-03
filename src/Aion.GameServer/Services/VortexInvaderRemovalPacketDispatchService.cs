using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum VortexInvaderRemovalPacketDispatchStatus
{
	NoRemoval,
	NoMessages,
	DisabledNoSend,
	MissingRegistry,
	Completed,
}

public enum VortexInvaderRemovalPacketDispatchMessageStatus
{
	NotAttemptedDisabled,
	Sent,
	MissingConnection,
	FailedAndStopped,
}

public sealed record VortexInvaderRemovalPacketDispatchMessageResult(
	int PlayerObjectId,
	int Sequence,
	int MessageId,
	VortexInvaderRemovalPacketDispatchMessageStatus Status,
	bool AttemptedSend,
	bool SentPacket,
	string JavaSource,
	string? FailureReason);

public sealed record VortexInvaderRemovalPacketDispatchResult(
	VortexInvaderRemovalPacketDispatchStatus Status,
	VortexInvaderRemovalResult Removal,
	IReadOnlyList<VortexInvaderRemovalPacketDispatchMessageResult> Messages,
	int SentCount,
	bool SendsPackets,
	bool IsEnabled,
	bool IsLive,
	bool StopsAfterFirstFailure,
	string JavaSource);

public sealed class VortexInvaderRemovalPacketDispatchService
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly bool _enabled;

	public VortexInvaderRemovalPacketDispatchService(IGameClientConnectionRegistry? connectionRegistry = null, bool enabled = false)
	{
		_connectionRegistry = connectionRegistry;
		_enabled = enabled;
	}

	public async Task<VortexInvaderRemovalPacketDispatchResult> DispatchAsync(
		VortexInvaderRemovalResult removal,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(removal);

		var messages = removal.SystemMessages ?? [];
		if (!removal.Removed || !removal.WasOnline)
		{
			return CreateResult(
				VortexInvaderRemovalPacketDispatchStatus.NoRemoval,
				removal,
				[],
				sendsPackets: false,
				isLive: false);
		}

		if (messages.Count == 0)
		{
			return CreateResult(
				VortexInvaderRemovalPacketDispatchStatus.NoMessages,
				removal,
				[],
				sendsPackets: false,
				isLive: false);
		}

		if (!_enabled)
		{
			return CreateResult(
				VortexInvaderRemovalPacketDispatchStatus.DisabledNoSend,
				removal,
				CreateDisabledMessages(removal),
				sendsPackets: false,
				isLive: false);
		}

		if (_connectionRegistry == null)
		{
			return CreateResult(
				VortexInvaderRemovalPacketDispatchStatus.MissingRegistry,
				removal,
				CreateMissingRegistryMessages(removal),
				sendsPackets: false,
				isLive: true);
		}

		var results = new List<VortexInvaderRemovalPacketDispatchMessageResult>();
		for (var i = 0; i < messages.Count; i++)
		{
			var message = messages[i];
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				var sent = await _connectionRegistry.SendPacketToPlayerAsync(removal.PlayerObjectId, message);
				results.Add(new VortexInvaderRemovalPacketDispatchMessageResult(
					removal.PlayerObjectId,
					i,
					message.MessageId,
					sent
						? VortexInvaderRemovalPacketDispatchMessageStatus.Sent
						: VortexInvaderRemovalPacketDispatchMessageStatus.MissingConnection,
					AttemptedSend: true,
					SentPacket: sent,
					"PacketSendUtility.sendPacket(player, SM_SYSTEM_MESSAGE) executed through the opt-in C# connection registry",
					FailureReason: null));
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				results.Add(new VortexInvaderRemovalPacketDispatchMessageResult(
					removal.PlayerObjectId,
					i,
					message.MessageId,
					VortexInvaderRemovalPacketDispatchMessageStatus.FailedAndStopped,
					AttemptedSend: true,
					SentPacket: false,
					"Java Invasion.kickPlayer sends Vortex messages sequentially; a send failure stops this focused C# executor before later messages",
					FailureReason: ex.Message));
				break;
			}
		}

		return CreateResult(
			VortexInvaderRemovalPacketDispatchStatus.Completed,
			removal,
			results,
			sendsPackets: true,
			isLive: true);
	}

	private static IReadOnlyList<VortexInvaderRemovalPacketDispatchMessageResult> CreateDisabledMessages(
		VortexInvaderRemovalResult removal)
	{
		var messages = removal.SystemMessages ?? [];
		return messages
			.Select((message, index) => new VortexInvaderRemovalPacketDispatchMessageResult(
				removal.PlayerObjectId,
				index,
				message.MessageId,
				VortexInvaderRemovalPacketDispatchMessageStatus.NotAttemptedDisabled,
				AttemptedSend: false,
				SentPacket: false,
				"PacketSendUtility.sendPacket(player, SM_SYSTEM_MESSAGE) socket boundary identified; disabled C# executor did not call SendPacketAsync",
				FailureReason: null))
			.ToArray();
	}

	private static IReadOnlyList<VortexInvaderRemovalPacketDispatchMessageResult> CreateMissingRegistryMessages(
		VortexInvaderRemovalResult removal)
	{
		var messages = removal.SystemMessages ?? [];
		return messages
			.Select((message, index) => new VortexInvaderRemovalPacketDispatchMessageResult(
				removal.PlayerObjectId,
				index,
				message.MessageId,
				VortexInvaderRemovalPacketDispatchMessageStatus.MissingConnection,
				AttemptedSend: false,
				SentPacket: false,
				"PacketSendUtility.sendPacket could not execute because the C# connection registry was missing",
				FailureReason: null))
			.ToArray();
	}

	private static VortexInvaderRemovalPacketDispatchResult CreateResult(
		VortexInvaderRemovalPacketDispatchStatus status,
		VortexInvaderRemovalResult removal,
		IReadOnlyList<VortexInvaderRemovalPacketDispatchMessageResult> messages,
		bool sendsPackets,
		bool isLive)
	{
		return new VortexInvaderRemovalPacketDispatchResult(
			status,
			removal,
			messages,
			messages.Count(message => message.SentPacket),
			sendsPackets,
			status == VortexInvaderRemovalPacketDispatchStatus.Completed,
			isLive,
			StopsAfterFirstFailure: true,
			"services/vortex/Invasion.kickPlayer -> PacketSendUtility.sendPacket(player, SM_SYSTEM_MESSAGE)");
	}
}
