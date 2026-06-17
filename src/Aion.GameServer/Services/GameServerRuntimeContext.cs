using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed class GameServerRuntimeContext
{
	public DataManager? DataManager { get; private set; }

	public LegionBonusRuntime LegionBonuses { get; } = new();

	// Java parity: LimitedItemTradeService is a singleton populated from DataManager in its Start().
	public LimitedItemTradeService LimitedItems { get; private set; } = LimitedItemTradeService.GetInstance();

	public void SetDataManager(DataManager dataManager)
	{
		DataManager = dataManager;
		LimitedItems = LimitedItemTradeService.GetInstance();
	}
}
