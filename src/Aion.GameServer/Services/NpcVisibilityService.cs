using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class NpcVisibilityService
{
	private readonly ConcurrentDictionary<int, HashSet<int>> _knownNpcObjectIdsByPlayer = new();

	public NpcVisibilityDelta UpdateKnownNpcs(Player player, IEnumerable<IWorldNpcObject> npcs)
	{
		// Java parity: world/knownlist/KnownList.updateKnownList for ordinary Npc visible objects.
		var visibleNpcs = npcs
			.Where(npc => IsVisibleTo(player, npc))
			.GroupBy(npc => npc.ObjectId)
			.Select(group => group.First())
			.ToArray();
		var visibleObjectIds = visibleNpcs.Select(npc => npc.ObjectId).ToHashSet();
		var knownObjectIds = _knownNpcObjectIdsByPlayer.GetOrAdd(player.ObjectId, _ => []);

		lock (knownObjectIds)
		{
			var appeared = visibleNpcs
				.Where(npc => !knownObjectIds.Contains(npc.ObjectId))
				.ToArray();
			var disappeared = knownObjectIds
				.Where(objectId => !visibleObjectIds.Contains(objectId))
				.ToArray();

			foreach (var npc in appeared)
				knownObjectIds.Add(npc.ObjectId);
			foreach (var objectId in disappeared)
				knownObjectIds.Remove(objectId);

			return new NpcVisibilityDelta(appeared, disappeared);
		}
	}

	public void ClearKnownNpcs(int playerObjectId)
	{
		// Java parity: KnownList.clear on player removal/despawn.
		_knownNpcObjectIdsByPlayer.TryRemove(playerObjectId, out _);
	}

	public bool IsKnownNpc(Player player, int npcObjectId)
	{
		return IsKnownNpc(player.ObjectId, npcObjectId);
	}

	public bool IsKnownNpc(int playerObjectId, int npcObjectId)
	{
		// Java parity: world/knownlist/KnownList.getObject scopes CM_SHOW_DIALOG dispatch to known visible objects.
		if (!_knownNpcObjectIdsByPlayer.TryGetValue(playerObjectId, out var knownObjectIds))
			return false;

		lock (knownObjectIds)
			return knownObjectIds.Contains(npcObjectId);
	}

	public bool IsVisibleTo(Player player, IWorldNpcObject npc)
	{
		return WorldVisibility.IsVisibleTo(player, npc.Position);
	}
}

public sealed record NpcVisibilityDelta(IReadOnlyList<IWorldNpcObject> Appeared, IReadOnlyList<int> DisappearedObjectIds);
