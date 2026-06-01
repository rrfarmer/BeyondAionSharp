using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed record GroupDataExchangeHandlerCompositionPlan(
	GroupDataExchangeFanoutPlan FanoutPlan,
	GroupDataExchangeFanoutSocketAdapterResult SocketAdapterResult,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource);

public static class GroupDataExchangeHandlerCompositionPlanService
{
	public static async Task<GroupDataExchangeHandlerCompositionPlan> CreateDisabledPlanAsync(
		Player? player,
		byte action,
		byte groupType,
		byte unknown2,
		byte[] data,
		PlayerGroupRuntime groupRuntime,
		PlayerAllianceRuntime allianceRuntime,
		PlayerLeagueRuntime leagueRuntime,
		IGameClientConnectionRegistry? connectionRegistry,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CM_GROUP_DATA_EXCHANGE.runImpl composes SM_GROUP_DATA_EXCHANGE and then reaches
		// PacketSendUtility. This disabled composition preserves that handoff without performing sends.
		var fanoutPlan = GroupDataExchangeFanoutPlanService.CreatePlan(
			player,
			action,
			groupType,
			unknown2,
			data,
			groupRuntime,
			allianceRuntime,
			leagueRuntime);
		var socketAdapter = new GroupDataExchangeFanoutSocketAdapterService(connectionRegistry, enabled: false);
		var socketResult = await socketAdapter.ExecuteAsync(
			fanoutPlan,
			player?.Position ?? new WorldPosition(0, 0, 0, 0, 0),
			cancellationToken);

		return new GroupDataExchangeHandlerCompositionPlan(
			fanoutPlan,
			socketResult,
			ShouldDispatchLiveSideEffects: false,
			"CM_GROUP_DATA_EXCHANGE.runImpl -> SM_GROUP_DATA_EXCHANGE fanout planned, socket boundary disabled in C#");
	}
}
