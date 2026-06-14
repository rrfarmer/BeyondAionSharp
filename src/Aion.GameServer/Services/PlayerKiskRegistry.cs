using System.Collections.Concurrent;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class PlayerKiskRegistry
{
	private readonly ConcurrentDictionary<int, PlayerKiskRuntimeState> _ownerKisks = new();
	private readonly ConcurrentDictionary<int, int> _ownersByKiskId = new();
	private readonly ConcurrentDictionary<int, int> _offlineBoundKiskIdsByPlayerId = new();

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

		if (!_ownerKisks.TryRemove(ownerObjectId, out removedKisk))
			return false;

		foreach (var memberObjectId in removedKisk.CurrentMemberIds)
			_offlineBoundKiskIdsByPlayerId.TryRemove(memberObjectId, out _);
		return true;
	}

	public bool RegisterOfflineBinding(int playerObjectId, int kiskObjectId)
	{
		// Java parity: services/KiskService.onLogout stores Player.getKisk for offline restore.
		if (playerObjectId <= 0 || GetKiskState(kiskObjectId) == null)
			return false;

		_offlineBoundKiskIdsByPlayerId[playerObjectId] = kiskObjectId;
		return true;
	}

	public PlayerKiskOfflineBindingRestoreResult RestoreOfflineBinding(Player player)
	{
		// Java parity: services/KiskService.onLogin reattaches a player to the stored offline Kisk.
		if (!_offlineBoundKiskIdsByPlayerId.TryRemove(player.ObjectId, out var kiskObjectId))
			return PlayerKiskOfflineBindingRestoreResult.NotFound();

		var kisk = GetKiskState(kiskObjectId);
		if (kisk == null)
			return PlayerKiskOfflineBindingRestoreResult.Expired(kiskObjectId);

		var addedMember = kisk.AddMember(player.ObjectId);
		// Java parity: the authoritative binding is Player.setKisk(Kisk) (faithful spine);
		// the reworked registry restores membership via _offlineBoundKiskIdsByPlayerId + AddMember.
		return addedMember
			? PlayerKiskOfflineBindingRestoreResult.RestoredAdded(kisk)
			: PlayerKiskOfflineBindingRestoreResult.RestoredExisting(kisk);
	}
}

public sealed record PlayerKiskOwnership(
	int KiskObjectId,
	int OwnerObjectId,
	int NpcId);

public sealed record PlayerKiskOfflineBindingRestoreResult(
	PlayerKiskOfflineBindingRestoreStatus Status,
	int KiskObjectId,
	PlayerKiskRuntimeState? Kisk,
	bool AddedMember)
{
	public bool Restored => Status is PlayerKiskOfflineBindingRestoreStatus.RestoredAddedMember
		or PlayerKiskOfflineBindingRestoreStatus.RestoredExistingMember;

	public static PlayerKiskOfflineBindingRestoreResult NotFound()
	{
		return new PlayerKiskOfflineBindingRestoreResult(PlayerKiskOfflineBindingRestoreStatus.NotFound, 0, null, false);
	}

	public static PlayerKiskOfflineBindingRestoreResult Expired(int kiskObjectId)
	{
		return new PlayerKiskOfflineBindingRestoreResult(PlayerKiskOfflineBindingRestoreStatus.Expired, kiskObjectId, null, false);
	}

	public static PlayerKiskOfflineBindingRestoreResult RestoredAdded(PlayerKiskRuntimeState kisk)
	{
		return new PlayerKiskOfflineBindingRestoreResult(
			PlayerKiskOfflineBindingRestoreStatus.RestoredAddedMember,
			kisk.ObjectId,
			kisk,
			true);
	}

	public static PlayerKiskOfflineBindingRestoreResult RestoredExisting(PlayerKiskRuntimeState kisk)
	{
		return new PlayerKiskOfflineBindingRestoreResult(
			PlayerKiskOfflineBindingRestoreStatus.RestoredExistingMember,
			kisk.ObjectId,
			kisk,
			false);
	}
}

public enum PlayerKiskOfflineBindingRestoreStatus
{
	NotFound,
	Expired,
	RestoredAddedMember,
	RestoredExistingMember,
}

public sealed class PlayerKiskRuntimeState
{
	public const int LifetimeSeconds = 7200;
	public const int DefaultUseMask = 4;
	public const int DefaultMaxMembers = 6;
	public const int DefaultMaxResurrects = 18;

	private readonly ConcurrentDictionary<int, byte> _memberIds = new();
	private readonly Lock _despawnTaskSync = new();
	private ScheduledTask? _despawnTask;

	public PlayerKiskRuntimeState(
		int objectId,
		int ownerObjectId,
		int npcId,
		int useMask = DefaultUseMask,
		int maxMembers = DefaultMaxMembers,
		int maxResurrects = DefaultMaxResurrects,
		DateTimeOffset? spawnedAt = null,
		string ownerRace = "",
		int ownerLegionId = 0)
	{
		ObjectId = objectId;
		OwnerObjectId = ownerObjectId;
		NpcId = npcId;
		UseMask = useMask;
		MaxMembers = maxMembers;
		MaxResurrects = maxResurrects;
		RemainingResurrects = maxResurrects;
		SpawnedAt = spawnedAt ?? DateTimeOffset.UtcNow;
		OwnerRace = ownerRace;
		OwnerLegionId = ownerLegionId;
	}

	public int ObjectId { get; }

	public int OwnerObjectId { get; }

	public int NpcId { get; }

	public int UseMask { get; }

	public int MaxMembers { get; }

	public int RemainingResurrects { get; private set; }

	public int MaxResurrects { get; }

	public DateTimeOffset SpawnedAt { get; }

	public string OwnerRace { get; }

	public int OwnerLegionId { get; }

	public PlayerKiskOwnership Ownership => new(ObjectId, OwnerObjectId, NpcId);

	public int CurrentMemberCount => _memberIds.Count;

	public IReadOnlyList<int> CurrentMemberIds => _memberIds.Keys.ToArray();

	public bool HasScheduledDespawnTask
	{
		get
		{
			lock (_despawnTaskSync)
				return _despawnTask != null;
		}
	}

	public static PlayerKiskRuntimeState FromTemplate(
		int objectId,
		int ownerObjectId,
		NpcTemplateSummary template,
		string ownerRace = "",
		int ownerLegionId = 0,
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
			spawnedAt,
			ownerRace,
			ownerLegionId);
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

	public bool SetDespawnTask(ScheduledTask task)
	{
		// Java parity: controllers/CreatureController.addTask(TaskId.DESPAWN) replaces and cancels a previous DESPAWN task.
		lock (_despawnTaskSync)
		{
			var replaced = _despawnTask?.Cancel() == true;
			_despawnTask = task;
			return replaced;
		}
	}

	public bool CancelDespawnTask()
	{
		// Java parity: controllers/CreatureController.onDelete -> cancelAllTasks cancels the Kisk TaskId.DESPAWN Future.
		lock (_despawnTaskSync)
		{
			var task = _despawnTask;
			_despawnTask = null;
			return task?.Cancel() == true;
		}
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
