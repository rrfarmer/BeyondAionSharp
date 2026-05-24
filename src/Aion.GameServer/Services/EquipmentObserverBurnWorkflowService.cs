using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public static class EquipmentObserverBurnWorkflowService
{
	public static async Task<EquipmentObserverBurnWorkflowResult> ApplyObserverBurnsAsync(
		Player player,
		ItemTemplateTable itemTemplates,
		EquipmentObserverBurnEvent observerEvent,
		int skillId,
		Func<Player, IdianPolishBurnPlan, CancellationToken, Task<bool>>? saveIdianPolishBurnAsync = null,
		Func<Player, ItemChargeBurnPlan, CancellationToken, Task<bool>>? saveItemChargeBurnAsync = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/items/IdianStone.onEquip registers its observer before ItemEquipmentListener registers ChargeInfo.
		var idianPlan = IdianPolishService.BurnEquippedWeaponPolishChargeForObserverEvent(
			player,
			itemTemplates,
			MapIdianEvent(observerEvent),
			skillId);
		var idianApplication = IdianPolishBurnApplicationService.ApplyBurnPlan(player, idianPlan, itemTemplates);
		var idianPersisted = saveIdianPolishBurnAsync == null || !idianPlan.Changed
			|| await saveIdianPolishBurnAsync(player, idianPlan, cancellationToken);

		var chargePlan = ItemChargeService.BurnEquippedChargePoints(
			player,
			itemTemplates,
			MapChargeEvent(observerEvent),
			skillId);
		var chargeApplication = ItemChargeBurnApplicationService.ApplyBurnPlan(player, chargePlan, itemTemplates);
		var chargePersisted = saveItemChargeBurnAsync == null || !chargePlan.Changed
			|| await saveItemChargeBurnAsync(player, chargePlan, cancellationToken);

		var packets = idianApplication.Packets.Concat(chargeApplication.Packets).ToArray();
		return new EquipmentObserverBurnWorkflowResult(
			idianPlan.Changed || chargePlan.Changed,
			idianPlan,
			chargePlan,
			idianApplication,
			chargeApplication,
			packets,
			idianPersisted,
			chargePersisted);
	}

	private static IdianPolishObserverEvent MapIdianEvent(EquipmentObserverBurnEvent observerEvent)
	{
		return observerEvent switch
		{
			EquipmentObserverBurnEvent.Attack => IdianPolishObserverEvent.Attack,
			EquipmentObserverBurnEvent.Attacked => IdianPolishObserverEvent.Attacked,
			EquipmentObserverBurnEvent.DotAttacked => IdianPolishObserverEvent.DotAttacked,
			_ => throw new ArgumentOutOfRangeException(nameof(observerEvent), observerEvent, null),
		};
	}

	private static ItemChargeObserverEvent MapChargeEvent(EquipmentObserverBurnEvent observerEvent)
	{
		return observerEvent switch
		{
			EquipmentObserverBurnEvent.Attack => ItemChargeObserverEvent.Attack,
			EquipmentObserverBurnEvent.Attacked => ItemChargeObserverEvent.Attacked,
			EquipmentObserverBurnEvent.DotAttacked => ItemChargeObserverEvent.DotAttacked,
			_ => throw new ArgumentOutOfRangeException(nameof(observerEvent), observerEvent, null),
		};
	}
}

public enum EquipmentObserverBurnEvent
{
	Attack,
	Attacked,
	DotAttacked,
}

public sealed record EquipmentObserverBurnWorkflowResult(
	bool Changed,
	IdianPolishBurnPlan IdianPlan,
	ItemChargeBurnPlan ChargePlan,
	IdianPolishBurnApplicationResult IdianApplication,
	ItemChargeBurnApplicationResult ChargeApplication,
	IReadOnlyList<GameServerPacket> Packets,
	bool IdianPersisted,
	bool ChargePersisted)
{
	public bool Persisted => IdianPersisted && ChargePersisted;
}
