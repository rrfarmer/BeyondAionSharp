using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

/// <summary>
/// Integration check for the "reuse the Java XML in place" principle: load the REAL
/// game-server static data (the 147 MB Java-generated <c>game-server/cache/static_data.xml</c>
/// plus the <c>game-server/data/static_data</c> tree) through the production
/// <see cref="DataManager.LoadAsync(string, string?, bool, Microsoft.Extensions.Logging.ILogger?, System.Threading.CancellationToken)"/>
/// path and assert it actually parses into non-empty tables. This is the linchpin of Front-A
/// (server boot): if the C# cannot consume Java's data as-is, nothing runs. NOT a unit test —
/// it reads the real on-disk data, so it is skipped (not failed) when that data is absent.
/// </summary>
public sealed class RealStaticDataLoadIntegrationTests
{
	[Fact]
	public async Task LoadAsync_ParsesRealJavaStaticDataCache_IntoNonEmptyTables()
	{
		var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
		var cacheFile = repoRoot is null
			? null
			: Path.Combine(repoRoot, "game-server", "cache", "static_data.xml");
		if (cacheFile is null || !File.Exists(cacheFile))
			return; // Real game-server/cache/static_data.xml not present; skip the reuse-in-place integration check.

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		// validateWhenCacheChanges:false — just parse the cache; we are proving the parse, not re-validating against source.
		var dataManager = await DataManager.LoadAsync(
			repoRoot!,
			cacheDirectory: null,
			validateWhenCacheChanges: false,
			logger: null,
			cancellationToken: cts.Token);

		var sd = dataManager.StaticData;

		// The merged cache must have parsed real content (these are the highest-traffic gameplay tables).
		Assert.True(sd.ImportedFileCount > 0, "no source files imported");
		Assert.True(sd.ItemTemplates.Count > 0, $"ItemTemplates empty (GetElementCount('item')={sd.GetElementCount("item")})");
		Assert.True(sd.NpcTemplates.Count > 0, "NpcTemplates empty");
		Assert.True(sd.WorldMaps.Count > 0, "WorldMaps empty");
	}

	private static string? FindRepoRoot(string startDirectory)
	{
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
