using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerIncomingDamageObserverFanoutService
{
	private readonly WorldNpcResourceStatsService _resourceStats;
	private readonly EquipmentObserverBurnFanoutService _observerBurnFanout;

	public PlayerIncomingDamageObserverFanoutService(
		WorldNpcResourceStatsService resourceStats,
		EquipmentObserverBurnFanoutService observerBurnFanout)
	{
		_resourceStats = resourceStats;
		_observerBurnFanout = observerBurnFanout;
	}

	public async ValueTask<PlayerIncomingDamageObserverFanoutResult> ApplyIncomingHpDamageAndObserverBurnsAsync(
		Player defender,
		int maxHp,
		int damage,
		ItemTemplateTable itemTemplates,
		int skillId = 0,
		SmAttackStatusType? packetType = SmAttackStatusType.Damage,
		SmAttackStatusLog? packetLog = SmAttackStatusLog.Regular,
		Func<Player, IdianPolishBurnPlan, CancellationToken, Task<bool>>? saveIdianPolishBurnAsync = null,
		Func<Player, ItemChargeBurnPlan, CancellationToken, Task<bool>>? saveItemChargeBurnAsync = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: player incoming HP damage reaches life-stat packet side effects before equipment attacked observers update item packets.
		var damageResult = await _resourceStats.IncreasePlayerHpAsync(
			defender,
			maxHp,
			-damage,
			skillId,
			packetType,
			packetLog,
			cancellationToken: cancellationToken);

		var observerBurns = damageResult.NotifyHpObservers
			? await _observerBurnFanout.ApplyObserverBurnsAndSendPacketsAsync(
				defender,
				itemTemplates,
				EquipmentObserverBurnEvent.Attacked,
				skillId,
				saveIdianPolishBurnAsync,
				saveItemChargeBurnAsync,
				cancellationToken)
			: null;

		return new PlayerIncomingDamageObserverFanoutResult(damageResult, observerBurns);
	}
}

public sealed record PlayerIncomingDamageObserverFanoutResult(
	WorldNpcResourceChangeResult DamageResult,
	EquipmentObserverBurnFanoutResult? ObserverBurns);
