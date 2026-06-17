using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class WalkerVersionTable
{
	private readonly IReadOnlyDictionary<string, string> _parentRouteIdsByVersionId;

	public WalkerVersionTable(IReadOnlyDictionary<string, string> parentRouteIdsByVersionId)
	{
		_parentRouteIdsByVersionId = parentRouteIdsByVersionId;
	}

	public int Count => _parentRouteIdsByVersionId.Count;

	public bool IsRouteVersioned(string? routeId)
	{
		return routeId != null && _parentRouteIdsByVersionId.ContainsKey(routeId);
	}

	public string? GetRouteVersionId(string? routeId)
	{
		return routeId == null ? null : _parentRouteIdsByVersionId.GetValueOrDefault(routeId);
	}
}
