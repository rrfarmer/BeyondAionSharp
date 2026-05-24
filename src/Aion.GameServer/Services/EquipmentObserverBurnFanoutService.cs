using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public sealed class EquipmentObserverBurnFanoutService
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;

	public EquipmentObserverBurnFanoutService(IGameClientConnectionRegistry? connectionRegistry = null)
	{
		_connectionRegistry = connectionRegistry;
	}

	public async ValueTask<EquipmentObserverBurnFanoutResult> ApplyObserverBurnsAndSendPacketsAsync(
		Player owner,
		ItemTemplateTable itemTemplates,
		EquipmentObserverBurnEvent observerEvent,
		int skillId,
		Func<Player, IdianPolishBurnPlan, CancellationToken, Task<bool>>? saveIdianPolishBurnAsync = null,
		Func<Player, ItemChargeBurnPlan, CancellationToken, Task<bool>>? saveItemChargeBurnAsync = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: IdianStone and ChargeInfo observer callbacks send SM_INVENTORY_UPDATE_ITEM to the equipment owner.
		var workflow = await EquipmentObserverBurnWorkflowService.ApplyObserverBurnsAsync(
			owner,
			itemTemplates,
			observerEvent,
			skillId,
			saveIdianPolishBurnAsync,
			saveItemChargeBurnAsync,
			cancellationToken);
		var sentCount = 0;

		if (_connectionRegistry != null)
		{
			foreach (var packet in workflow.Packets)
			{
				if (await _connectionRegistry.SendPacketToPlayerAsync(owner.ObjectId, packet))
					sentCount++;
			}
		}

		return new EquipmentObserverBurnFanoutResult(workflow, workflow.Packets, sentCount);
	}
}

public sealed record EquipmentObserverBurnFanoutResult(
	EquipmentObserverBurnWorkflowResult Workflow,
	IReadOnlyList<GameServerPacket> Packets,
	int SentCount);
