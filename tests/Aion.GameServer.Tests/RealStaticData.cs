using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

/// <summary>
/// The real game-server static data, loaded once per test process through the production
/// <see cref="DataManager.LoadAsync(string, string?, bool, Microsoft.Extensions.Logging.ILogger?, CancellationToken)"/>
/// path, for the integration tests that need to read it.
/// </summary>
/// <remarks>
/// <para>
/// Gate on the SOURCE tree, never on the generated cache. <c>game-server/data/static_data</c> is checked in
/// (773 files), so every checkout has the XMLs and a test may depend on them unconditionally.
/// <c>game-server/cache/static_data.xml</c> is not: it is gitignored, and <see cref="LoadingUtils.XmlMerger"/>
/// produces it from those same source XMLs during the load. Tests that guarded on the cache existing and
/// returned early when it did not were therefore circular — the call they skipped was the one that would have
/// created the file — so on a clean checkout they always returned immediately, asserted nothing, and still
/// reported green. Anything here that cannot find the source data throws instead of skipping.
/// </para>
/// <para>
/// The merged cache lands in the production <c>game-server/cache</c> directory (gitignored): the first run in
/// a fresh clone pays the ~150 MB merge, later runs reuse it as a real server would, and no test has to know
/// whether it was the one that built it.
/// </para>
/// <para>
/// One <see cref="DataManager"/> is shared by every caller. The holders are read-only once loaded (the spawn
/// path in particular keeps its live-object state on the world, not on <c>SpawnTemplate</c>), and parsing the
/// merged cache costs both real time and a lot of memory, so re-loading it per test would buy nothing.
/// Callers that need the singleton bridge still register it themselves, under their own
/// <c>DataManagerSingletonGuard</c>.
/// </para>
/// </remarks>
internal static class RealStaticData
{
	private static readonly Lazy<Task<DataManager>> Loader = new(LoadOnceAsync);

	/// <summary>The real <see cref="DataManager"/>, loaded on first use and shared from then on.</summary>
	internal static Task<DataManager> LoadAsync() => Loader.Value;

	/// <summary>The repository root, located by the checked-in static-data entry point it must contain.</summary>
	internal static string RepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new InvalidOperationException(
			"Could not locate game-server/data/static_data/static_data.xml above " + AppContext.BaseDirectory +
			". That tree is checked in, so this means a broken checkout rather than a data-less one.");
	}

	private static async Task<DataManager> LoadOnceAsync()
	{
		// Generous: the first run in a fresh clone merges the whole source tree before parsing it. A hang here
		// should still surface as a failure rather than a test run that never ends.
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));

		// validateWhenCacheChanges:false — the XSD pass is a separate concern (and a fire-and-forget background
		// task); these tests prove the parse.
		return await DataManager.LoadAsync(
			RepoRoot(),
			cacheDirectory: null,
			validateWhenCacheChanges: false,
			logger: null,
			cancellationToken: cts.Token);
	}
}
