using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class GameServerRuntimeContext
{
	public DataManager? DataManager { get; private set; }

	public WorldMapRuntimeStateTable WorldMapStates { get; private set; } = WorldMapRuntimeStateTable.Empty;

	public void SetDataManager(DataManager dataManager)
	{
		DataManager = dataManager;
		WorldMapStates = new WorldMapRuntimeStateTable(dataManager.StaticData.WorldMaps);
	}
}
