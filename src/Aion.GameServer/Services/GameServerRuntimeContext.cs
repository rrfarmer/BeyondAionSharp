using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed class GameServerRuntimeContext
{
	public DataManager? DataManager { get; private set; }

	public void SetDataManager(DataManager dataManager)
	{
		DataManager = dataManager;
	}
}
