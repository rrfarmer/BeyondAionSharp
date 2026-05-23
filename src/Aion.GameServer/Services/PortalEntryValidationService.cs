using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PortalEntryValidationService
{
	public static PortalEntryValidationResult ValidateCooldown(
		Player player,
		int worldId,
		InstanceCooltimeTable instanceCooltimes,
		DateTimeOffset now)
	{
		// Java parity: services/teleport/PortalService.port rejects fresh instance creation when PortalCooldownList.isPortalUseDisabled(mapId).
		return PlayerPortalCooldownService.IsPortalUseDisabled(player, worldId, instanceCooltimes, now)
			? PortalEntryValidationResult.Rejected(
				PortalEntryValidationStatus.CooldownLocked,
				SmSystemMessage.CannotMakeInstanceCoolTime())
			: PortalEntryValidationResult.Allowed();
	}

	public static PortalEntryInstanceValidationResult ValidateCooldownForRegisteredInstance(
		Player player,
		int worldId,
		int maxPlayers,
		WorldMapRuntimeStateTable worldMaps,
		InstanceCooltimeTable instanceCooltimes,
		DateTimeOffset now)
	{
		// Java parity: services/teleport/PortalService.port resolves registered solo/group/alliance instances before applying the cooldown lockout.
		var registeredInstance = ResolveRegisteredInstance(player, worldId, maxPlayers, worldMaps);
		if (registeredInstance == null || !registeredInstance.IsRegistered(player.ObjectId))
		{
			var validation = ValidateCooldown(player, worldId, instanceCooltimes, now);
			return validation.CanEnter
				? PortalEntryInstanceValidationResult.Allowed(null, reenter: false)
				: PortalEntryInstanceValidationResult.Rejected(validation.Status, validation.FailurePacket!);
		}

		var reenter = player.Position.WorldId != worldId || player.Position.InstanceId != registeredInstance.InstanceId;
		return PortalEntryInstanceValidationResult.Allowed(registeredInstance, reenter);
	}

	public static PortalEntryValidationResult ValidateEnterLevel(
		Player player,
		int worldId,
		InstanceCooltimeTable instanceCooltimes,
		int portalPathMinLevel = 0,
		int portalPathErrLevel = 0,
		int npcObjectId = 0,
		bool bypassLevelRequirement = false)
	{
		// Java parity: services/teleport/PortalService.checkEnterLevel.
		if (bypassLevelRequirement)
			return PortalEntryValidationResult.Allowed();

		var enterMinLevel = portalPathMinLevel;
		if (enterMinLevel == 0)
			enterMinLevel = instanceCooltimes.GetEnterMinLevel(worldId, player.Race);
		var enterMaxLevel = instanceCooltimes.GetEnterMaxLevel(worldId, player.Race);

		if (player.Level >= enterMinLevel && (enterMaxLevel <= 0 || player.Level <= enterMaxLevel))
			return PortalEntryValidationResult.Allowed();

		GameServerPacket failurePacket = portalPathErrLevel != 0
			? new SmDialogWindow(npcObjectId, portalPathErrLevel)
			: SmSystemMessage.CantInstanceEnterLevel();
		return PortalEntryValidationResult.Rejected(PortalEntryValidationStatus.LevelRestricted, failurePacket);
	}

	public static PortalEntryValidationResult ValidateMentor(
		Player player,
		int worldId,
		InstanceCooltimeTable instanceCooltimes)
	{
		// Java parity: services/teleport/PortalService.checkMentor.
		var template = instanceCooltimes.GetInstanceCooltimeByWorldId(worldId);
		if (template != null && player.IsMentor && !template.CanEnterMentor)
			return PortalEntryValidationResult.Rejected(
				PortalEntryValidationStatus.MentorRestricted,
				SmSystemMessage.MentorCantEnter(worldId));

		return PortalEntryValidationResult.Allowed();
	}

	public static PortalEntryValidationResult ValidateRace(
		Player player,
		string portalRace,
		bool siegeOwnerMatchesPlayerRace = true,
		bool npcIsDialogNpc = true,
		int npcObjectId = 0,
		bool bypassRaceRequirement = false)
	{
		// Java parity: services/teleport/PortalService.checkRace, with SiegeService.checkSiegeId result supplied by caller.
		if (bypassRaceRequirement)
			return PortalEntryValidationResult.Allowed();

		var raceRestricted = !string.Equals(portalRace, "PC_ALL", StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(player.Race, portalRace, StringComparison.OrdinalIgnoreCase);
		if (!raceRestricted && siegeOwnerMatchesPlayerRace)
			return PortalEntryValidationResult.Allowed();

		GameServerPacket failurePacket = npcIsDialogNpc
			? new SmDialogWindow(npcObjectId, SmDialogWindow.NoRightPageId)
			: SmSystemMessage.MovePortalErrorInvalidRace();
		return PortalEntryValidationResult.Rejected(PortalEntryValidationStatus.RaceRestricted, failurePacket);
	}

	public static PortalEntryValidationResult ValidateRank(
		Player player,
		int portalPathMinRank,
		int npcObjectId)
	{
		// Java parity: services/teleport/PortalService.checkRank.
		if (player.AbyssRank.Rank >= portalPathMinRank)
			return PortalEntryValidationResult.Allowed();

		return PortalEntryValidationResult.Rejected(
			PortalEntryValidationStatus.RankRestricted,
			new SmDialogWindow(npcObjectId, SmDialogWindow.NoRightPageId));
	}

	public static PortalEntryValidationResult ValidateTitle(
		Player player,
		int portalPathTitleId,
		int npcObjectId,
		bool bypassTitleRequirement = false)
	{
		// Java parity: services/teleport/PortalService.checkTitle compares PlayerCommonData.titleId.
		if (bypassTitleRequirement || portalPathTitleId == 0 || player.TitleId == portalPathTitleId)
			return PortalEntryValidationResult.Allowed();

		return PortalEntryValidationResult.Rejected(
			PortalEntryValidationStatus.TitleRestricted,
			new SmDialogWindow(npcObjectId, SmDialogWindow.NoRightPageId));
	}

	private static WorldMapInstanceRuntimeState? ResolveRegisteredInstance(
		Player player,
		int worldId,
		int maxPlayers,
		WorldMapRuntimeStateTable worldMaps)
	{
		return maxPlayers switch
		{
			1 => worldMaps.GetRegisteredInstance(worldId, player.ObjectId),
			_ => null,
		};
	}
}

public sealed record PortalEntryValidationResult(
	bool CanEnter,
	PortalEntryValidationStatus Status,
	GameServerPacket? FailurePacket)
{
	public static PortalEntryValidationResult Allowed()
	{
		return new PortalEntryValidationResult(true, PortalEntryValidationStatus.Allowed, null);
	}

	public static PortalEntryValidationResult Rejected(
		PortalEntryValidationStatus status,
		GameServerPacket failurePacket)
	{
		return new PortalEntryValidationResult(false, status, failurePacket);
	}
}

public enum PortalEntryValidationStatus
{
	Allowed,
	CooldownLocked,
	LevelRestricted,
	MentorRestricted,
	RaceRestricted,
	RankRestricted,
	TitleRestricted,
}

public sealed record PortalEntryInstanceValidationResult(
	bool CanEnter,
	PortalEntryValidationStatus Status,
	WorldMapInstanceRuntimeState? RegisteredInstance,
	bool Reenter,
	GameServerPacket? FailurePacket)
{
	public static PortalEntryInstanceValidationResult Allowed(
		WorldMapInstanceRuntimeState? registeredInstance,
		bool reenter)
	{
		return new PortalEntryInstanceValidationResult(
			true,
			PortalEntryValidationStatus.Allowed,
			registeredInstance,
			reenter,
			null);
	}

	public static PortalEntryInstanceValidationResult Rejected(
		PortalEntryValidationStatus status,
		GameServerPacket failurePacket)
	{
		return new PortalEntryInstanceValidationResult(false, status, null, false, failurePacket);
	}
}
