using System;
using System.Collections.Generic;
using System.IO;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.World.Spawns;

/// <summary>
/// Puts the conditional spawn groups into the world at start, and keeps them tracking their gates.
/// </summary>
/// <remarks>
/// The call site the engine was built for. <see cref="GatedSpawnData"/> carries 14,292 placements across
/// 91 maps once the ones this port already spawns unconditionally are filtered out, and roughly
/// <b>619</b> of them hold before anything writes a variable — those appear at start, and the rest wait
/// for a pattern to move a counter.
/// <para>
/// <b>Non-instance maps only.</b> A map whose instances come and go would need a controller per
/// instance and a store per instance, and the scope measurement only ever reached map level. Instanced
/// content keeps behaving as it does today rather than getting a half-answer.
/// </para>
/// <para>
/// Controllers are held for the life of the process because they are the subscribers: a controller that
/// is collected stops listening, and the gate it watches silently stops working.
/// </para>
/// </remarks>
public static class GatedSpawnService
{
	private static readonly List<GatedSpawnController> Live = new();

	private static readonly Lock Gate = new();

	/// <summary>How many groups are in the world right now because their gate holds.</summary>
	public static int Placed
	{
		get
		{
			lock (Gate)
			{
				int total = 0;
				foreach (GatedSpawnController controller in Live)
					total += controller.Placed;

				return total;
			}
		}
	}

	/// <summary>
	/// Starts from the data directory found by walking up from the running assembly.
	/// </summary>
	/// <remarks>
	/// The bootstrap does not carry a server root -- it takes a loader that already has one -- and
	/// threading a path through it for one file would touch more than this is worth. The walk stops at
	/// the first directory holding <c>game-server/data/static_data</c>.
	/// </remarks>
	public static int Start()
	{
		DirectoryInfo? at = new DirectoryInfo(AppContext.BaseDirectory);
		while (at is not null)
		{
			if (Directory.Exists(Path.Combine(at.FullName, "game-server", "data", "static_data")))
				return Start(at.FullName);

			at = at.Parent;
		}

		return 0;
	}

	/// <summary>Reads the data and starts a controller for every non-instance map that has groups.</summary>
	/// <param name="repoRoot">The server root, as <see cref="DataManager"/> uses it.</param>
	/// <returns>How many groups were placed straight away.</returns>
	public static int Start(string repoRoot)
	{
		ArgumentException.ThrowIfNullOrEmpty(repoRoot);

		string path = Path.Combine(repoRoot, "game-server", "data", "static_data",
			GatedSpawnData.RelativePath.Replace('/', Path.DirectorySeparatorChar));

		IReadOnlyDictionary<int, IReadOnlyList<GatedSpawn>> byMap = GatedSpawnData.Load(path);
		lock (Gate)
		{
			foreach ((int mapId, IReadOnlyList<GatedSpawn> groups) in byMap)
			{
				WorldMap map = World.GetInstance().GetWorldMap(mapId);
				if (map is null || map.IsInstanceType())
					continue;

				map.ForEach(instance =>
				{
					var controller = new GatedSpawnController(mapId, instance.GetInstanceId(),
						SpawnVariableRegistry.For(mapId, instance.GetInstanceId()), groups);
					controller.Refresh();
					Live.Add(controller);
				});
			}

			int placed = 0;
			foreach (GatedSpawnController controller in Live)
				placed += controller.Placed;

			return placed;
		}
	}

	/// <summary>Stops every controller, for a restart or a test.</summary>
	public static void Stop()
	{
		lock (Gate)
		{
			foreach (GatedSpawnController controller in Live)
				controller.Dispose();

			Live.Clear();
		}
	}
}
