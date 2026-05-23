using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PortalEntryValidationService
{
	public static PortalEntryPlanResult ValidatePortalEntryPlan(
		Player player,
		PortalPathSummary portalPath,
		PortalLocTable portalLocs,
		InstanceCooltimeTable instanceCooltimes,
		WorldMapRuntimeStateTable worldMaps,
		DateTimeOffset now,
		int npcObjectId = 0,
		bool adminBypassRequirements = false,
		bool bypassLevelRequirement = false,
		bool bypassRaceRequirement = false,
		bool bypassTitleRequirement = false,
		bool bypassQuestRequirement = false,
		bool siegeOwnerMatchesPlayerRace = true,
		bool npcIsDialogNpc = true)
	{
		// Java parity: services/teleport/PortalService.port early location lookup and solo/open-world guard ordering.
		var loc = portalLocs.GetPortalLoc(portalPath.LocId);
		if (loc == null)
			return PortalEntryPlanResult.MissingLocation();

		var maxPlayers = instanceCooltimes.GetMaxMemberCount(loc.WorldId, player.Race);
		if (maxPlayers != 0 && maxPlayers != 1)
			return PortalEntryPlanResult.UnsupportedTeamPortal(loc);

		if (!adminBypassRequirements)
		{
			var validation = ValidateMentor(player, loc.WorldId, instanceCooltimes);
			if (!validation.CanEnter)
				return PortalEntryPlanResult.Rejected(validation.Status, loc, validation.FailurePacket!);

			validation = ValidateRace(
				player,
				portalPath,
				siegeOwnerMatchesPlayerRace,
				npcIsDialogNpc,
				npcObjectId,
				bypassRaceRequirement);
			if (!validation.CanEnter)
				return PortalEntryPlanResult.Rejected(validation.Status, loc, validation.FailurePacket!);

			validation = ValidateRank(player, portalPath, npcObjectId);
			if (!validation.CanEnter)
				return PortalEntryPlanResult.Rejected(validation.Status, loc, validation.FailurePacket!);

			validation = ValidateTitle(player, portalPath, npcObjectId, bypassTitleRequirement);
			if (!validation.CanEnter)
				return PortalEntryPlanResult.Rejected(validation.Status, loc, validation.FailurePacket!);

			validation = ValidateQuestRequirements(player, portalPath, npcIsDialogNpc, npcObjectId, bypassQuestRequirement);
			if (!validation.CanEnter)
				return PortalEntryPlanResult.Rejected(validation.Status, loc, validation.FailurePacket!);
		}

		var instanceValidation = ValidateCooldownForRegisteredInstance(
			player,
			loc.WorldId,
			maxPlayers,
			worldMaps,
			instanceCooltimes,
			now);
		if (!instanceValidation.CanEnter)
			return PortalEntryPlanResult.Rejected(instanceValidation.Status, loc, instanceValidation.FailurePacket!);

		if (!instanceValidation.Reenter)
		{
			var validation = ValidateEnterLevel(
				player,
				loc.WorldId,
				instanceCooltimes,
				portalPath,
				npcObjectId,
				bypassLevelRequirement);
			if (!validation.CanEnter)
				return PortalEntryPlanResult.Rejected(validation.Status, loc, validation.FailurePacket!);

			if (loc.WorldId == player.Position.WorldId)
				return PortalEntryPlanResult.SameInstanceTeleport(loc, instanceValidation.RegisteredInstance);
		}

		return PortalEntryPlanResult.Allowed(loc, instanceValidation.RegisteredInstance, instanceValidation.Reenter);
	}

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
		PortalPathSummary portalPath,
		int npcObjectId = 0,
		bool bypassLevelRequirement = false)
	{
		// Java parity: PortalService.checkEnterLevel consumes PortalPath.getMinLevel and getErrLevel.
		return ValidateEnterLevel(
			player,
			worldId,
			instanceCooltimes,
			portalPath.MinLevel,
			portalPath.ErrLevel,
			npcObjectId,
			bypassLevelRequirement);
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
		PortalPathSummary portalPath,
		bool siegeOwnerMatchesPlayerRace = true,
		bool npcIsDialogNpc = true,
		int npcObjectId = 0,
		bool bypassRaceRequirement = false)
	{
		// Java parity: PortalService.checkRace consumes PortalPath.getRace and getSiegeId.
		return ValidateRace(
			player,
			portalPath.Race,
			siegeOwnerMatchesPlayerRace,
			npcIsDialogNpc,
			npcObjectId,
			bypassRaceRequirement);
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
		PortalPathSummary portalPath,
		int npcObjectId)
	{
		// Java parity: PortalService.checkRank consumes PortalPath.getMinRank.
		return ValidateRank(player, portalPath.MinRank, npcObjectId);
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
		PortalPathSummary portalPath,
		int npcObjectId,
		bool bypassTitleRequirement = false)
	{
		// Java parity: PortalService.checkTitle consumes PortalPath.getTitleId.
		return ValidateTitle(player, portalPath.TitleId, npcObjectId, bypassTitleRequirement);
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

	public static PortalEntryValidationResult ValidateQuestRequirements(
		Player player,
		PortalPathSummary portalPath,
		bool npcIsDialogNpc = true,
		int npcObjectId = 0,
		bool bypassQuestRequirement = false)
	{
		// Java parity: services/teleport/PortalService.checkQuests.
		if (bypassQuestRequirement || portalPath.QuestRequirements.Count == 0)
			return PortalEntryValidationResult.Allowed();

		foreach (var requirement in portalPath.QuestRequirements)
		{
			var quest = player.Quests.FirstOrDefault(state => state.QuestId == requirement.QuestId);
			if (quest == null)
				continue;

			if (quest.IsComplete
				|| (requirement.QuestStep > 0 && quest.GetQuestVarById(0) >= requirement.QuestStep))
			{
				return PortalEntryValidationResult.Allowed();
			}
		}

		GameServerPacket failurePacket = npcIsDialogNpc
			? new SmDialogWindow(npcObjectId, SmDialogWindow.NoRightPageId)
			: SmSystemMessage.SkillCanNotUseGroupgateNoRight();
		return PortalEntryValidationResult.Rejected(PortalEntryValidationStatus.QuestRestricted, failurePacket);
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
	MissingPortalLocation,
	UnsupportedTeamPortal,
	CooldownLocked,
	LevelRestricted,
	MentorRestricted,
	RaceRestricted,
	RankRestricted,
	TitleRestricted,
	QuestRestricted,
}

public sealed record PortalEntryPlanResult(
	bool CanEnter,
	PortalEntryValidationStatus Status,
	PortalEntryPlanAction Action,
	PortalLocSummary? PortalLoc,
	WorldMapInstanceRuntimeState? RegisteredInstance,
	bool Reenter,
	GameServerPacket? FailurePacket)
{
	public static PortalEntryPlanResult Allowed(
		PortalLocSummary portalLoc,
		WorldMapInstanceRuntimeState? registeredInstance,
		bool reenter)
	{
		return new PortalEntryPlanResult(
			true,
			PortalEntryValidationStatus.Allowed,
			PortalEntryPlanAction.Continue,
			portalLoc,
			registeredInstance,
			reenter,
			null);
	}

	public static PortalEntryPlanResult SameInstanceTeleport(
		PortalLocSummary portalLoc,
		WorldMapInstanceRuntimeState? registeredInstance)
	{
		return new PortalEntryPlanResult(
			true,
			PortalEntryValidationStatus.Allowed,
			PortalEntryPlanAction.SameInstanceTeleport,
			portalLoc,
			registeredInstance,
			false,
			null);
	}

	public static PortalEntryPlanResult MissingLocation()
	{
		return new PortalEntryPlanResult(
			false,
			PortalEntryValidationStatus.MissingPortalLocation,
			PortalEntryPlanAction.None,
			null,
			null,
			false,
			null);
	}

	public static PortalEntryPlanResult UnsupportedTeamPortal(PortalLocSummary portalLoc)
	{
		return new PortalEntryPlanResult(
			false,
			PortalEntryValidationStatus.UnsupportedTeamPortal,
			PortalEntryPlanAction.None,
			portalLoc,
			null,
			false,
			null);
	}

	public static PortalEntryPlanResult Rejected(
		PortalEntryValidationStatus status,
		PortalLocSummary portalLoc,
		GameServerPacket failurePacket)
	{
		return new PortalEntryPlanResult(
			false,
			status,
			PortalEntryPlanAction.None,
			portalLoc,
			null,
			false,
			failurePacket);
	}
}

public enum PortalEntryPlanAction
{
	None,
	Continue,
	SameInstanceTeleport,
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
