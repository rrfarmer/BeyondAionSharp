using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public enum BindPointTeleportRuntimeCallbackExecutionStatus
{
	StoppedNotEnoughKinah,
	MissingCooldownOrFanoutMetadata,
	StoredCooldownAndBroadcast,
}

public sealed record BindPointTeleportRuntimeCallbackExecutionResult(
	BindPointTeleportRuntimeCallbackExecutionStatus Status,
	BindPointTeleportScheduledCallbackPlan CallbackPlan,
	BindPointTeleportCooldownFact? StoredCooldown,
	BindPointTeleportRuntimeFanoutResult? FanoutResult,
	bool ShouldSendNotEnoughFee,
	bool StoredCooldownFact,
	bool BroadcastCooldown,
	bool ShouldScheduleFinalTeleport,
	bool ShouldTeleport,
	string JavaSource,
	bool IsLive);

public sealed class BindPointTeleportRuntimeCallbackExecutionBridgeService
{
	private readonly BindPointTeleportRuntimeStateOwner _runtimeStateOwner;
	private readonly BindPointTeleportRuntimeFanoutService _fanoutService;

	public BindPointTeleportRuntimeCallbackExecutionBridgeService(
		BindPointTeleportRuntimeStateOwner runtimeStateOwner,
		BindPointTeleportRuntimeFanoutService fanoutService)
	{
		_runtimeStateOwner = runtimeStateOwner;
		_fanoutService = fanoutService;
	}

	public async Task<BindPointTeleportRuntimeCallbackExecutionResult> ExecuteCooldownFanoutAsync(
		int playerObjectId,
		BindPointTeleportScheduledCallbackPlan callbackPlan,
		WorldPosition sourcePosition,
		long currentTimeMillis,
		CancellationToken cancellationToken = default)
	{
		// Java parity: BindPointTeleportService.teleport scheduled callback first tries Kinah, then addCooldown,
		// broadcasts action 3, and only then schedules final movement. Kinah mutation and movement remain staged out here.
		cancellationToken.ThrowIfCancellationRequested();
		if (!callbackPlan.KinahPlan.ShouldContinueScheduledTeleport)
		{
			return new BindPointTeleportRuntimeCallbackExecutionResult(
				BindPointTeleportRuntimeCallbackExecutionStatus.StoppedNotEnoughKinah,
				callbackPlan,
				StoredCooldown: null,
				FanoutResult: null,
				ShouldSendNotEnoughFee: callbackPlan.ShouldSendNotEnoughFee,
				StoredCooldownFact: false,
				BroadcastCooldown: false,
				ShouldScheduleFinalTeleport: false,
				ShouldTeleport: false,
				"BindPointTeleportService.teleport scheduled task -> tryDecreaseKinah failed -> send fee message and return before cooldown/fanout/movement",
				IsLive: false);
		}

		if (!callbackPlan.ShouldStoreCooldown
			|| !callbackPlan.ShouldBroadcastCooldown
			|| callbackPlan.CooldownPlan?.LocId is not int locId
			|| callbackPlan.CooldownFanoutPlan == null)
		{
			return new BindPointTeleportRuntimeCallbackExecutionResult(
				BindPointTeleportRuntimeCallbackExecutionStatus.MissingCooldownOrFanoutMetadata,
				callbackPlan,
				StoredCooldown: null,
				FanoutResult: null,
				ShouldSendNotEnoughFee: false,
				StoredCooldownFact: false,
				BroadcastCooldown: false,
				callbackPlan.ShouldScheduleFinalTeleport,
				callbackPlan.ShouldTeleport,
				"C# staging guard: successful scheduled callback requires cooldown and action 3 fanout metadata before runtime execution",
				IsLive: false);
		}

		var cooldown = _runtimeStateOwner.AddCooldown(playerObjectId, locId, currentTimeMillis);
		var fanoutResult = await _fanoutService.BroadcastFanoutPlanAsync(
			callbackPlan.CooldownFanoutPlan,
			sourcePosition,
			cancellationToken);
		return new BindPointTeleportRuntimeCallbackExecutionResult(
			BindPointTeleportRuntimeCallbackExecutionStatus.StoredCooldownAndBroadcast,
			callbackPlan,
			cooldown,
			fanoutResult,
			ShouldSendNotEnoughFee: false,
			StoredCooldownFact: true,
			BroadcastCooldown: fanoutResult.SentPacket,
			callbackPlan.ShouldScheduleFinalTeleport,
			callbackPlan.ShouldTeleport,
			"BindPointTeleportService.teleport scheduled task -> addCooldown -> broadcast action 3 -> schedule final teleport; final movement remains staged",
			IsLive: false);
	}
}
