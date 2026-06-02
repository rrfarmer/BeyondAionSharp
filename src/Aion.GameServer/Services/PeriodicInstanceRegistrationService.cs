using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PeriodicInstanceRegistrationService
{
	private readonly object _sync = new();
	private readonly HashSet<int> _openedRegistrations = [];

	public bool OpenRegistration(int maskId)
	{
		lock (_sync)
			return _openedRegistrations.Add(maskId);
	}

	public bool CloseRegistration(int maskId)
	{
		lock (_sync)
			return _openedRegistrations.Remove(maskId);
	}

	public bool IsRegistrationOpen(int maskId)
	{
		lock (_sync)
			return _openedRegistrations.Contains(maskId);
	}

	public IReadOnlyList<SmAutoGroup> CreateOpenRegistrationPackets(
		Player player,
		AutoGroupTable? autoGroups,
		InstanceCooltimeTable? instanceCooltimes,
		DateTimeOffset now)
	{
		if (autoGroups == null || instanceCooltimes == null)
			return Array.Empty<SmAutoGroup>();

		int[] openedMaskIds;
		lock (_sync)
			openedMaskIds = _openedRegistrations.ToArray();

		var packets = new List<SmAutoGroup>();
		foreach (var maskId in openedMaskIds)
		{
			var autoGroup = autoGroups.GetTemplateByInstanceMaskId(maskId);
			if (autoGroup == null)
				continue;
			if (player.Level < autoGroup.MinLevel || player.Level > autoGroup.MaxLevel)
				continue;
			if (PlayerPortalCooldownService.IsPortalUseDisabled(player, autoGroup.InstanceMapId, instanceCooltimes, now))
				continue;

			packets.Add(new SmAutoGroup(autoGroup, SmAutoGroup.EntryIconWindowId, close: false));
		}

		return packets;
	}
}
