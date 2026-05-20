using Aion.GameServer.Dataholders.LoadingUtils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Dataholders;

public sealed class DataManager
{
	private DataManager(StaticData staticData)
	{
		StaticData = staticData;
	}

	public StaticData StaticData { get; }

	public static Task<DataManager> LoadAsync(
		string repoRoot,
		string? cacheDirectory = null,
		bool validateWhenCacheChanges = true,
		ILogger? logger = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dataholders/DataManager static_data.xml bootstrap entry point.
		var staticDataDirectory = Path.Combine(repoRoot, "game-server", "data", "static_data");
		var options = new XmlDataLoaderOptions
		{
			MainXmlFilePath = Path.Combine(staticDataDirectory, "static_data.xml"),
			SchemaFilePath = Path.Combine(staticDataDirectory, "static_data.xsd"),
			CacheXmlFilePath = Path.Combine(cacheDirectory ?? Path.Combine(repoRoot, "game-server", "cache"), "static_data.xml"),
			ValidateWhenCacheChanges = validateWhenCacheChanges,
		};

		return LoadAsync(options, logger, cancellationToken);
	}

	public static async Task<DataManager> LoadAsync(XmlDataLoaderOptions options, ILogger? logger = null, CancellationToken cancellationToken = default)
	{
		// Java parity: DataManager delegates static XML load/cache construction.
		var staticData = await XmlDataLoader.LoadStaticDataAsync(options, logger, cancellationToken);
		return new DataManager(staticData);
	}
}
