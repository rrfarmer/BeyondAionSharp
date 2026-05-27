using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum PlayerReviveCleanupAdapterStatus
{
	DisabledPlanned,
	BlockedMissingLiveAggroList,
	LiveAggroCleared,
}

public sealed record PlayerReviveCleanupAdapterRequest(
	int PlayerObjectId,
	IReadOnlyList<PlayerAggroEntrySnapshot> PreReviveAggroEntries,
	bool ExecuteLiveAggroMutation = false,
	PlayerOwnedAggroList? LiveAggroList = null);

public sealed record PlayerReviveCleanupAdapterResult(
	PlayerReviveCleanupAdapterStatus Status,
	PlayerReviveCleanupPlan Plan,
	bool MutatedLiveAggro,
	bool ExposesPlanForObservation,
	string JavaSource,
	bool IsLive);

public sealed class PlayerReviveCleanupAdapterService
{
	private readonly PlayerReviveCleanupPlanService _planService;

	public PlayerReviveCleanupAdapterService(PlayerReviveCleanupPlanService? planService = null)
	{
		_planService = planService ?? new PlayerReviveCleanupPlanService();
	}

	public PlayerReviveCleanupAdapterResult Apply(PlayerReviveCleanupAdapterRequest request)
	{
		// Java parity breadcrumb: GameServerConnection.HandleReviveAsync currently
		// mirrors pieces of PlayerReviveService.kiskRevive. This adapter exposes
		// the planned PlayerReviveService.revive cleanup order without mutating a
		// live PlayerAggroList, which the C# port does not yet own.
		var plan = _planService.CreateKiskReviveCleanupPlan(
			request.PlayerObjectId,
			request.PreReviveAggroEntries);
		if (!request.ExecuteLiveAggroMutation)
		{
			return new PlayerReviveCleanupAdapterResult(
				PlayerReviveCleanupAdapterStatus.DisabledPlanned,
				plan,
				MutatedLiveAggro: false,
				ExposesPlanForObservation: true,
				"com.aionemu.gameserver.services.player.PlayerReviveService.kiskRevive -> revive cleanup plan exposed with live aggro mutation disabled",
				IsLive: false);
		}

		if (request.LiveAggroList is not null)
		{
			var clearedEntries = request.LiveAggroList.Clear();
			var livePlan = _planService.CreateKiskReviveCleanupPlan(
				request.PlayerObjectId,
				clearedEntries,
				isLive: true);
			return new PlayerReviveCleanupAdapterResult(
				PlayerReviveCleanupAdapterStatus.LiveAggroCleared,
				livePlan,
				MutatedLiveAggro: true,
				ExposesPlanForObservation: true,
				"com.aionemu.gameserver.services.player.PlayerReviveService.revive -> player.getAggroList().clear() executed against C# PlayerOwnedAggroList",
				IsLive: true);
		}

		return new PlayerReviveCleanupAdapterResult(
			PlayerReviveCleanupAdapterStatus.BlockedMissingLiveAggroList,
			plan,
			MutatedLiveAggro: false,
			ExposesPlanForObservation: true,
			"Live PlayerAggroList mutation remains blocked until the C# port has an executable player-owned aggro list",
			IsLive: false);
	}
}
