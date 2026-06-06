using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class GameServerRuntimeContext
{
	public DataManager? DataManager { get; private set; }

	public PlayerKiskRegistry Kisks { get; } = new();

	public LegionWarehouseRuntime LegionWarehouses { get; } = new();

	public LegionBonusRuntime LegionBonuses { get; } = new();

	public WorldMapRuntimeStateTable WorldMapStates { get; private set; } = WorldMapRuntimeStateTable.Empty;

	public LimitedItemTradeService LimitedItems { get; private set; } = LimitedItemTradeService.Empty;

	public void SetDataManager(DataManager dataManager)
	{
		DataManager = dataManager;
		WorldMapStates = new WorldMapRuntimeStateTable(dataManager.StaticData.WorldMaps);
		LimitedItems = LimitedItemTradeService.Create(dataManager.StaticData.TradeLists, dataManager.StaticData.GoodsLists);
	}

	public void SetWorldMapStates(WorldMapRuntimeStateTable worldMapStates)
	{
		WorldMapStates = worldMapStates;
	}
}
