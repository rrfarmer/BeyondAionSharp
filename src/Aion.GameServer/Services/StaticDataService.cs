using Aion.GameServer.Dataholders;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class StaticDataService : IStaticDataLoader
{
	private readonly ILogger<StaticDataService> _logger;

	public StaticDataService(ILogger<StaticDataService> logger)
	{
		_logger = logger;
	}

	public DataManager? DataManager { get; private set; }

	public async Task<DataManager> LoadAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: GameServer bootstrap calls DataManager initialization from repo static data.
		var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
			?? throw new DirectoryNotFoundException("Could not find game-server/data/static_data from the application directory.");
		DataManager = await Aion.GameServer.Dataholders.DataManager.LoadAsync(repoRoot, logger: _logger, cancellationToken: cancellationToken);
		return DataManager;
	}

	private static string? FindRepoRoot(string startDirectory)
	{
		// Java parity: resolve game-server/data/static_data beside the running game-server.
		var directory = new DirectoryInfo(startDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		return null;
	}
}
