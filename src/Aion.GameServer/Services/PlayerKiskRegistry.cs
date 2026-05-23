using System.Collections.Concurrent;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed class PlayerKiskRegistry
{
	private readonly ConcurrentDictionary<int, PlayerKiskRuntimeState> _ownerKisks = new();
	private readonly ConcurrentDictionary<int, int> _ownersByKiskId = new();

	public PlayerKiskOwnership RegisterKisk(int ownerObjectId, int kiskObjectId, int npcId)
	{
		// Java parity: services/KiskService.regKisk stores the spawned Kisk by creator object id.
		return RegisterKisk(new PlayerKiskRuntimeState(kiskObjectId, ownerObjectId, npcId)).Ownership;
	}

	public PlayerKiskRuntimeState RegisterKisk(PlayerKiskRuntimeState kisk)
	{
		// Java parity: services/KiskService.regKisk stores the spawned Kisk by creator object id.
		_ownerKisks.AddOrUpdate(
			kisk.OwnerObjectId,
			kisk,
			(_, previous) =>
			{
				// Java normally removes the old kisk before replacing ownership; clear this reverse link defensively.
				if (previous.ObjectId != kisk.ObjectId)
					_ownersByKiskId.TryRemove(previous.ObjectId, out _);
				return kisk;
			});
		_ownersByKiskId[kisk.ObjectId] = kisk.OwnerObjectId;
		return kisk;
	}

	public bool HaveKisk(int ownerObjectId)
	{
		// Java parity: services/KiskService.haveKisk.
		return _ownerKisks.ContainsKey(ownerObjectId);
	}

	public PlayerKiskOwnership? GetOwnerKisk(int ownerObjectId)
	{
		return GetOwnerKiskState(ownerObjectId)?.Ownership;
	}

	public PlayerKiskRuntimeState? GetOwnerKiskState(int ownerObjectId)
	{
		return _ownerKisks.GetValueOrDefault(ownerObjectId);
	}

	public PlayerKiskRuntimeState? GetKiskState(int kiskObjectId)
	{
		// Java parity: Player.getKisk stores a direct Kisk reference; C# resolves the lightweight runtime state by object id.
		return _ownersByKiskId.TryGetValue(kiskObjectId, out var ownerObjectId)
			? GetOwnerKiskState(ownerObjectId)
			: null;
	}

	public bool RemoveKisk(int kiskObjectId)
	{
		// Java parity: services/KiskService.removeKisk removes ownerPlayer entries pointing at the deleted kisk.
		return TryRemoveKisk(kiskObjectId, out _);
	}

	public bool TryRemoveKisk(int kiskObjectId, out PlayerKiskRuntimeState? removedKisk)
	{
		// Java parity: services/KiskService.removeKisk needs the removed Kisk instance for member cleanup side effects.
		removedKisk = null;
		if (!_ownersByKiskId.TryRemove(kiskObjectId, out var ownerObjectId))
			return false;

		return _ownerKisks.TryRemove(ownerObjectId, out removedKisk);
	}
}

public sealed record PlayerKiskOwnership(
	int KiskObjectId,
	int OwnerObjectId,
	int NpcId);

public sealed class PlayerKiskRuntimeState
{
	public const int LifetimeSeconds = 7200;
	public const int DefaultUseMask = 4;
	public const int DefaultMaxMembers = 6;
	public const int DefaultMaxResurrects = 18;

	private readonly ConcurrentDictionary<int, byte> _memberIds = new();

	public PlayerKiskRuntimeState(
		int objectId,
		int ownerObjectId,
		int npcId,
		int useMask = DefaultUseMask,
		int maxMembers = DefaultMaxMembers,
		int maxResurrects = DefaultMaxResurrects,
		DateTimeOffset? spawnedAt = null)
	{
		ObjectId = objectId;
		OwnerObjectId = ownerObjectId;
		NpcId = npcId;
		UseMask = useMask;
		MaxMembers = maxMembers;
		MaxResurrects = maxResurrects;
		RemainingResurrects = maxResurrects;
		SpawnedAt = spawnedAt ?? DateTimeOffset.UtcNow;
	}

	public int ObjectId { get; }

	public int OwnerObjectId { get; }

	public int NpcId { get; }

	public int UseMask { get; }

	public int MaxMembers { get; }

	public int RemainingResurrects { get; private set; }

	public int MaxResurrects { get; }

	public DateTimeOffset SpawnedAt { get; }

	public PlayerKiskOwnership Ownership => new(ObjectId, OwnerObjectId, NpcId);

	public int CurrentMemberCount => _memberIds.Count;

	public IReadOnlyList<int> CurrentMemberIds => _memberIds.Keys.ToArray();

	public static PlayerKiskRuntimeState FromTemplate(
		int objectId,
		int ownerObjectId,
		NpcTemplateSummary template,
		DateTimeOffset? spawnedAt = null)
	{
		// Java parity: model/gameobjects/Kisk constructor uses NpcTemplate.kiskStatsTemplate or KiskStatsTemplate defaults.
		var stats = template.KiskStats ?? new KiskStatsSummary();
		return new PlayerKiskRuntimeState(
			objectId,
			ownerObjectId,
			template.TemplateId,
			stats.UseMask,
			stats.MaxMembers,
			stats.MaxResurrects,
			spawnedAt);
	}

	public int GetRemainingLifetimeSeconds(DateTimeOffset now)
	{
		var elapsedSeconds = (int)Math.Max(0, (now - SpawnedAt).TotalSeconds);
		return Math.Max(LifetimeSeconds - elapsedSeconds, 0);
	}

	public bool AddMember(int playerObjectId)
	{
		return _memberIds.TryAdd(playerObjectId, 0);
	}

	public bool RemoveMember(int playerObjectId)
	{
		return _memberIds.TryRemove(playerObjectId, out _);
	}

	public bool UseResurrection()
	{
		if (RemainingResurrects <= 0)
			return false;

		RemainingResurrects--;
		return true;
	}
}
