using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskExecutionBridgeStatus
{
	NoBroadcast,
	PlannedDisabledNoSend,
	LiveVisualMutationNoSend,
	LiveSocketExecution,
}

public sealed record PlayerProtectionActiveTaskExecutionBridgeResult(
	PlayerProtectionActiveTaskExecutionBridgeStatus Status,
	PlayerProtectionActiveTaskAdapterResult AdapterResult,
	PlayerProtectionActiveTaskSideEffectOperationPlan SideEffectOperationPlan,
	PlayerProtectionAttackUtilRecipientPlan AttackUtilRecipientPlan,
	SmPlayerState? Packet,
	PlayerProtectionActiveTaskSightedRecipientSocketExecutorResult SocketExecutorResult,
	bool ConstructedPacket,
	bool SentPackets,
	bool UsesDisabledSocketExecutorByDefault,
	string JavaSource,
	bool IsLive);

public sealed record PlayerProtectionActiveTaskExecutionBridgeRequest(
	PlayerProtectionActiveTaskAdapterRequest AdapterRequest,
	IReadOnlyList<PlayerProtectionAttackUtilKnownObjectFact>? KnownObjectFacts = null,
	bool ValidateSeeForTargetRemoval = false);

public sealed class PlayerProtectionActiveTaskExecutionBridgeService
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly bool _enableSocketExecutor;

	public PlayerProtectionActiveTaskExecutionBridgeService(
		IGameClientConnectionRegistry? connectionRegistry = null,
		bool enableSocketExecutor = false)
	{
		_connectionRegistry = connectionRegistry;
		_enableSocketExecutor = enableSocketExecutor;
	}

	public async Task<PlayerProtectionActiveTaskExecutionBridgeResult> ExecuteAsync(
		PlayerProtectionActiveTaskAdapterRequest request,
		CancellationToken cancellationToken = default) =>
		await ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(request), cancellationToken);

	public async Task<PlayerProtectionActiveTaskExecutionBridgeResult> ExecuteAsync(
		PlayerProtectionActiveTaskExecutionBridgeRequest request,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var adapterResult = PlayerProtectionActiveTaskAdapterService.Apply(request.AdapterRequest);
		var sideEffectOperationPlan = PlayerProtectionActiveTaskSideEffectOperationPlanService.Create(
			request.AdapterRequest,
			adapterResult);
		var attackUtilRecipientPlan = PlayerProtectionAttackUtilRecipientPlannerService.CreatePlan(
			request.AdapterRequest.Player.ObjectId,
			request.KnownObjectFacts,
			request.ValidateSeeForTargetRemoval);
		var packet = adapterResult.FanoutPlan.ShouldBroadcast
			? new SmPlayerState(request.AdapterRequest.Player)
			: null;
		var executor = new PlayerProtectionActiveTaskSightedRecipientSocketExecutorService(
			_connectionRegistry,
			_enableSocketExecutor);
		var executorResult = await executor.ExecuteAsync(
			adapterResult.SightedRecipientTrace,
			packet,
			cancellationToken);
		var status = ResolveStatus(adapterResult, executorResult);

		return new PlayerProtectionActiveTaskExecutionBridgeResult(
			status,
			adapterResult,
			sideEffectOperationPlan,
			attackUtilRecipientPlan,
			packet,
			executorResult,
			ConstructedPacket: packet != null,
			SentPackets: executorResult.SentCount > 0,
			UsesDisabledSocketExecutorByDefault: !_enableSocketExecutor,
			"PlayerController protection task -> mutate visual state -> new SM_PLAYER_STATE(player) -> PacketSendUtility.broadcastToSightedPlayers(player, packet, true)",
			IsLive: adapterResult.IsLive || executorResult.IsLive);
	}

	private static PlayerProtectionActiveTaskExecutionBridgeStatus ResolveStatus(
		PlayerProtectionActiveTaskAdapterResult adapterResult,
		PlayerProtectionActiveTaskSightedRecipientSocketExecutorResult executorResult)
	{
		if (!adapterResult.FanoutPlan.ShouldBroadcast)
			return PlayerProtectionActiveTaskExecutionBridgeStatus.NoBroadcast;
		if (executorResult.SendsPackets)
			return PlayerProtectionActiveTaskExecutionBridgeStatus.LiveSocketExecution;
		if (adapterResult.MutatedVisualState)
			return PlayerProtectionActiveTaskExecutionBridgeStatus.LiveVisualMutationNoSend;

		return PlayerProtectionActiveTaskExecutionBridgeStatus.PlannedDisabledNoSend;
	}
}
