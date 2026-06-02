using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PortalEntryValidationService
{
	private const int KinahItemId = 182400001;

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
		bool bypassGroupRequirement = false,
		bool siegeOwnerMatchesPlayerRace = true,
		bool npcIsDialogNpc = true)
	{
		// Java parity: services/teleport/PortalService.port early location lookup and solo/open-world guard ordering.
		var loc = portalLocs.GetPortalLoc(portalPath.LocId);
		if (loc == null)
			return PortalEntryPlanResult.MissingLocation();

		var maxPlayers = instanceCooltimes.GetMaxMemberCount(loc.WorldId, player.Race);

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

			validation = ValidatePlayerSize(player, portalPath, maxPlayers, npcObjectId, bypassGroupRequirement);
			if (!validation.CanEnter)
				return PortalEntryPlanResult.Rejected(validation.Status, loc, validation.FailurePacket!);
		}

		if (maxPlayers != 0 && maxPlayers != 1)
			return PortalEntryPlanResult.UnsupportedTeamPortal(loc, CreateUnsupportedTeamPlan(player, loc.WorldId, maxPlayers, worldMaps, bypassGroupRequirement));

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

			validation = ValidateRequiredItemsAndKinah(player, portalPath, npcIsDialogNpc, npcObjectId);
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

	public static PortalEntryValidationResult ValidateRequiredItemsAndKinah(
		Player player,
		PortalPathSummary portalPath,
		bool npcIsDialogNpc = true,
		int npcObjectId = 0)
	{
		// Java parity: services/teleport/PortalService.checkAndRemoveRequiredItems, validation-only half.
		if (GetInventoryCount(player, KinahItemId) < portalPath.Kinah)
		{
			GameServerPacket failurePacket = npcIsDialogNpc
				? new SmDialogWindow(npcObjectId, SmDialogWindow.NoRightPageId)
				: SmSystemMessage.NotEnoughKinah(portalPath.Kinah);
			return PortalEntryValidationResult.Rejected(PortalEntryValidationStatus.KinahRestricted, failurePacket);
		}

		foreach (var requirement in portalPath.ItemRequirements)
		{
			if (GetInventoryCount(player, requirement.ItemId) >= requirement.ItemCount)
				continue;

			GameServerPacket failurePacket = npcIsDialogNpc
				? new SmDialogWindow(npcObjectId, SmDialogWindow.NoRightPageId)
				: SmSystemMessage.InstanceCantEnterWithoutItem();
			return PortalEntryValidationResult.Rejected(PortalEntryValidationStatus.ItemRestricted, failurePacket);
		}

		return PortalEntryValidationResult.Allowed();
	}

	public static PortalEntryValidationResult ValidatePlayerSize(
		Player player,
		PortalPathSummary portalPath,
		int maxPlayers,
		int npcObjectId = 0,
		bool bypassGroupRequirement = false)
	{
		// Java parity: services/teleport/PortalService.checkPlayerSize.
		if (bypassGroupRequirement)
			return PortalEntryValidationResult.Allowed();

		if (maxPlayers is 3 or 6)
		{
			if (player.TeamMembership == PlayerTeamMembership.Group)
				return PortalEntryValidationResult.Allowed();

			GameServerPacket failurePacket = portalPath.ErrGroup != 0
				? new SmDialogWindow(npcObjectId, portalPath.ErrGroup)
				: SmSystemMessage.EnterOnlyPartyDon();
			return PortalEntryValidationResult.Rejected(PortalEntryValidationStatus.GroupRequired, failurePacket);
		}

		if (maxPlayers > 6 && maxPlayers <= 24)
		{
			if (player.TeamMembership == PlayerTeamMembership.Alliance)
				return PortalEntryValidationResult.Allowed();

			return PortalEntryValidationResult.Rejected(
				PortalEntryValidationStatus.AllianceRequired,
				SmSystemMessage.EnterOnlyForceDon());
		}

		if (maxPlayers > 24)
		{
			return PortalEntryValidationResult.Rejected(
				PortalEntryValidationStatus.LeagueRequired,
				SmSystemMessage.EnterOnlyUnionDon());
		}

		return PortalEntryValidationResult.Allowed();
	}

	public static PortalRequirementConsumptionPlan CreateRequiredItemsAndKinahConsumptionPlan(
		Player player,
		PortalPathSummary portalPath)
	{
		// Java parity: services/teleport/PortalService.checkAndRemoveRequiredItems consumes item requirements first, then kinah.
		var workingItems = player.InventoryItems.ToList();
		var updatedItemsByObjectId = new Dictionary<int, InventoryItem>();
		var deletedObjectIds = new List<int>();
		var consumptionSteps = new List<PortalRequirementConsumptionStep>();

		var availableKinah = GetInventoryCount(workingItems, KinahItemId);
		if (availableKinah < portalPath.Kinah)
		{
			return PortalRequirementConsumptionPlan.Failed(
				KinahItemId,
				portalPath.Kinah - availableKinah,
				updatedItemsByObjectId.Values.ToArray(),
				deletedObjectIds,
				consumptionSteps);
		}

		foreach (var requirement in portalPath.ItemRequirements)
		{
			var availableCount = GetInventoryCount(workingItems, requirement.ItemId);
			if (availableCount < requirement.ItemCount)
			{
				return PortalRequirementConsumptionPlan.Failed(
					requirement.ItemId,
					requirement.ItemCount - availableCount,
					updatedItemsByObjectId.Values.ToArray(),
					deletedObjectIds,
					consumptionSteps);
			}
		}

		foreach (var requirement in portalPath.ItemRequirements)
		{
			PlanDecreaseByItemId(
				workingItems,
				requirement.ItemId,
				requirement.ItemCount,
				deleteWhenZero: true,
				updatedItemsByObjectId,
				deletedObjectIds,
				consumptionSteps);
		}

		if (portalPath.Kinah > 0)
		{
			PlanDecreaseByItemId(
				workingItems,
				KinahItemId,
				portalPath.Kinah,
				deleteWhenZero: false,
				updatedItemsByObjectId,
				deletedObjectIds,
				consumptionSteps);
		}

		return PortalRequirementConsumptionPlan.Success(
			updatedItemsByObjectId.Values.ToArray(),
			deletedObjectIds,
			consumptionSteps);
	}

	public static PortalRequirementConsumptionApplication CreateRequiredItemsAndKinahApplication(
		Player player,
		PortalRequirementConsumptionPlan consumptionPlan,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null)
	{
		// Java parity: services/item/ItemPacketService.sendItemPacket over PortalService.checkAndRemoveRequiredItems mutations.
		if (!consumptionPlan.Succeeded)
			return PortalRequirementConsumptionApplication.NotApplied(player.InventoryItems);

		var updatedItemsByObjectId = consumptionPlan.UpdatedItems.ToDictionary(item => item.ObjectId);
		var deletedObjectIds = consumptionPlan.DeletedObjectIds.ToHashSet();
		var neededTemplateIds = consumptionPlan.ConsumptionSteps
			.Where(step => !deletedObjectIds.Contains(step.ObjectId))
			.Select(step => step.ItemId)
			.Distinct()
			.Where(itemId => itemTemplates.GetItemTemplate(itemId) == null)
			.ToArray();
		if (neededTemplateIds.Length > 0)
			return PortalRequirementConsumptionApplication.MissingTemplates(player.InventoryItems, neededTemplateIds);

		var workingItems = player.InventoryItems.ToList();
		var packets = new List<GameServerPacket>();

		foreach (var step in consumptionPlan.ConsumptionSteps)
		{
			if (deletedObjectIds.Contains(step.ObjectId))
			{
				workingItems.RemoveAll(item => item.ObjectId == step.ObjectId);
				packets.Add(new SmDeleteItem(step.ObjectId, SmDeleteItem.UseDeleteType));
				packets.Add(SmCubeUpdate.CubeSize(CreatePacketPlayer(player, workingItems)));
				continue;
			}

			var updatedItem = updatedItemsByObjectId[step.ObjectId];
			ReplaceInventoryItem(workingItems, updatedItem);
			var template = itemTemplates.GetItemTemplate(step.ItemId)!;
			var updateType = step.IsKinah
				? SmInventoryUpdateItem.DecreaseKinahBuy
				: SmInventoryUpdateItem.DecreaseItemUse;
			packets.Add(new SmInventoryUpdateItem(
				updatedItem,
				template,
				updateType,
				GetGeneralInfoWarehouseRestrictionFlag(updatedItem.ItemId, itemRestrictionCleanups)));
		}

		return PortalRequirementConsumptionApplication.Success(
			workingItems,
			packets,
			consumptionPlan.UpdatedItems,
			consumptionPlan.DeletedObjectIds);
	}

	private static long GetInventoryCount(Player player, int itemId)
	{
		return GetInventoryCount(player.InventoryItems, itemId);
	}

	private static long GetInventoryCount(IReadOnlyList<InventoryItem> inventoryItems, int itemId)
	{
		return inventoryItems.Where(item => item.ItemId == itemId).Sum(item => item.Count);
	}

	private static int GetGeneralInfoWarehouseRestrictionFlag(int itemId, ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		// Java parity: GeneralInfoBlobEntry reads ItemRestrictionCleanupData.hasAccountOrLegionWhStorabilityDisabled.
		return itemRestrictionCleanups?.HasAccountOrLegionWarehouseStorabilityDisabled(itemId) == true ? 3 : 0;
	}

	private static void PlanDecreaseByItemId(
		List<InventoryItem> workingItems,
		int itemId,
		long count,
		bool deleteWhenZero,
		Dictionary<int, InventoryItem> updatedItemsByObjectId,
		List<int> deletedObjectIds,
		List<PortalRequirementConsumptionStep> consumptionSteps)
	{
		var remaining = count;
		foreach (var item in workingItems.Where(candidate => candidate.ItemId == itemId && candidate.Count > 0).ToArray())
		{
			if (remaining == 0)
				break;

			var consumed = Math.Min(item.Count, remaining);
			var newCount = item.Count - consumed;
			if (newCount == 0 && deleteWhenZero)
			{
				updatedItemsByObjectId.Remove(item.ObjectId);
				deletedObjectIds.Add(item.ObjectId);
				workingItems.RemoveAll(candidate => candidate.ObjectId == item.ObjectId);
			}
			else
			{
				var updatedItem = CopyInventoryItem(item, newCount);
				updatedItemsByObjectId[updatedItem.ObjectId] = updatedItem;
				ReplaceInventoryItem(workingItems, updatedItem);
			}

			consumptionSteps.Add(new PortalRequirementConsumptionStep(
				itemId,
				item.ObjectId,
				consumed,
				newCount,
				IsKinah: itemId == KinahItemId));
			remaining -= consumed;
		}
	}

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem replacement)
	{
		var index = items.FindIndex(item => item.ObjectId == replacement.ObjectId);
		if (index >= 0)
			items[index] = replacement;
	}

	private static Player CreatePacketPlayer(Player player, IReadOnlyList<InventoryItem> inventoryItems)
	{
		return new Player
		{
			ObjectId = player.ObjectId,
			InventoryItems = inventoryItems,
			NpcExpands = player.NpcExpands,
			QuestExpands = player.QuestExpands,
			ItemExpands = player.ItemExpands,
		};
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long count)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
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

	private static PortalTeamEntryPlan? CreateUnsupportedTeamPlan(
		Player player,
		int worldId,
		int maxPlayers,
		WorldMapRuntimeStateTable worldMaps,
		bool groupRequirementBypassed)
	{
		// Java parity: services/teleport/PortalService.port preserves team id/member context before group/alliance transfer fanout.
		if (maxPlayers is 3 or 6
			&& player.TeamMembership == PlayerTeamMembership.None
			&& groupRequirementBypassed)
		{
			var registeredInstance = worldMaps.GetRegisteredInstance(worldId, player.ObjectId);
			return new PortalTeamEntryPlan(
				PortalTeamEntryKind.PlayerObject,
				player.ObjectId,
				Array.Empty<int>(),
				maxPlayers,
				GetTeamEntryDisposition(registeredInstance),
				registeredInstance,
				IsRegisteredReentry(player, worldId, registeredInstance),
				FanoutSupported: false);
		}

		if (maxPlayers is 3 or 6 && player.TeamMembership == PlayerTeamMembership.Group)
		{
			var groupSnapshot = PlayerGroupSnapshotResolver.Resolve(player);
			var teamId = groupSnapshot?.TeamId ?? 0;
			var memberObjectIds = groupSnapshot?.MemberObjectIds ?? Array.Empty<int>();
			var registeredInstance = teamId == 0 ? null : worldMaps.GetRegisteredInstance(worldId, teamId);
			var registeredInstanceFromMemberScan = false;
			if (registeredInstance == null && groupRequirementBypassed)
			{
				registeredInstance = FindRegisteredMemberInstance(worldMaps, worldId, memberObjectIds);
				registeredInstanceFromMemberScan = registeredInstance != null;
			}

			return new PortalTeamEntryPlan(
				PortalTeamEntryKind.Group,
				teamId,
				memberObjectIds,
				maxPlayers,
				GetTeamEntryDisposition(registeredInstance),
				registeredInstance,
				IsRegisteredReentry(player, worldId, registeredInstance),
				FanoutSupported: false,
				RegisteredInstanceFromMemberScan: registeredInstanceFromMemberScan);
		}

		if (maxPlayers > 6)
		{
			if (player.TeamMembership != PlayerTeamMembership.Alliance && !groupRequirementBypassed)
				return null;

			var allianceSnapshot = player.CurrentAllianceSnapshot;
			var leagueId = allianceSnapshot?.LeagueId ?? 0;
			var teamKind = player.TeamMembership == PlayerTeamMembership.Alliance
				? leagueId > 0 ? PortalTeamEntryKind.League : PortalTeamEntryKind.Alliance
				: PortalTeamEntryKind.PlayerObject;
			var teamId = teamKind switch
			{
				PortalTeamEntryKind.League => leagueId,
				PortalTeamEntryKind.Alliance => allianceSnapshot?.AllianceId ?? player.CurrentTeamId,
				_ => player.ObjectId,
			};
			var memberObjectIds = teamKind == PortalTeamEntryKind.PlayerObject
				? Array.Empty<int>()
				: allianceSnapshot?.MemberObjectIds ?? player.CurrentTeamMemberObjectIds;
			var registeredInstance = teamId == 0 ? null : worldMaps.GetRegisteredInstance(worldId, teamId);
			return new PortalTeamEntryPlan(
				teamKind,
				teamId,
				memberObjectIds,
				maxPlayers,
				GetTeamEntryDisposition(registeredInstance),
				registeredInstance,
				IsRegisteredReentry(player, worldId, registeredInstance),
				FanoutSupported: false);
		}

		return null;
	}

	private static WorldMapInstanceRuntimeState? FindRegisteredMemberInstance(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		IReadOnlyList<int> memberObjectIds)
	{
		// Java parity: PortalService.port scans group.getMembers() and reuses the first solo-registered member instance
		// only when default group requirement is disabled.
		foreach (var memberObjectId in memberObjectIds)
		{
			var registeredInstance = worldMaps.GetRegisteredInstance(worldId, memberObjectId);
			if (registeredInstance != null)
				return registeredInstance;
		}

		return null;
	}

	private static PortalTeamEntryDisposition GetTeamEntryDisposition(WorldMapInstanceRuntimeState? registeredInstance)
	{
		return registeredInstance == null
			? PortalTeamEntryDisposition.FreshInstanceAllocationNeeded
			: PortalTeamEntryDisposition.RegisteredInstanceTransfer;
	}

	private static bool IsRegisteredReentry(
		Player player,
		int worldId,
		WorldMapInstanceRuntimeState? registeredInstance)
	{
		// Java parity: PortalService.port only sets reenter from the early registered-instance check when the player object id is registered.
		return registeredInstance != null
			&& registeredInstance.IsRegistered(player.ObjectId)
			&& (player.Position.WorldId != worldId || player.Position.InstanceId != registeredInstance.InstanceId);
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

public sealed record PortalRequirementConsumptionPlan(
	bool Succeeded,
	IReadOnlyList<InventoryItem> UpdatedItems,
	IReadOnlyList<int> DeletedObjectIds,
	IReadOnlyList<PortalRequirementConsumptionStep> ConsumptionSteps,
	int? MissingItemId,
	long MissingCount)
{
	public static PortalRequirementConsumptionPlan Success(
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<int> deletedObjectIds,
		IReadOnlyList<PortalRequirementConsumptionStep> consumptionSteps)
	{
		return new PortalRequirementConsumptionPlan(
			true,
			updatedItems,
			deletedObjectIds,
			consumptionSteps,
			null,
			0);
	}

	public static PortalRequirementConsumptionPlan Failed(
		int missingItemId,
		long missingCount,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<int> deletedObjectIds,
		IReadOnlyList<PortalRequirementConsumptionStep> consumptionSteps)
	{
		return new PortalRequirementConsumptionPlan(
			false,
			updatedItems,
			deletedObjectIds,
			consumptionSteps,
			missingItemId,
			missingCount);
	}
}

public sealed record PortalRequirementConsumptionStep(
	int ItemId,
	int ObjectId,
	long ConsumedCount,
	long RemainingItemCount,
	bool IsKinah);

public sealed record PortalRequirementConsumptionApplication(
	bool Applied,
	IReadOnlyList<InventoryItem> InventoryItems,
	IReadOnlyList<GameServerPacket> Packets,
	IReadOnlyList<InventoryItem> UpdatedItems,
	IReadOnlyList<int> DeletedObjectIds,
	IReadOnlyList<int> MissingTemplateIds)
{
	public static PortalRequirementConsumptionApplication Success(
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<GameServerPacket> packets,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<int> deletedObjectIds)
	{
		return new PortalRequirementConsumptionApplication(
			true,
			inventoryItems,
			packets,
			updatedItems,
			deletedObjectIds,
			Array.Empty<int>());
	}

	public static PortalRequirementConsumptionApplication NotApplied(IReadOnlyList<InventoryItem> inventoryItems)
	{
		return new PortalRequirementConsumptionApplication(
			false,
			inventoryItems,
			Array.Empty<GameServerPacket>(),
			Array.Empty<InventoryItem>(),
			Array.Empty<int>(),
			Array.Empty<int>());
	}

	public static PortalRequirementConsumptionApplication MissingTemplates(
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<int> missingTemplateIds)
	{
		return new PortalRequirementConsumptionApplication(
			false,
			inventoryItems,
			Array.Empty<GameServerPacket>(),
			Array.Empty<InventoryItem>(),
			Array.Empty<int>(),
			missingTemplateIds);
	}
}

public enum PortalEntryValidationStatus
{
	Allowed,
	MissingPortalLocation,
	UnsupportedTeamPortal,
	GroupRequired,
	AllianceRequired,
	LeagueRequired,
	CooldownLocked,
	LevelRestricted,
	MentorRestricted,
	RaceRestricted,
	RankRestricted,
	TitleRestricted,
	QuestRestricted,
	KinahRestricted,
	ItemRestricted,
}

public sealed record PortalEntryPlanResult(
	bool CanEnter,
	PortalEntryValidationStatus Status,
	PortalEntryPlanAction Action,
	PortalLocSummary? PortalLoc,
	WorldMapInstanceRuntimeState? RegisteredInstance,
	bool Reenter,
	PortalTeamEntryPlan? TeamPlan,
	GameServerPacket? FailurePacket,
	byte DifficultyId = 0)
{
	public static PortalEntryPlanResult Allowed(
		PortalLocSummary portalLoc,
		WorldMapInstanceRuntimeState? registeredInstance,
		bool reenter,
		byte difficultyId = 0)
	{
		return new PortalEntryPlanResult(
			true,
			PortalEntryValidationStatus.Allowed,
			PortalEntryPlanAction.Continue,
			portalLoc,
			registeredInstance,
			reenter,
			null,
			null,
			difficultyId);
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
			null,
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
			null,
			null);
	}

	public static PortalEntryPlanResult UnsupportedTeamPortal(
		PortalLocSummary portalLoc,
		PortalTeamEntryPlan? teamPlan = null)
	{
		return new PortalEntryPlanResult(
			false,
			PortalEntryValidationStatus.UnsupportedTeamPortal,
			PortalEntryPlanAction.None,
			portalLoc,
			null,
			false,
			teamPlan,
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
			null,
			failurePacket);
	}
}

public sealed record PortalTeamEntryPlan(
	PortalTeamEntryKind Kind,
	int TeamId,
	IReadOnlyList<int> MemberObjectIds,
	int MaxPlayers,
	PortalTeamEntryDisposition Disposition,
	WorldMapInstanceRuntimeState? RegisteredInstance,
	bool Reenter,
	bool FanoutSupported,
	byte DifficultyId = 0,
	bool RegisteredInstanceFromMemberScan = false);

public enum PortalTeamEntryKind
{
	PlayerObject,
	Group,
	Alliance,
	League,
}

public enum PortalTeamEntryDisposition
{
	FreshInstanceAllocationNeeded,
	RegisteredInstanceTransfer,
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
