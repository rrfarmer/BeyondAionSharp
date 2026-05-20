using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.World;

public sealed class World
{
	private readonly ILogger<World> _logger;
	private readonly ConcurrentDictionary<int, object> _objects = new();
	private int _initialized;

	public World(ILogger<World> logger)
	{
		_logger = logger;
	}

	public bool IsInitialized => Volatile.Read(ref _initialized) != 0;

	public int ObjectCount => _objects.Count;

	public void Initialize()
	{
		// Java parity: world/World singleton initialization shell.
		if (Interlocked.Exchange(ref _initialized, 1) == 0)
			_logger.LogInformation("Initialized world container");
	}

	public bool TryAddObject(int objectId, object gameObject)
	{
		// Java parity: world/World.storeObject.
		return _objects.TryAdd(objectId, gameObject);
	}

	public bool TryRemoveObject(int objectId, out object? gameObject)
	{
		return _objects.TryRemove(objectId, out gameObject);
	}

	public bool TryGetObject(int objectId, out object? gameObject)
	{
		return _objects.TryGetValue(objectId, out gameObject);
	}

	public void Clear()
	{
		_objects.Clear();
	}
}
