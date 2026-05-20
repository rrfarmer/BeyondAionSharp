using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public interface IStaticDataLoader
{
	Task<DataManager> LoadAsync(CancellationToken cancellationToken = default);
}
