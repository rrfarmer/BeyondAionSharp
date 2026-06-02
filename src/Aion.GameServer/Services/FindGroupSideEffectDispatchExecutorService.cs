using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public sealed class FindGroupSideEffectDispatchExecutorService(IGameClientConnectionRegistry connectionRegistry)
{
	public async Task<FindGroupSideEffectDispatchExecutionPlan> ExecuteAsync(
		IEnumerable<FindGroupDirectPacketIntent>? directPacketIntents = null,
		IEnumerable<FindGroupWorldBroadcastIntent?>? worldBroadcastIntents = null)
	{
		// Java parity: FindGroupService uses PacketSendUtility.sendPacket for direct
		// player packets and PacketSendUtility.broadcastToWorld(packet, p -> p.getRace() == race)
		// for race-filtered world fanout. This executor is opt-in and is not wired into
		// GameServerConnection's CM_FIND_GROUP branch.
		var directResults = new List<FindGroupDirectPacketDispatchExecution>();
		foreach (var intent in directPacketIntents ?? [])
		{
			var sent = await connectionRegistry.SendPacketToPlayerAsync(intent.RecipientObjectId, intent.Packet);
			directResults.Add(new FindGroupDirectPacketDispatchExecution(
				intent.RecipientObjectId,
				intent.Packet.GetType().Name,
				intent.JavaSource,
				sent));
		}

		var worldBroadcastResults = new List<FindGroupWorldBroadcastDispatchExecution>();
		foreach (var intent in worldBroadcastIntents ?? [])
		{
			if (intent is null)
				continue;

			var sentCount = await connectionRegistry.BroadcastToWorldAsync(
				intent.Packet,
				player => string.Equals(player.Race, intent.Race, StringComparison.Ordinal));
			worldBroadcastResults.Add(new FindGroupWorldBroadcastDispatchExecution(
				intent.Race,
				intent.Packet.GetType().Name,
				intent.JavaSource,
				"p -> p.getRace() == recorded race",
				sentCount));
		}

		return new FindGroupSideEffectDispatchExecutionPlan(
			directResults,
			worldBroadcastResults,
			DispatchLiveSideEffects: true,
			"Opt-in executor only; CM_FIND_GROUP live boundary remains deferred.");
	}
}

public sealed record FindGroupSideEffectDispatchExecutionPlan(
	IReadOnlyList<FindGroupDirectPacketDispatchExecution> DirectPackets,
	IReadOnlyList<FindGroupWorldBroadcastDispatchExecution> WorldBroadcasts,
	bool DispatchLiveSideEffects,
	string BoundaryNote);

public sealed record FindGroupDirectPacketDispatchExecution(
	int RecipientObjectId,
	string PacketType,
	string JavaSource,
	bool Sent);

public sealed record FindGroupWorldBroadcastDispatchExecution(
	string Race,
	string PacketType,
	string JavaSource,
	string JavaFilter,
	int SentCount);
