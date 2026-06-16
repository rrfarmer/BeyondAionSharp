using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class GameServerRuntimeContext
{
	public DataManager? DataManager { get; private set; }

	public LegionBonusRuntime LegionBonuses { get; } = new();

	public WorldMapRuntimeStateTable WorldMapStates { get; private set; } = WorldMapRuntimeStateTable.Empty;

	// Java parity: LimitedItemTradeService is a singleton populated from DataManager in its Start().
	public LimitedItemTradeService LimitedItems { get; private set; } = LimitedItemTradeService.GetInstance();

	public void SetDataManager(DataManager dataManager)
	{
		DataManager = dataManager;
		WorldMapStates = new WorldMapRuntimeStateTable(dataManager.StaticData.WorldMaps);
		LimitedItems = LimitedItemTradeService.GetInstance();
	}

	public void SetWorldMapStates(WorldMapRuntimeStateTable worldMapStates)
	{
		WorldMapStates = worldMapStates;
	}
}
