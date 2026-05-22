using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;

namespace Aion.GameServer.Dataholders;

public sealed class StaticData
{
	private StaticData(
		string cacheFilePath,
		IReadOnlyList<string> importedFiles,
		IReadOnlyDictionary<string, int> elementCounts,
		IReadOnlyList<string> topLevelElements,
		IReadOnlyList<WorldMapSummary> worldMaps,
		PlayerExperienceTable playerExperienceTable,
		ItemTemplateTable itemTemplates,
		CosmeticItemTable cosmeticItems,
		DecomposableItemTable decomposableItems,
		AssemblyItemTable assemblyItems,
		RideTable rideInfos,
		ItemRandomBonusTable itemRandomBonuses,
		ItemSetTable itemSets,
		EnchantTable enchantTemplates,
		TemperingTable temperingTemplates,
		NpcTemplateTable npcTemplates,
		NpcSpawnTable npcSpawns,
		SkillTemplateTable skillTemplates,
		TitleTemplateTable titleTemplates,
		RecipeTemplateTable recipeTemplates,
		HousingTemplateTable housingTemplates,
		HousingObjectTemplateTable housingObjectTemplates,
		InstanceCooltimeTable instanceCooltimes,
		PlayerInitialDataTable playerInitialData,
		SkillTreeTable skillTree,
		Task? validationTask)
	{
		CacheFilePath = cacheFilePath;
		ImportedFiles = importedFiles;
		ElementCounts = elementCounts;
		TopLevelElements = topLevelElements;
		WorldMaps = worldMaps;
		PlayerExperienceTable = playerExperienceTable;
		ItemTemplates = itemTemplates;
		CosmeticItems = cosmeticItems;
		DecomposableItems = decomposableItems;
		AssemblyItems = assemblyItems;
		RideInfos = rideInfos;
		ItemRandomBonuses = itemRandomBonuses;
		ItemSets = itemSets;
		EnchantTemplates = enchantTemplates;
		TemperingTemplates = temperingTemplates;
		NpcTemplates = npcTemplates;
		NpcSpawns = npcSpawns;
		SkillTemplates = skillTemplates;
		TitleTemplates = titleTemplates;
		RecipeTemplates = recipeTemplates;
		HousingTemplates = housingTemplates;
		HousingObjectTemplates = housingObjectTemplates;
		InstanceCooltimes = instanceCooltimes;
		PlayerInitialData = playerInitialData;
		SkillTree = skillTree;
		ValidationTask = validationTask;
	}

	public string CacheFilePath { get; }

	public IReadOnlyList<string> ImportedFiles { get; }

	public int ImportedFileCount => ImportedFiles.Count;

	public IReadOnlyDictionary<string, int> ElementCounts { get; }

	public IReadOnlyList<string> TopLevelElements { get; }

	public IReadOnlyList<WorldMapSummary> WorldMaps { get; }

	public PlayerExperienceTable PlayerExperienceTable { get; }

	public ItemTemplateTable ItemTemplates { get; }

	public CosmeticItemTable CosmeticItems { get; }

	public DecomposableItemTable DecomposableItems { get; }

	public AssemblyItemTable AssemblyItems { get; }

	public RideTable RideInfos { get; }

	public ItemRandomBonusTable ItemRandomBonuses { get; }

	public ItemSetTable ItemSets { get; }

	public EnchantTable EnchantTemplates { get; }

	public TemperingTable TemperingTemplates { get; }

	public NpcTemplateTable NpcTemplates { get; }

	public NpcSpawnTable NpcSpawns { get; }

	public SkillTemplateTable SkillTemplates { get; }

	public TitleTemplateTable TitleTemplates { get; }

	public RecipeTemplateTable RecipeTemplates { get; }

	public HousingTemplateTable HousingTemplates { get; }

	public HousingObjectTemplateTable HousingObjectTemplates { get; }

	public InstanceCooltimeTable InstanceCooltimes { get; }

	public PlayerInitialDataTable PlayerInitialData { get; }

	public SkillTreeTable SkillTree { get; }

	public Task? ValidationTask { get; }

	public int GetElementCount(string elementName)
	{
		return ElementCounts.TryGetValue(elementName, out var count) ? count : 0;
	}

	public static async Task<StaticData> LoadFromCacheAsync(
		string cacheFilePath,
		IReadOnlyList<string> importedFiles,
		Task? validationTask = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dataholders/DataManager static_data.xml import graph plus typed DataHolder caches.
		var counts = new Dictionary<string, int>(StringComparer.Ordinal);
		var topLevelElements = new List<string>();
		var worldMaps = new List<WorldMapSummary>();
		var experience = new List<long>();
		var itemTemplates = new List<ItemTemplateSummary>();
		var cosmeticItems = new List<CosmeticItemSummary>();
		var decomposableItems = new List<DecomposableItemSummary>();
		var assemblyItems = new List<AssemblyItemSummary>();
		var rideInfos = new List<RideInfoSummary>();
		var itemRandomBonuses = new List<ItemRandomBonusSummary>();
		var itemSets = new List<ItemSetSummary>();
		var enchantGroups = new List<EnchantGroupSummary>();
		var temperingGroups = new List<TemperingGroupSummary>();
		var npcTemplates = new List<NpcTemplateSummary>();
		var npcSpawns = new List<NpcSpawnSummary>();
		var skillTemplates = new List<SkillTemplateSummary>();
		var titleTemplates = new List<TitleTemplateSummary>();
		var recipeTemplates = new List<RecipeTemplateSummary>();
		var housingAddresses = new List<HousingAddressSummary>();
		var housingLandMinLevels = new Dictionary<int, int>();
		var housingLandMaintenanceFees = new Dictionary<int, long>();
		var housingLandFirstBuildingIds = new Dictionary<int, int>();
		var housingLandDefaultBuildingIds = new Dictionary<int, int>();
		var housingBuildings = new List<HousingBuildingSummary>();
		var housingParts = new List<HousingPartSummary>();
		var housingObjectTemplates = new List<HousingObjectTemplateSummary>();
		var instanceCooltimes = new List<InstanceCooltimeSummary>();
		var skillTree = new List<SkillLearnSummary>();
		var learnableEmotionIds = new HashSet<int>();
		var creationItemsByClass = new Dictionary<string, List<StartingItem>>(StringComparer.OrdinalIgnoreCase);
		var spawnLocationsByRace = new Dictionary<string, PlayerSpawnLocation>(StringComparer.OrdinalIgnoreCase);
		string? currentPlayerCreationClass = null;
		InstanceCooltimeBuilder? currentInstanceCooltime = null;
		ItemTemplateBuilder? currentItemTemplate = null;
		ItemRandomBonusBuilder? currentItemRandomBonus = null;
		ItemSetBuilder? currentItemSet = null;
		EnchantGroupBuilder? currentEnchantGroup = null;
		TemperingGroupBuilder? currentTemperingGroup = null;
		NpcTemplateBuilder? currentNpcTemplate = null;
		NpcSpawnBuilder? currentNpcSpawn = null;
		int currentNpcSpawnMapId = 0;
		int currentNpcSpawnDepth = -1;
		SkillTemplateBuilder? currentSkillTemplate = null;
		TitleTemplateBuilder? currentTitleTemplate = null;
		CosmeticItemBuilder? currentCosmeticItem = null;
		DecomposableItemBuilder? currentDecomposableItem = null;
		HousingBuildingBuilder? currentHousingBuilding = null;
		int currentHousingLandId = 0;
		int currentHousingManagerNpcId = 0;
		var elementPath = new Dictionary<int, string>();
		var settings = new XmlReaderSettings
		{
			Async = true,
			DtdProcessing = DtdProcessing.Prohibit,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
		};

		using var reader = XmlReader.Create(cacheFilePath, settings);
		while (await reader.ReadAsync())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (reader.NodeType == XmlNodeType.EndElement)
			{
				if (reader.Depth == 2 && reader.LocalName == "instance_cooltime" && currentInstanceCooltime != null)
				{
					instanceCooltimes.Add(currentInstanceCooltime.ToSummary());
					currentInstanceCooltime = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "item_template" && currentItemTemplate != null)
				{
					itemTemplates.Add(currentItemTemplate.ToSummary());
					currentItemTemplate = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "cosmetic_item" && currentCosmeticItem != null)
				{
					cosmeticItems.Add(currentCosmeticItem.ToSummary());
					currentCosmeticItem = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "decomposable" && currentDecomposableItem != null)
				{
					decomposableItems.Add(currentDecomposableItem.ToSummary());
					currentDecomposableItem = null;
				}

				if (reader.Depth == 3 && reader.LocalName == "items" && currentDecomposableItem != null)
					currentDecomposableItem.EndCollection();

				if (reader.Depth == 2 && reader.LocalName == "random_bonus" && currentItemRandomBonus != null)
				{
					itemRandomBonuses.Add(currentItemRandomBonus.ToSummary());
					currentItemRandomBonus = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "itemset" && currentItemSet != null)
				{
					itemSets.Add(currentItemSet.ToSummary());
					currentItemSet = null;
				}

				if (reader.Depth == 3 && currentItemSet != null && reader.LocalName is "partbonus" or "fullbonus")
					currentItemSet.EndBonus();

				if (reader.Depth == 4 && currentItemTemplate != null && IsStatModifierElement(reader.LocalName))
					currentItemTemplate.EndModifier();

				if (reader.Depth == 2 && reader.LocalName == "enchant_list" && currentEnchantGroup != null)
				{
					enchantGroups.Add(currentEnchantGroup.ToSummary());
					currentEnchantGroup = null;
				}

				if (reader.Depth == 3 && reader.LocalName == "enchant_data" && currentEnchantGroup != null)
					currentEnchantGroup.EndLevel();

				if (reader.Depth == 2 && reader.LocalName == "tempering_list" && currentTemperingGroup != null)
				{
					temperingGroups.Add(currentTemperingGroup.ToSummary());
					currentTemperingGroup = null;
				}

				if (reader.Depth == 3 && reader.LocalName == "tempering_data" && currentTemperingGroup != null)
					currentTemperingGroup.EndLevel();

				if (reader.Depth == 2 && reader.LocalName == "npc_template" && currentNpcTemplate != null)
				{
					npcTemplates.Add(currentNpcTemplate.ToSummary());
					currentNpcTemplate = null;
				}

				if (reader.Depth == currentNpcSpawnDepth && reader.LocalName == "spawn")
				{
					currentNpcSpawn = null;
					currentNpcSpawnDepth = -1;
				}

				if (reader.Depth == 2 && reader.LocalName == "spawn_map" && elementPath.GetValueOrDefault(1) == "spawns")
					currentNpcSpawnMapId = 0;

				if (reader.Depth == 2 && reader.LocalName == "skill_template" && currentSkillTemplate != null)
				{
					skillTemplates.Add(currentSkillTemplate.ToSummary());
					currentSkillTemplate = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "title" && currentTitleTemplate != null)
				{
					titleTemplates.Add(currentTitleTemplate.ToSummary());
					currentTitleTemplate = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "building" && currentHousingBuilding != null)
				{
					housingBuildings.Add(currentHousingBuilding.ToSummary());
					currentHousingBuilding = null;
				}

				if (reader.Depth == 4 && reader.LocalName is "armormastery" or "wpnmastery" or "shieldmastery")
					currentSkillTemplate?.EndMastery();

				if (reader.Depth == 2 && reader.LocalName == "player_data")
					currentPlayerCreationClass = null;
				if (reader.Depth == 2 && reader.LocalName == "land")
				{
					currentHousingLandId = 0;
					currentHousingManagerNpcId = 0;
				}
				elementPath.Remove(reader.Depth);
				continue;
			}

			if (reader.NodeType != XmlNodeType.Element)
				continue;

			foreach (var depth in elementPath.Keys.Where(depth => depth >= reader.Depth).ToArray())
				elementPath.Remove(depth);
			elementPath[reader.Depth] = reader.LocalName;

			counts[reader.LocalName] = counts.GetValueOrDefault(reader.LocalName) + 1;
			if (reader.Depth == 1)
				topLevelElements.Add(reader.LocalName);
			if (reader.LocalName == "exp"
				&& elementPath.TryGetValue(reader.Depth - 1, out var parentElement)
				&& parentElement == "player_experience_table")
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				if (long.TryParse(value, out var parsedExperience))
					experience.Add(parsedExperience);
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "map")
			{
				var idText = reader.GetAttribute("id");
				if (int.TryParse(idText, out var mapId))
				{
					var isInstance = bool.TryParse(reader.GetAttribute("instance"), out var parsedInstance) && parsedInstance;
					var twinCount = int.TryParse(reader.GetAttribute("twin_count"), out var parsedTwinCount) ? parsedTwinCount : 0;
					worldMaps.Add(new WorldMapSummary(mapId, isInstance, twinCount));
				}
			}

			if (reader.Depth == 2 && reader.LocalName == "spawn_map" && elementPath.GetValueOrDefault(1) == "spawns")
			{
				currentNpcSpawnMapId = ReadRequiredIntAttribute(reader, "map_id");
				continue;
			}

			if (currentNpcSpawnMapId != 0
				&& reader.LocalName == "spawn"
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spawn_map")
			{
				// Java parity: dataholders/SpawnsData loads direct Spawn groups from static_data/spawns/*.xml.
				currentNpcSpawn = new NpcSpawnBuilder(
					currentNpcSpawnMapId,
					ReadRequiredIntAttribute(reader, "npc_id"),
					ReadIntAttribute(reader, "respawn_time"),
					ReadIntAttribute(reader, "pool"),
					reader.GetAttribute("handler") ?? string.Empty,
					ReadBoolAttribute(reader, "custom"));
				currentNpcSpawnDepth = reader.Depth;
				continue;
			}

			if (currentNpcSpawn != null
				&& reader.LocalName == "spot"
				&& reader.Depth == currentNpcSpawnDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spawn")
			{
				// Java parity: model/templates/spawns/SpawnSpotTemplate spot coordinates and movement metadata.
				npcSpawns.Add(currentNpcSpawn.ToSummary(reader));
				continue;
			}

			if (reader.Depth == 2
				&& reader.LocalName == "item"
				&& elementPath.GetValueOrDefault(1) == "assembly_items")
			{
				// Java parity: data/static_data/items/assembly_items.xml JAXB item id/parts attributes.
				assemblyItems.Add(new AssemblyItemSummary(
					ReadRequiredIntAttribute(reader, "id"),
					ReadIntListAttribute(reader, "parts")));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "cosmetic_item")
			{
				currentCosmeticItem = new CosmeticItemBuilder(
					reader.GetAttribute("type") ?? string.Empty,
					reader.GetAttribute("cosmetic_name") ?? string.Empty,
					ReadIntAttribute(reader, "id"),
					reader.GetAttribute("race") ?? string.Empty,
					reader.GetAttribute("gender_permitted") ?? string.Empty);
				if (reader.IsEmptyElement)
				{
					cosmeticItems.Add(currentCosmeticItem.ToSummary());
					currentCosmeticItem = null;
				}
				continue;
			}

			if (reader.Depth == 4 && currentCosmeticItem != null && elementPath.GetValueOrDefault(3) == "preset")
			{
				var presetField = reader.LocalName;
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentCosmeticItem.SetPresetValue(presetField, value);
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "decomposable")
			{
				currentDecomposableItem = new DecomposableItemBuilder(
					ReadRequiredIntAttribute(reader, "item_id"),
					ReadBoolAttribute(reader, "selectable"));
				if (reader.IsEmptyElement)
				{
					decomposableItems.Add(currentDecomposableItem.ToSummary());
					currentDecomposableItem = null;
				}

				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "items" && currentDecomposableItem != null)
			{
				// Java parity: model/templates/rewards/ExtractedItemsCollection default chance/min/max values.
				currentDecomposableItem.StartCollection(
					ReadOptionalFloatAttribute(reader, "chance", 100f),
					ReadOptionalIntAttribute(reader, "minlevel", 0),
					ReadOptionalIntAttribute(reader, "maxlevel", 99));
				if (reader.IsEmptyElement)
					currentDecomposableItem.EndCollection();

				continue;
			}

			if (reader.Depth == 4
				&& reader.LocalName == "item"
				&& currentDecomposableItem != null
				&& elementPath.GetValueOrDefault(3) == "items")
			{
				var minCount = ReadOptionalIntAttribute(reader, "min_count", 1);
				var maxCount = ReadOptionalIntAttribute(reader, "max_count", minCount);
				if (maxCount == 0)
					maxCount = minCount;
				currentDecomposableItem.AddItem(
					new ResultedItemSummary(
						ReadRequiredIntAttribute(reader, "id"),
						minCount,
						maxCount,
						reader.GetAttribute("race") ?? "PC_ALL",
						ReadPlayerClasses(reader.GetAttribute("player_classes"))));
				continue;
			}

			if (reader.Depth == 4
				&& reader.LocalName == "random_item"
				&& currentDecomposableItem != null
				&& elementPath.GetValueOrDefault(3) == "items")
			{
				var minCount = ReadOptionalIntAttribute(reader, "min_count", 1);
				var maxCount = ReadOptionalIntAttribute(reader, "max_count", minCount);
				if (maxCount == 0)
					maxCount = minCount;
				currentDecomposableItem.AddRandomItem(
					new RandomItemSummary(
						reader.GetAttribute("type") ?? string.Empty,
						minCount,
						maxCount));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "instance_cooltime")
			{
				currentInstanceCooltime = new InstanceCooltimeBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					ReadRequiredIntAttribute(reader, "worldId"),
					reader.GetAttribute("race") ?? string.Empty);
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "land")
			{
				// Java parity: model/templates/housing/HousingLand id/manager_npc used by House.matchesLandRace.
				currentHousingLandId = ReadRequiredIntAttribute(reader, "id");
				currentHousingManagerNpcId = ReadRequiredIntAttribute(reader, "manager_npc");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "address" && currentHousingLandId != 0)
			{
				// Java parity: model/templates/housing/HouseAddress links an address back to its HousingLand and town id.
				housingAddresses.Add(
					new HousingAddressSummary(
						ReadRequiredIntAttribute(reader, "id"),
						currentHousingLandId,
						currentHousingManagerNpcId,
						ReadIntAttribute(reader, "town"),
						MapId: ReadRequiredIntAttribute(reader, "map"),
						X: ReadFloatAttribute(reader, "x"),
						Y: ReadFloatAttribute(reader, "y"),
						Z: ReadFloatAttribute(reader, "z"),
						ExitMapId: ReadNullableIntAttribute(reader, "exit_map"),
						ExitX: ReadNullableFloatAttribute(reader, "exit_x"),
						ExitY: ReadNullableFloatAttribute(reader, "exit_y"),
						ExitZ: ReadNullableFloatAttribute(reader, "exit_z")));
				continue;
			}

			if (reader.Depth == 4
				&& reader.LocalName == "building"
				&& currentHousingLandId != 0
				&& elementPath.GetValueOrDefault(3) == "buildings")
			{
				// Java parity: model/templates/housing/HousingLand.getDefaultBuilding.
				var buildingId = ReadRequiredIntAttribute(reader, "id");
				housingLandFirstBuildingIds.TryAdd(currentHousingLandId, buildingId);
				if (ReadBoolAttribute(reader, "default") && !housingLandDefaultBuildingIds.ContainsKey(currentHousingLandId))
					housingLandDefaultBuildingIds[currentHousingLandId] = buildingId;
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "sale" && currentHousingLandId != 0)
			{
				// Java parity: model/templates/housing/Sale.level used as fallback minimum bid level.
				housingLandMinLevels[currentHousingLandId] = ReadIntAttribute(reader, "level");
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "fee" && currentHousingLandId != 0)
			{
				// Java parity: model/templates/housing/HousingLand.maintenanceFee used by CM_HOUSE_PAY_RENT.
				var value = await ReadElementTextAsync(reader, cancellationToken);
				housingLandMaintenanceFees[currentHousingLandId] = long.TryParse(value, out var parsedFee) ? parsedFee : 0;
				continue;
			}

			if (reader.Depth == 2
				&& reader.LocalName == "building"
				&& elementPath.TryGetValue(1, out var rootElement)
				&& rootElement == "buildings")
			{
				// Java parity: model/templates/housing/Building fields from housing/house_buildings.xml.
				var size = reader.GetAttribute("size") ?? string.Empty;
				currentHousingBuilding = new HousingBuildingBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					size,
					GetHouseTypeId(size),
					reader.GetAttribute("type") ?? string.Empty,
					reader.GetAttribute("parts_match") ?? string.Empty);
				continue;
			}

			if (currentHousingBuilding != null && IsHousingBuildingPartElement(reader.LocalName))
			{
				var partName = reader.LocalName;
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentHousingBuilding.SetDefaultPart(partName, int.TryParse(value, out var parsedPartId) ? parsedPartId : 0);
				continue;
			}

			if (reader.Depth == 2
				&& elementPath.GetValueOrDefault(1) == "house_parts"
				&& reader.LocalName == "house_part")
			{
				// Java parity: dataholders/HousePartsData indexes HousePart by id for decoration validation.
				housingParts.Add(
					new HousingPartSummary(
						ReadRequiredIntAttribute(reader, "id"),
						reader.GetAttribute("type") ?? string.Empty,
						SplitHousePartTags(reader.GetAttribute("building_tags"))));
				continue;
			}

			if (reader.Depth == 2
				&& elementPath.GetValueOrDefault(1) == "housing_objects"
				&& IsHousingObjectTemplateElement(reader.LocalName))
			{
				// Java parity: dataholders/HousingObjectData indexes PlaceableHouseObject templates by id.
				housingObjectTemplates.Add(
					new HousingObjectTemplateSummary(
						ReadRequiredIntAttribute(reader, "id"),
						GetHousingObjectTypeId(reader.LocalName),
						reader.LocalName,
						reader.GetAttribute("area") ?? string.Empty,
						reader.GetAttribute("location") ?? string.Empty,
						reader.GetAttribute("limit") ?? "NONE",
						reader.GetAttribute("category") ?? string.Empty,
						ReadIntAttribute(reader, "use_days"),
						ReadBoolAttribute(reader, "can_dye"),
						NpcId: ReadIntAttribute(reader, "npc_id"),
						WarehouseId: ReadIntAttribute(reader, "warehouse_id"),
						OwnerOnly: ReadBoolAttribute(reader, "owner"),
						CooldownSeconds: ReadIntAttribute(reader, "cd"),
						DelayMilliseconds: ReadIntAttribute(reader, "delay"),
						UseCount: ReadIntAttribute(reader, "use_count"),
						RequiredItemId: ReadIntAttribute(reader, "required_item"),
						EmblemLevel: ReadIntAttribute(reader, "level"),
						NameId: ReadIntAttribute(reader, "name_id"),
						TalkingDistance: ReadFloatAttribute(reader, "talking_distance")));
				continue;
			}

			if (reader.Depth == 3
				&& reader.LocalName == "action"
				&& elementPath.GetValueOrDefault(1) == "housing_objects"
				&& elementPath.GetValueOrDefault(2) == "use_item"
				&& housingObjectTemplates.Count > 0)
			{
				// Java parity: model/templates/housing/UseItemAction.checkType serialized by UseableItemObject.writeUsageData.
				var lastIndex = housingObjectTemplates.Count - 1;
				housingObjectTemplates[lastIndex] = housingObjectTemplates[lastIndex] with
				{
					UseActionCheckType = ReadIntAttribute(reader, "check_type"),
					UseActionRemoveCount = ReadIntAttribute(reader, "remove_count"),
					UseActionRewardId = ReadIntAttribute(reader, "reward_id"),
					UseActionFinalRewardId = ReadIntAttribute(reader, "final_reward_id"),
				};
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "maxcount" && currentInstanceCooltime != null)
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentInstanceCooltime.MaxCount = int.TryParse(value, out var parsedMaxCount) ? parsedMaxCount : 0;
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "item_template")
			{
				var requiredLevels = ReadLevelRestrictions(reader.GetAttribute("restrict"));
				// Java parity: model/templates/item/ItemTemplate.weaponBoost feeds PlayerGameStats.getPowerShardDamage.
				currentItemTemplate = new ItemTemplateBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					reader.GetAttribute("name") ?? string.Empty,
					ReadIntAttribute(reader, "desc"),
					ReadIntAttribute(reader, "mask"),
					ReadIntAttribute(reader, "level"),
					reader.GetAttribute("item_group") ?? string.Empty,
					reader.GetAttribute("item_type") ?? string.Empty,
					reader.GetAttribute("quality") ?? string.Empty,
					reader.GetAttribute("race") ?? string.Empty,
					reader.GetAttribute("attack_type") ?? string.Empty,
					ReadOptionalIntAttribute(reader, "max_stack_count", 1),
					ReadLongAttribute(reader, "price"),
					GetItemGroupSlots(reader.GetAttribute("item_group")),
					ReadIntAttribute(reader, "m_slots"),
					ReadIntAttribute(reader, "s_slots"),
					requiredLevels,
					ReadLevelRestrictions(reader.GetAttribute("restrict_max")),
					ReadIntAttribute(reader, "activate_count"),
					ReadIntAttribute(reader, "expire_time"),
					ReadIntAttribute(reader, "enchant_type"),
					ReadIntAttribute(reader, "max_enchant"),
					ReadIntAttribute(reader, "max_enchant_bonus"),
					ReadBoolAttribute(reader, "can_exceed_enchant"),
					reader.GetAttribute("exceed_enchant_skill") ?? string.Empty,
					ReadIntAttribute(reader, "option_slot_bonus"),
					ReadIntAttribute(reader, "rnd_bonus"),
					ReadOptionalIntAttribute(reader, "rnd_count", -1),
					reader.GetAttribute("enchant_name") ?? string.Empty,
					reader.GetAttribute("tempering_name") ?? string.Empty,
					ReadIntAttribute(reader, "weapon_boost"));
				if (reader.IsEmptyElement)
				{
					itemTemplates.Add(currentItemTemplate.ToSummary());
					currentItemTemplate = null;
				}

				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "random_bonus")
			{
				currentItemRandomBonus = new ItemRandomBonusBuilder(
					reader.GetAttribute("type") ?? string.Empty,
					ReadRequiredIntAttribute(reader, "id"));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "itemset")
			{
				currentItemSet = new ItemSetBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					reader.GetAttribute("name") ?? string.Empty);
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "enchant_list")
			{
				currentEnchantGroup = new EnchantGroupBuilder(reader.GetAttribute("item_group") ?? string.Empty);
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "tempering_list")
			{
				currentTemperingGroup = new TemperingGroupBuilder(reader.GetAttribute("item_group") ?? string.Empty);
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "itempart" && currentItemSet != null)
			{
				currentItemSet.AddItemPart(ReadRequiredIntAttribute(reader, "itemid"));
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "partbonus" && currentItemSet != null)
			{
				currentItemSet.StartPartBonus(ReadIntAttribute(reader, "count"));
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "fullbonus" && currentItemSet != null)
			{
				currentItemSet.StartFullBonus();
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "enchant_data" && currentEnchantGroup != null)
			{
				currentEnchantGroup.StartLevel(ReadRequiredIntAttribute(reader, "level"));
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "tempering_data" && currentTemperingGroup != null)
			{
				currentTemperingGroup.StartLevel(ReadRequiredIntAttribute(reader, "level"));
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "modifiers" && currentItemRandomBonus != null)
			{
				currentItemRandomBonus.AddModifierGroup(ReadFloatAttribute(reader, "chance"));
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "weapon_stats" && currentItemTemplate != null)
			{
				currentItemTemplate.WeaponStats = new ItemWeaponStats(
					ReadIntAttribute(reader, "min_damage"),
					ReadIntAttribute(reader, "max_damage"),
					ReadIntAttribute(reader, "attack_speed"),
					ReadIntAttribute(reader, "critical"),
					ReadIntAttribute(reader, "physical_accuracy"),
					ReadIntAttribute(reader, "parry"),
					ReadIntAttribute(reader, "magical_accuracy"),
					ReadIntAttribute(reader, "boost_magical_skill"),
					ReadIntAttribute(reader, "attack_range"),
					ReadIntAttribute(reader, "hit_count"),
					ReadIntAttribute(reader, "reduce_max"));
				continue;
			}

			if (reader.LocalName == "ride_info")
			{
				rideInfos.Add(
					new RideInfoSummary(
						ReadRequiredIntAttribute(reader, "id"),
						ReadIntAttribute(reader, "type"),
						ReadFloatAttribute(reader, "move_speed"),
						ReadFloatAttribute(reader, "fly_speed"),
						ReadFloatAttribute(reader, "sprint_speed"),
						ReadIntAttribute(reader, "start_fp"),
						ReadIntAttribute(reader, "cost_fp")));
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "godstone" && currentItemTemplate != null)
			{
				currentItemTemplate.GodstoneInfo = new ItemGodstoneInfo(
					ReadIntAttribute(reader, "skillid"),
					ReadIntAttribute(reader, "skilllvl"),
					ReadIntAttribute(reader, "probability"),
					ReadIntAttribute(reader, "probabilityleft"),
					ReadIntAttribute(reader, "breakprob"),
					ReadIntAttribute(reader, "nonbreakcount"));
				continue;
			}

			if (reader.Depth == 4
				&& currentItemTemplate != null
				&& IsStatModifierElement(reader.LocalName)
				&& elementPath.TryGetValue(reader.Depth - 1, out var modifierParent)
				&& modifierParent == "modifiers")
			{
				currentItemTemplate.AddModifier(
					new ItemStatModifier(
						reader.LocalName,
						reader.GetAttribute("name") ?? string.Empty,
						ReadIntAttribute(reader, "value"),
						ReadBoolAttribute(reader, "bonus")));
				continue;
			}

			if (reader.Depth == 6
				&& currentItemTemplate != null
				&& reader.LocalName == "charge"
				&& elementPath.TryGetValue(reader.Depth - 1, out var conditionParent)
				&& conditionParent == "conditions")
			{
				currentItemTemplate.SetCurrentModifierChargeCondition(ReadIntAttribute(reader, "value"));
				continue;
			}

			if (reader.Depth == 4
				&& currentItemRandomBonus != null
				&& IsStatModifierElement(reader.LocalName)
				&& elementPath.TryGetValue(reader.Depth - 1, out var randomModifierParent)
				&& randomModifierParent == "modifiers")
			{
				currentItemRandomBonus.AddModifier(
					new ItemStatModifier(
						reader.LocalName,
						reader.GetAttribute("name") ?? string.Empty,
						ReadIntAttribute(reader, "value"),
						ReadBoolAttribute(reader, "bonus")));
				continue;
			}

			if (reader.Depth == 5
				&& currentItemSet != null
				&& IsStatModifierElement(reader.LocalName)
				&& elementPath.TryGetValue(reader.Depth - 1, out var itemSetModifierParent)
				&& itemSetModifierParent == "modifiers")
			{
				currentItemSet.AddModifier(
					new ItemStatModifier(
						reader.LocalName,
						reader.GetAttribute("name") ?? string.Empty,
						ReadIntAttribute(reader, "value"),
						ReadBoolAttribute(reader, "bonus")));
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "enchant_stat" && currentEnchantGroup != null)
			{
				currentEnchantGroup.AddStat(
					new EnchantStatSummary(
						reader.GetAttribute("stat") ?? string.Empty,
						ReadIntAttribute(reader, "value")));
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "tempering_stat" && currentTemperingGroup != null)
			{
				currentTemperingGroup.AddStat(
					new TemperingStatSummary(
						reader.GetAttribute("stat") ?? string.Empty,
						ReadIntAttribute(reader, "value")));
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "disposition" && currentItemTemplate != null)
			{
				currentItemTemplate.DispositionItemId = ReadIntAttribute(reader, "id");
				currentItemTemplate.DispositionItemCount = ReadIntAttribute(reader, "count");
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "inventory" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/ExtraInventory id used by Storage.isFullSpecialCube.
				currentItemTemplate.ExtraInventoryId = ReadIntAttribute(reader, "id");
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "acquisition" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/Acquisition.getRequiredAp consumed by ApExtractAction.act.
				currentItemTemplate.RequiredAbyssPoints = ReadIntAttribute(reader, "ap");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "ride" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/RideAction.npcId.
				currentItemTemplate.RideNpcId = ReadIntAttribute(reader, "npc_id");
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "improve" && currentItemTemplate != null)
			{
				currentItemTemplate.Improvement = new ItemImprovement(
					ReadIntAttribute(reader, "way"),
					ReadIntAttribute(reader, "level"),
					ReadIntAttribute(reader, "burn_attack"),
					ReadIntAttribute(reader, "burn_defend"),
					ReadIntAttribute(reader, "price1"),
					ReadIntAttribute(reader, "price2"));
				currentItemTemplate.ConditioningMaxLevel = currentItemTemplate.Improvement.Level;
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "idian" && currentItemTemplate != null)
			{
				currentItemTemplate.IdianInfo = new ItemIdianInfo(
					ReadIntAttribute(reader, "burn_attack"),
					ReadIntAttribute(reader, "burn_defend"));
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "stigma" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/Stigma.afterUnmarshal gain skill groups.
				var gainSkillGroup1 = reader.GetAttribute("gain_skill_group1") ?? string.Empty;
				var gainSkillGroup2 = reader.GetAttribute("gain_skill_group2") ?? string.Empty;
				currentItemTemplate.StigmaInfo = new ItemStigmaInfo(
					new[] { gainSkillGroup1, gainSkillGroup2 }
						.Where(group => !string.IsNullOrWhiteSpace(group))
						.ToArray(),
					ReadBoolAttribute(reader, "chargeable"));
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "uselimits" && currentItemTemplate != null)
			{
				currentItemTemplate.GenderPermitted = reader.GetAttribute("gender") ?? string.Empty;
				currentItemTemplate.MinRank = ReadOptionalIntAttribute(reader, "rank_min", 1);
				currentItemTemplate.MaxRank = ReadOptionalIntAttribute(reader, "rank_max", 18);
				currentItemTemplate.RecommendRank = ReadIntAttribute(reader, "recommend_rank");
				currentItemTemplate.UseDelayId = ReadIntAttribute(reader, "usedelayid");
				currentItemTemplate.UseDelayMillis = ReadIntAttribute(reader, "usedelay");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "polish" && currentItemTemplate != null)
			{
				currentItemTemplate.PolishSetId = ReadIntAttribute(reader, "set_id");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "charge" && currentItemTemplate != null)
			{
				currentItemTemplate.ChargeActionMaxLevel = ReadIntAttribute(reader, "capacity");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "enchant" && currentItemTemplate != null)
			{
				currentItemTemplate.EnchantAction = new ItemEnchantActionInfo(
					ReadIntAttribute(reader, "count"),
					ReadIntAttribute(reader, "min_level"),
					ReadIntAttribute(reader, "max_level"),
					ReadBoolAttribute(reader, "manastone_only"),
					ReadFloatAttribute(reader, "chance"));
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "craftlearn" && currentItemTemplate != null)
			{
				currentItemTemplate.CraftLearnRecipeId = ReadIntAttribute(reader, "recipeid");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "skilllearn" && currentItemTemplate != null)
			{
				currentItemTemplate.SkillLearnAction = new ItemSkillLearnActionInfo(
					ReadIntAttribute(reader, "skillid"),
					ReadIntAttribute(reader, "level"),
					reader.GetAttribute("class") ?? string.Empty);
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "expandinventory" && currentItemTemplate != null)
			{
				currentItemTemplate.ExpandInventoryAction = new ItemExpandInventoryActionInfo(
					ReadIntAttribute(reader, "level"),
					reader.GetAttribute("storage") ?? string.Empty);
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "expextract" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/ExpExtractAction item_id/percent/cost metadata.
				currentItemTemplate.ExpExtractAction = new ItemExpExtractActionInfo(
					ReadIntAttribute(reader, "item_id"),
					ReadBoolAttribute(reader, "percent"),
					ReadLongAttribute(reader, "cost"));
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "extract" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/ExtractAction marker.
				currentItemTemplate.HasExtractAction = true;
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "apextract" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/ApExtractAction target/rate metadata.
				currentItemTemplate.ApExtractAction = new ItemApExtractActionInfo(
					ReadFloatAttribute(reader, "rate"),
					reader.GetAttribute("target") ?? string.Empty);
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "dye" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/DyeAction color/minutes metadata.
				var color = reader.GetAttribute("color");
				currentItemTemplate.DyeAction = new ItemDyeActionInfo(
					string.Equals(color, "no", StringComparison.Ordinal)
						? null
						: int.Parse(color ?? "0", NumberStyles.HexNumber, CultureInfo.InvariantCulture),
					ReadIntAttribute(reader, "minutes"),
					reader.GetAttribute("minutes") != null);
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "animation" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/AnimationAddAction motion-slot metadata.
				currentItemTemplate.AnimationAction = new ItemAnimationActionInfo(
					ReadNullableIntAttribute(reader, "idle"),
					ReadNullableIntAttribute(reader, "run"),
					ReadNullableIntAttribute(reader, "jump"),
					ReadNullableIntAttribute(reader, "rest"),
					ReadNullableIntAttribute(reader, "shop"),
					ReadIntAttribute(reader, "minutes"));
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "remodel" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/RemodelAction type/minutes metadata.
				currentItemTemplate.RemodelAction = new ItemRemodelActionInfo(
					ReadIntAttribute(reader, "type"),
					ReadIntAttribute(reader, "minutes"));
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "houseobject" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/SummonHouseObjectAction id consumed by CM_HOUSE_EDIT action 3.
				currentItemTemplate.HasHouseObjectAction = true;
				currentItemTemplate.HouseObjectTemplateId = ReadIntAttribute(reader, "id");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "housedeco" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/DecorateAction allows an absent id, yielding template 0.
				currentItemTemplate.HasHouseDecorateAction = true;
				currentItemTemplate.HouseDecorateTemplateId = ReadIntAttribute(reader, "id");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "decompose" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/DecomposeAction marker.
				currentItemTemplate.HasDecomposeAction = true;
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "composition" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/CompositionAction marker used by CM_COMPOSITE_STONES.
				currentItemTemplate.HasCompositionAction = true;
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "assemble" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/AssemblyItemAction item attribute.
				currentItemTemplate.AssemblyItemId = ReadIntAttribute(reader, "item");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "cosmetic" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/CosmeticItemAction cosmetic-name metadata.
				currentItemTemplate.CosmeticActionName = reader.GetAttribute("name") ?? string.Empty;
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "titleadd" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/TitleAddAction titleid/minutes metadata.
				currentItemTemplate.HasTitleAddAction = true;
				currentItemTemplate.TitleAddTitleId = ReadIntAttribute(reader, "titleid");
				if (reader.GetAttribute("minutes") != null)
				{
					currentItemTemplate.HasTitleAddMinutes = true;
					currentItemTemplate.TitleAddMinutes = ReadIntAttribute(reader, "minutes");
				}
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "learnemotion" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/EmotionLearnAction.afterUnmarshal.
				var emotionId = ReadRequiredIntAttribute(reader, "emotionid");
				learnableEmotionIds.Add(emotionId);
				currentItemTemplate.HasEmotionLearnAction = true;
				currentItemTemplate.EmotionLearnId = emotionId;
				currentItemTemplate.EmotionLearnMinutes = ReadIntAttribute(reader, "minutes");
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "npc_template")
			{
				currentNpcTemplate = new NpcTemplateBuilder(
					ReadRequiredIntAttribute(reader, "npc_id"),
					reader.GetAttribute("name") ?? string.Empty,
					ReadIntAttribute(reader, "name_id"),
					ReadIntAttribute(reader, "level"),
					reader.GetAttribute("rank") ?? string.Empty,
					reader.GetAttribute("rating") ?? string.Empty,
					reader.GetAttribute("race") ?? string.Empty,
					reader.GetAttribute("tribe") ?? string.Empty,
					reader.GetAttribute("type") ?? string.Empty,
					ReadIntAttribute(reader, "title_id"),
					ReadFloatAttribute(reader, "height"),
					ReadIntAttribute(reader, "attack_speed"));
				if (reader.IsEmptyElement)
				{
					npcTemplates.Add(currentNpcTemplate.ToSummary());
					currentNpcTemplate = null;
				}

				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "stats" && currentNpcTemplate != null)
			{
				currentNpcTemplate.MaxHp = ReadIntAttribute(reader, "maxHp");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "speeds" && currentNpcTemplate != null)
			{
				currentNpcTemplate.RunSpeed = ReadFloatAttribute(reader, "run");
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "bound_radius" && currentNpcTemplate != null)
			{
				currentNpcTemplate.BoundRadiusFront = ReadFloatAttribute(reader, "front");
				currentNpcTemplate.BoundRadiusSide = ReadFloatAttribute(reader, "side");
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "talk_info" && currentNpcTemplate != null)
			{
				// Java parity: model/templates/npc/TalkInfo feeds NpcTemplate.getTalkDistance and supportsAction.
				currentNpcTemplate.TalkDistance = ReadOptionalIntAttribute(reader, "distance", 2);
				currentNpcTemplate.FunctionDialogIds.AddRange(ReadIntListAttribute(reader, "func_dialogs"));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "skill_template")
			{
				currentSkillTemplate = new SkillTemplateBuilder(
					ReadRequiredIntAttribute(reader, "skill_id"),
					reader.GetAttribute("name") ?? string.Empty,
					ReadIntAttribute(reader, "nameId"),
					ReadIntAttribute(reader, "lvl"),
					reader.GetAttribute("group") ?? string.Empty,
					reader.GetAttribute("stack") ?? string.Empty,
					reader.GetAttribute("skilltype") ?? string.Empty,
					reader.GetAttribute("skillsubtype") ?? string.Empty,
					ReadIntAttribute(reader, "cooldownId"),
					ReadIntAttribute(reader, "cooldown"))
				{
					StigmaType = reader.GetAttribute("stigma") ?? string.Empty,
				};
				if (reader.IsEmptyElement)
				{
					skillTemplates.Add(currentSkillTemplate.ToSummary());
					currentSkillTemplate = null;
				}
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "armormastery" && currentSkillTemplate != null)
			{
				currentSkillTemplate.StartArmorMastery(
					reader.GetAttribute("armor") ?? string.Empty,
					ReadIntAttribute(reader, "value"),
					ReadIntAttribute(reader, "delta"));
				if (reader.IsEmptyElement)
					currentSkillTemplate.EndMastery();
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "wpnmastery" && currentSkillTemplate != null)
			{
				currentSkillTemplate.StartWeaponMastery(reader.GetAttribute("weapon") ?? string.Empty);
				if (reader.IsEmptyElement)
					currentSkillTemplate.EndMastery();
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "shieldmastery" && currentSkillTemplate != null)
			{
				currentSkillTemplate.StartShieldMastery();
				if (reader.IsEmptyElement)
					currentSkillTemplate.EndMastery();
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "wpndual" && currentSkillTemplate != null)
			{
				currentSkillTemplate.AddWeaponDual(
					new SkillWeaponDualEffectSummary(
						ReadIntAttribute(reader, "value"),
						ReadIntAttribute(reader, "delta"),
						ReadIntAttribute(reader, "skill_efficiency"),
						ReadIntAttribute(reader, "max_damage_chance"),
						ReadIntAttribute(reader, "max_damage_delta")));
				continue;
			}

			if (reader.Depth == 5 && reader.LocalName == "change" && currentSkillTemplate != null)
			{
				currentSkillTemplate.AddCurrentMasteryChange(
					new SkillStatChange(
						reader.GetAttribute("stat") ?? string.Empty,
						reader.GetAttribute("func") ?? string.Empty,
						ReadIntAttribute(reader, "value"),
						ReadIntAttribute(reader, "delta")));
				continue;
			}

			if (reader.Depth == 2
				&& reader.LocalName == "title"
				&& elementPath.TryGetValue(1, out var titleParent)
				&& titleParent == "player_titles")
			{
				currentTitleTemplate = new TitleTemplateBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					ReadIntAttribute(reader, "nameId"),
					reader.GetAttribute("desc") ?? string.Empty,
					reader.GetAttribute("race") ?? string.Empty);
				if (reader.IsEmptyElement)
				{
					titleTemplates.Add(currentTitleTemplate.ToSummary());
					currentTitleTemplate = null;
				}
				continue;
			}

			if (reader.Depth == 4
				&& currentTitleTemplate != null
				&& IsStatModifierElement(reader.LocalName)
				&& elementPath.TryGetValue(reader.Depth - 1, out var titleModifierParent)
				&& titleModifierParent == "modifiers")
			{
				currentTitleTemplate.AddModifier(
					new ItemStatModifier(
						reader.LocalName,
						reader.GetAttribute("name") ?? string.Empty,
						ReadIntAttribute(reader, "value"),
						ReadBoolAttribute(reader, "bonus")));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "recipe_template")
			{
				recipeTemplates.Add(new RecipeTemplateSummary(
					ReadRequiredIntAttribute(reader, "id"),
					ReadIntAttribute(reader, "nameid"),
					ReadIntAttribute(reader, "skillid"),
					reader.GetAttribute("race") ?? string.Empty,
					ReadIntAttribute(reader, "skillpoint"),
					ReadIntAttribute(reader, "dp"),
					ReadIntAttribute(reader, "autolearn"),
					ReadIntAttribute(reader, "productid"),
					ReadIntAttribute(reader, "quantity")));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "skill")
			{
				skillTree.Add(new SkillLearnSummary(
					reader.GetAttribute("classId") ?? string.Empty,
					ReadRequiredIntAttribute(reader, "skillId"),
					ReadNullableIntAttribute(reader, "skillLearn"),
					reader.GetAttribute("race") ?? "PC_ALL",
					ReadRequiredIntAttribute(reader, "minLevel"),
					ReadBoolAttribute(reader, "autolearn"),
					ReadIntAttribute(reader, "stigma"),
					SkillLevel: 0));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "elyos_spawn_location")
			{
				spawnLocationsByRace["ELYOS"] = ReadSpawnLocation(reader);
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "asmodian_spawn_location")
			{
				spawnLocationsByRace["ASMODIANS"] = ReadSpawnLocation(reader);
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "player_data")
			{
				currentPlayerCreationClass = reader.GetAttribute("class") ?? string.Empty;
				if (!string.IsNullOrEmpty(currentPlayerCreationClass))
					creationItemsByClass.TryAdd(currentPlayerCreationClass, []);
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "item" && currentPlayerCreationClass != null)
			{
				creationItemsByClass[currentPlayerCreationClass].Add(
					new StartingItem(
						ReadRequiredIntAttribute(reader, "id"),
						ReadLongAttribute(reader, "count")));
			}
		}

		if (experience.Count == 0)
			experience.AddRange(await LoadExperienceTableFromImportedFilesAsync(importedFiles, cancellationToken));

		return new StaticData(
			cacheFilePath,
			importedFiles,
			new ReadOnlyDictionary<string, int>(counts),
			topLevelElements.AsReadOnly(),
			worldMaps.AsReadOnly(),
			new PlayerExperienceTable(experience.AsReadOnly()),
			new ItemTemplateTable(itemTemplates.AsReadOnly(), learnableEmotionIds),
			new CosmeticItemTable(cosmeticItems.AsReadOnly()),
			new DecomposableItemTable(decomposableItems.AsReadOnly()),
			new AssemblyItemTable(assemblyItems.AsReadOnly()),
			new RideTable(rideInfos.AsReadOnly()),
			new ItemRandomBonusTable(itemRandomBonuses.AsReadOnly()),
			new ItemSetTable(itemSets.AsReadOnly()),
			new EnchantTable(enchantGroups.AsReadOnly()),
			new TemperingTable(temperingGroups.AsReadOnly()),
			new NpcTemplateTable(npcTemplates.AsReadOnly()),
			new NpcSpawnTable(npcSpawns.AsReadOnly()),
			new SkillTemplateTable(skillTemplates.AsReadOnly()),
			new TitleTemplateTable(titleTemplates.AsReadOnly()),
			new RecipeTemplateTable(recipeTemplates.AsReadOnly()),
			new HousingTemplateTable(
				housingAddresses
					.Select(
						address => address with
						{
							MinLevel = housingLandMinLevels.GetValueOrDefault(address.LandId),
							MaintenanceFee = housingLandMaintenanceFees.GetValueOrDefault(address.LandId),
							DefaultBuildingId = GetDefaultBuildingId(
								address.LandId,
								housingLandDefaultBuildingIds,
								housingLandFirstBuildingIds),
							DefaultBuildingType = housingBuildings
								.FirstOrDefault(
									building => building.BuildingId == GetDefaultBuildingId(
										address.LandId,
										housingLandDefaultBuildingIds,
										housingLandFirstBuildingIds))
								?.BuildingType ?? string.Empty,
						})
					.ToArray(),
				housingBuildings.AsReadOnly(),
				housingParts.AsReadOnly()),
			new HousingObjectTemplateTable(housingObjectTemplates.AsReadOnly()),
			new InstanceCooltimeTable(instanceCooltimes.AsReadOnly()),
			new PlayerInitialDataTable(
				creationItemsByClass.ToDictionary(
					pair => pair.Key,
					pair => new PlayerCreationData(pair.Key, pair.Value.AsReadOnly()),
					StringComparer.OrdinalIgnoreCase),
				spawnLocationsByRace),
			new SkillTreeTable(skillTree.AsReadOnly(), new SkillTemplateTable(skillTemplates.AsReadOnly())),
			validationTask);
	}

	private sealed class HousingBuildingBuilder
	{
		private readonly Dictionary<string, int> _defaultParts = new(StringComparer.OrdinalIgnoreCase);

		public HousingBuildingBuilder(int buildingId, string size, int houseTypeId, string buildingType, string partsMatch)
		{
			BuildingId = buildingId;
			Size = size;
			HouseTypeId = houseTypeId;
			BuildingType = buildingType;
			PartsMatch = partsMatch;
		}

		private int BuildingId { get; }

		private string Size { get; }

		private int HouseTypeId { get; }

		private string BuildingType { get; }

		private string PartsMatch { get; }

		public void SetDefaultPart(string partName, int partId)
		{
			if (partId <= 0)
				return;

			_defaultParts[partName] = partId;
		}

		public HousingBuildingSummary ToSummary()
		{
			// Java parity: model/templates/housing/Building.partsByType consumed by HouseRegistry default decor fallback.
			return new HousingBuildingSummary(
				BuildingId,
				Size,
				HouseTypeId,
				BuildingType,
				BuildDefaultDecorIds(),
				BuildDefaultPartIds(),
				PartsMatch);
		}

		private int[] BuildDefaultPartIds()
		{
			// Java parity: model/templates/housing/Building.getDefaultPartIds returns EnumMap values in PartType order without room repeats.
			return
			[
				.. new[]
				{
					GetPart("roof"),
					GetPart("outwall"),
					GetPart("frame"),
					GetPart("door"),
					GetPart("garden"),
					GetPart("fence"),
					GetPart("inwall"),
					GetPart("infloor"),
				}.Where(partId => partId > 0),
			];
		}

		private int[] BuildDefaultDecorIds()
		{
			return
			[
				GetPart("roof"),
				GetPart("outwall"),
				GetPart("frame"),
				GetPart("door"),
				GetPart("garden"),
				GetPart("fence"),
				GetPart("inwall"),
				GetPart("inwall"),
				GetPart("inwall"),
				GetPart("inwall"),
				GetPart("inwall"),
				GetPart("inwall"),
				GetPart("infloor"),
				GetPart("infloor"),
				GetPart("infloor"),
				GetPart("infloor"),
				GetPart("infloor"),
				GetPart("infloor"),
				GetPart("addon"),
			];
		}

		private int GetPart(string partName)
		{
			return _defaultParts.GetValueOrDefault(partName);
		}
	}

	private static bool IsHousingBuildingPartElement(string elementName)
	{
		// Java parity: model/templates/housing/Building.Parts fields serialized from housing/house_buildings.xml.
		return elementName is "roof" or "outwall" or "frame" or "door" or "garden" or "fence" or "inwall" or "infloor" or "addon";
	}

	private static IReadOnlySet<string> SplitHousePartTags(string? buildingTags)
	{
		// Java parity: model/templates/housing/HousePart.buildingTags JAXB Set<String> from whitespace-separated XML attribute values.
		if (string.IsNullOrWhiteSpace(buildingTags))
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		return buildingTags
			.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static bool IsHousingObjectTemplateElement(string elementName)
	{
		return elementName is "jukebox"
			or "moviejukebox"
			or "picture"
			or "postbox"
			or "chair"
			or "storage"
			or "npc"
			or "move_item"
			or "use_item"
			or "passive"
			or "emblem";
	}

	private static byte GetHousingObjectTypeId(string elementName)
	{
		// Java parity: concrete model/templates/housing PlaceableHouseObject.getTypeId implementations.
		return elementName switch
		{
			"use_item" => 1,
			"storage" => 2,
			"postbox" => 3,
			"chair" => 5,
			"jukebox" => 6,
			"npc" => 7,
			"emblem" => 11,
			_ => 0,
		};
	}

	private sealed class InstanceCooltimeBuilder
	{
		public InstanceCooltimeBuilder(int id, int worldId, string race)
		{
			Id = id;
			WorldId = worldId;
			Race = race;
		}

		private int Id { get; }

		private int WorldId { get; }

		private string Race { get; }

		public int MaxCount { get; set; }

		public InstanceCooltimeSummary ToSummary()
		{
			// Java parity: model/templates/InstanceCooltime fields consumed by SM_INSTANCE_INFO.
			return new InstanceCooltimeSummary(Id, WorldId, Race, MaxCount);
		}
	}

	private sealed class ItemRandomBonusBuilder
	{
		private readonly List<IReadOnlyList<ItemStatModifier>> _modifierGroups = [];
		private readonly List<double> _chances = [];
		private List<ItemStatModifier>? _currentModifierGroup;

		public ItemRandomBonusBuilder(string type, int setId)
		{
			Type = type;
			SetId = setId;
		}

		private string Type { get; }

		private int SetId { get; }

		public void AddModifierGroup(double chance)
		{
			_currentModifierGroup = [];
			_modifierGroups.Add(_currentModifierGroup);
			_chances.Add(chance);
		}

		public void AddModifier(ItemStatModifier modifier)
		{
			_currentModifierGroup ??= [];
			if (_modifierGroups.Count == 0)
			{
				_modifierGroups.Add(_currentModifierGroup);
				_chances.Add(0);
			}
			_currentModifierGroup.Add(modifier);
		}

		public ItemRandomBonusSummary ToSummary()
		{
			// Java parity: model/templates/item/bonuses/RandomBonusSet modifier groups are selected by 1-based rnd_bonus rows.
			return new ItemRandomBonusSummary(Type, SetId, _modifierGroups.ToArray(), _chances.ToArray());
		}
	}

	private sealed class SkillTemplateBuilder
	{
		private readonly List<SkillArmorMasteryEffectSummary> _armorMasteryEffects = [];
		private readonly List<SkillWeaponMasteryEffectSummary> _weaponMasteryEffects = [];
		private readonly List<SkillShieldMasteryEffectSummary> _shieldMasteryEffects = [];
		private readonly List<SkillWeaponDualEffectSummary> _weaponDualEffects = [];
		private List<SkillStatChange>? _currentMasteryChanges;

		public SkillTemplateBuilder(
			int skillId,
			string name,
			int nameId,
			int level,
			string group,
			string stack,
			string skillType,
			string skillSubType,
			int cooldownId,
			int cooldown)
		{
			SkillId = skillId;
			Name = name;
			NameId = nameId;
			Level = level;
			Group = group;
			Stack = stack;
			SkillType = skillType;
			SkillSubType = skillSubType;
			CooldownId = cooldownId;
			Cooldown = cooldown;
		}

		private int SkillId { get; }

		private string Name { get; }

		private int NameId { get; }

		private int Level { get; }

		private string Group { get; }

		private string Stack { get; }

		private string SkillType { get; }

		private string SkillSubType { get; }

		private int CooldownId { get; }

		private int Cooldown { get; }

		public string StigmaType { get; set; } = string.Empty;

		public void StartArmorMastery(string armorType, int value, int delta)
		{
			_currentMasteryChanges = [];
			_armorMasteryEffects.Add(new SkillArmorMasteryEffectSummary(
				armorType,
				value,
				delta,
				_currentMasteryChanges));
		}

		public void StartWeaponMastery(string weaponGroup)
		{
			_currentMasteryChanges = [];
			_weaponMasteryEffects.Add(new SkillWeaponMasteryEffectSummary(weaponGroup, _currentMasteryChanges));
		}

		public void StartShieldMastery()
		{
			_currentMasteryChanges = [];
			_shieldMasteryEffects.Add(new SkillShieldMasteryEffectSummary(_currentMasteryChanges));
		}

		public void AddCurrentMasteryChange(SkillStatChange change)
		{
			if (_currentMasteryChanges == null)
				return;

			_currentMasteryChanges.Add(change);
		}

		public void AddWeaponDual(SkillWeaponDualEffectSummary weaponDual)
		{
			_weaponDualEffects.Add(weaponDual);
		}

		public void EndMastery()
		{
			_currentMasteryChanges = null;
		}

		public SkillTemplateSummary ToSummary()
		{
			// Java parity: model/templates/skill/SkillTemplate with passive mastery effect metadata.
			return new SkillTemplateSummary(
				SkillId,
				Name,
				NameId,
				Level,
				Group,
				Stack,
				SkillType,
				SkillSubType,
				CooldownId,
				Cooldown,
				_armorMasteryEffects.ToArray(),
				_weaponMasteryEffects.ToArray(),
				_shieldMasteryEffects.ToArray(),
				_weaponDualEffects.ToArray(),
				StigmaType);
		}
	}

	private sealed class TitleTemplateBuilder
	{
		private readonly List<ItemStatModifier> _modifiers = [];

		public TitleTemplateBuilder(int titleId, int nameId, string description, string race)
		{
			TitleId = titleId;
			NameId = nameId;
			Description = description;
			Race = race;
		}

		private int TitleId { get; }

		private int NameId { get; }

		private string Description { get; }

		private string Race { get; }

		public void AddModifier(ItemStatModifier modifier)
		{
			_modifiers.Add(modifier);
		}

		public TitleTemplateSummary ToSummary()
		{
			// Java parity: model/templates/TitleTemplate modifiers.
			return new TitleTemplateSummary(
				TitleId,
				NameId,
				Description,
				Race,
				_modifiers.ToArray());
		}
	}

	private sealed class ItemSetBuilder
	{
		private readonly HashSet<int> _itemIds = [];
		private readonly List<ItemSetPartBonus> _partBonuses = [];
		private List<ItemStatModifier>? _currentModifiers;
		private int _currentPartBonusIndex = -1;
		private bool _isBuildingFullBonus;

		public ItemSetBuilder(int setId, string name)
		{
			SetId = setId;
			Name = name;
		}

		private int SetId { get; }

		private string Name { get; }

		private ItemSetFullBonus? FullBonus { get; set; }

		public void AddItemPart(int itemId)
		{
			_itemIds.Add(itemId);
		}

		public void StartPartBonus(int count)
		{
			_currentModifiers = [];
			_currentPartBonusIndex = _partBonuses.Count;
			_isBuildingFullBonus = false;
			_partBonuses.Add(new ItemSetPartBonus(count, _currentModifiers));
		}

		public void StartFullBonus()
		{
			_currentModifiers = [];
			_currentPartBonusIndex = -1;
			_isBuildingFullBonus = true;
			FullBonus = new ItemSetFullBonus(_itemIds.Count, _currentModifiers);
		}

		public void AddModifier(ItemStatModifier modifier)
		{
			_currentModifiers ??= [];
			_currentModifiers.Add(modifier);
			if (_isBuildingFullBonus)
				FullBonus = new ItemSetFullBonus(_itemIds.Count, _currentModifiers);
			else if (_currentPartBonusIndex >= 0)
				_partBonuses[_currentPartBonusIndex] = _partBonuses[_currentPartBonusIndex] with { Modifiers = _currentModifiers };
		}

		public void EndBonus()
		{
			_currentModifiers = null;
			_currentPartBonusIndex = -1;
			_isBuildingFullBonus = false;
		}

		public ItemSetSummary ToSummary()
		{
			// Java parity: model/templates/itemset/ItemSetTemplate.afterUnmarshal sets full-bonus count to itempart size.
			return new ItemSetSummary(
				SetId,
				Name,
				_itemIds.ToHashSet(),
				_partBonuses.AsReadOnly(),
				FullBonus);
		}
	}

	private sealed class EnchantGroupBuilder
	{
		private readonly List<EnchantLevelSummary> _levels = [];
		private List<EnchantStatSummary>? _currentStats;
		private int _currentLevelIndex = -1;

		public EnchantGroupBuilder(string itemGroup)
		{
			ItemGroup = itemGroup;
		}

		private string ItemGroup { get; }

		public void StartLevel(int level)
		{
			_currentStats = [];
			_currentLevelIndex = _levels.Count;
			_levels.Add(new EnchantLevelSummary(level, _currentStats));
		}

		public void AddStat(EnchantStatSummary stat)
		{
			_currentStats ??= [];
			_currentStats.Add(stat);
			if (_currentLevelIndex >= 0)
				_levels[_currentLevelIndex] = _levels[_currentLevelIndex] with { Stats = _currentStats };
		}

		public void EndLevel()
		{
			_currentStats = null;
			_currentLevelIndex = -1;
		}

		public EnchantGroupSummary ToSummary()
		{
			// Java parity: model/enchants/EnchantList item_group mapped by dataholders/EnchantData.afterUnmarshal.
			return new EnchantGroupSummary(ItemGroup, _levels.AsReadOnly());
		}
	}

	private sealed class TemperingGroupBuilder
	{
		private readonly List<TemperingLevelSummary> _levels = [];
		private List<TemperingStatSummary>? _currentStats;
		private int _currentLevelIndex = -1;

		public TemperingGroupBuilder(string itemGroup)
		{
			ItemGroup = itemGroup;
		}

		private string ItemGroup { get; }

		public void StartLevel(int level)
		{
			_currentStats = [];
			_currentLevelIndex = _levels.Count;
			_levels.Add(new TemperingLevelSummary(level, _currentStats));
		}

		public void AddStat(TemperingStatSummary stat)
		{
			_currentStats ??= [];
			_currentStats.Add(stat);
			if (_currentLevelIndex >= 0)
				_levels[_currentLevelIndex] = _levels[_currentLevelIndex] with { Stats = _currentStats };
		}

		public void EndLevel()
		{
			_currentStats = null;
			_currentLevelIndex = -1;
		}

		public TemperingGroupSummary ToSummary()
		{
			// Java parity: model/enchants/TemperingList item_group mapped by dataholders/TemperingData.afterUnmarshal.
			return new TemperingGroupSummary(ItemGroup, _levels.AsReadOnly());
		}
	}

	private sealed class ItemTemplateBuilder
	{
		public ItemTemplateBuilder(
			int templateId,
			string name,
			int descriptionId,
			int mask,
			int level,
			string itemGroup,
			string itemType,
			string quality,
			string race,
			string attackType,
			int maxStackCount,
			long price,
			long validEquipmentSlots,
			int manastoneSlots,
			int specialManastoneSlots,
			IReadOnlyDictionary<string, int> requiredLevels,
			IReadOnlyDictionary<string, int> maxLevelRestrictions,
			int activationCount,
			int expireTimeMinutes,
			int enchantType,
			int maxEnchantLevel,
			int maxEnchantBonus,
			bool canExceedEnchant,
			string exceedEnchantSkill,
			int optionSlotBonus,
			int randomBonusId,
			int maxTuneCount,
			string enchantName,
			string temperingName,
			int weaponBoost)
		{
			TemplateId = templateId;
			Name = name;
			DescriptionId = descriptionId;
			Mask = mask;
			Level = level;
			ItemGroup = itemGroup;
			ItemType = itemType;
			Quality = quality;
			Race = race;
			AttackType = attackType;
			MaxStackCount = maxStackCount;
			Price = price;
			ValidEquipmentSlots = validEquipmentSlots;
			ManastoneSlots = manastoneSlots;
			SpecialManastoneSlots = specialManastoneSlots;
			RequiredLevels = requiredLevels;
			MaxLevelRestrictions = maxLevelRestrictions;
			ClassRestrictions = requiredLevels.Keys.ToHashSet(StringComparer.Ordinal);
			ActivationCount = activationCount;
			ExpireTimeMinutes = expireTimeMinutes;
			EnchantType = enchantType;
			MaxEnchantLevel = maxEnchantLevel;
			CanExceedEnchant = canExceedEnchant;
			ExceedEnchantSkill = exceedEnchantSkill;
			StatBonusSetId = randomBonusId;
			EnchantName = enchantName;
			TemperingName = temperingName;
			WeaponBoost = weaponBoost;
			MaxTuneCount = CalculateMaxTuneCount(validEquipmentSlots, maxTuneCount, maxEnchantBonus, optionSlotBonus, randomBonusId);
			CanTune = MaxTuneCount != 0;
		}

		private int TemplateId { get; }

		private string Name { get; }

		private int DescriptionId { get; }

		private int Mask { get; }

		private int Level { get; }

		private string ItemGroup { get; }

		private string ItemType { get; }

		private string Quality { get; }

		private string Race { get; }

		private string AttackType { get; }

		private int WeaponBoost { get; }

		private int MaxStackCount { get; }

		private long Price { get; }

		private long ValidEquipmentSlots { get; }

		private int ManastoneSlots { get; }

		private int SpecialManastoneSlots { get; }

		private IReadOnlySet<string> ClassRestrictions { get; }

		private IReadOnlyDictionary<string, int> RequiredLevels { get; }

		private IReadOnlyDictionary<string, int> MaxLevelRestrictions { get; }

		private int ActivationCount { get; }

		private int ExpireTimeMinutes { get; }

		private int EnchantType { get; }

		private int MaxEnchantLevel { get; }

		private bool CanExceedEnchant { get; }

		private string ExceedEnchantSkill { get; }

		private int StatBonusSetId { get; }

		private string EnchantName { get; }

		private string TemperingName { get; }

		private bool CanTune { get; }

		private int MaxTuneCount { get; }

		private int CurrentModifierIndex { get; set; } = -1;

		public ItemWeaponStats? WeaponStats { get; set; }

		public ItemGodstoneInfo? GodstoneInfo { get; set; }

		public ItemImprovement? Improvement { get; set; }

		public ItemIdianInfo? IdianInfo { get; set; }

		public ItemStigmaInfo? StigmaInfo { get; set; }

		public List<ItemStatModifier> Modifiers { get; } = [];

		public int DispositionItemId { get; set; }

		public int DispositionItemCount { get; set; }

		public int ExtraInventoryId { get; set; } = -1;

		public int CraftLearnRecipeId { get; set; }

		public ItemSkillLearnActionInfo? SkillLearnAction { get; set; }

		public ItemExpandInventoryActionInfo? ExpandInventoryAction { get; set; }

		public ItemExpExtractActionInfo? ExpExtractAction { get; set; }

		public bool HasExtractAction { get; set; }

		public ItemApExtractActionInfo? ApExtractAction { get; set; }

		public int RequiredAbyssPoints { get; set; }

		public ItemDyeActionInfo? DyeAction { get; set; }

		public ItemAnimationActionInfo? AnimationAction { get; set; }

		public ItemRemodelActionInfo? RemodelAction { get; set; }

		public bool HasDecomposeAction { get; set; }

		public bool HasCompositionAction { get; set; }

		public int AssemblyItemId { get; set; }

		public string CosmeticActionName { get; set; } = string.Empty;

		public int ConditioningMaxLevel { get; set; }

		public int PolishSetId { get; set; }

		public int ChargeActionMaxLevel { get; set; }

		public ItemEnchantActionInfo? EnchantAction { get; set; }

		public int RideNpcId { get; set; }

		public int EmotionLearnId { get; set; }

		public int EmotionLearnMinutes { get; set; }

		public bool HasEmotionLearnAction { get; set; }

		public int TitleAddTitleId { get; set; }

		public int TitleAddMinutes { get; set; }

		public bool HasTitleAddAction { get; set; }

		public bool HasTitleAddMinutes { get; set; }

		public int RecommendRank { get; set; }

		public string GenderPermitted { get; set; } = string.Empty;

		public int MinRank { get; set; } = 1;

		public int MaxRank { get; set; } = 18;

		public int UseDelayId { get; set; }

		public int UseDelayMillis { get; set; }

		public bool HasHouseObjectAction { get; set; }

		public int HouseObjectTemplateId { get; set; }

		public bool HasHouseDecorateAction { get; set; }

		public int HouseDecorateTemplateId { get; set; }

		public void AddModifier(ItemStatModifier modifier)
		{
			Modifiers.Add(modifier);
			CurrentModifierIndex = Modifiers.Count - 1;
		}

		public void SetCurrentModifierChargeCondition(int chargeCondition)
		{
			if (CurrentModifierIndex < 0)
				return;

			Modifiers[CurrentModifierIndex] = Modifiers[CurrentModifierIndex] with { ChargeCondition = chargeCondition };
		}

		public void EndModifier()
		{
			CurrentModifierIndex = -1;
		}

		public ItemTemplateSummary ToSummary()
		{
			// Java parity: model/templates/item/ItemTemplate fields consumed by item creation, broker/mail checks, and item blobs.
			return new ItemTemplateSummary(
				TemplateId,
				Name,
				DescriptionId,
				Mask,
				Level,
				ItemGroup,
				ItemType,
				Quality,
				Race,
				MaxStackCount,
				Price,
				ValidEquipmentSlots,
				DispositionItemId,
				DispositionItemCount,
				ClassRestrictions,
				CraftLearnRecipeId,
				SkillLearnAction,
				ActivationCount,
				ExpireTimeMinutes,
				EnchantType,
				CanTune,
				MaxTuneCount,
				ConditioningMaxLevel,
				AttackType,
				WeaponStats,
				Modifiers.AsReadOnly(),
				StatBonusSetId,
				EnchantName,
				TemperingName,
				PolishSetId,
				ChargeActionMaxLevel,
				GodstoneInfo,
				Improvement,
				RecommendRank,
				IdianInfo,
				StigmaInfo,
				RequiredLevels,
				MaxLevelRestrictions,
				GenderPermitted,
				MinRank,
				MaxRank,
				MaxEnchantLevel,
				CanExceedEnchant,
				ManastoneSlots,
				SpecialManastoneSlots,
				ExceedEnchantSkill,
				EnchantAction,
				UseDelayId,
				UseDelayMillis,
				RideNpcId,
				EmotionLearnId,
				EmotionLearnMinutes,
				HasEmotionLearnAction,
				TitleAddTitleId,
				TitleAddMinutes,
				HasTitleAddAction,
				HasTitleAddMinutes,
				ExpandInventoryAction,
				DyeAction,
				AnimationAction,
				RemodelAction,
				CosmeticActionName,
				HasDecomposeAction,
				HasCompositionAction,
				ExtraInventoryId,
				AssemblyItemId,
				HasExtractAction,
				ApExtractAction,
				ExpExtractAction,
				RequiredAbyssPoints,
				HasHouseObjectAction,
				HouseObjectTemplateId,
				HasHouseDecorateAction,
				HouseDecorateTemplateId,
				WeaponBoost);
		}

		private static int CalculateMaxTuneCount(
			long validEquipmentSlots,
			int maxTuneCount,
			int maxEnchantBonus,
			int optionSlotBonus,
			int randomBonusId)
		{
			// Java parity: model/templates/item/ItemTemplate.afterUnmarshal + getMaxTuneCount.
			if (validEquipmentSlots == 0)
				return 0;

			if (maxTuneCount == -1 && maxEnchantBonus == 0 && optionSlotBonus == 0 && randomBonusId == 0)
				return 0;

			return maxTuneCount;
		}
	}

	private sealed class CosmeticItemBuilder
	{
		public CosmeticItemBuilder(string type, string cosmeticName, int id, string race, string genderPermitted)
		{
			Type = type;
			CosmeticName = cosmeticName;
			Id = id;
			Race = race;
			GenderPermitted = genderPermitted;
		}

		private string Type { get; }

		private string CosmeticName { get; }

		private int Id { get; }

		private string Race { get; }

		private string GenderPermitted { get; }

		private float Scale { get; set; }

		private int HairType { get; set; }

		private int FaceType { get; set; }

		private int HairColor { get; set; }

		private int LipColor { get; set; }

		private int EyeColor { get; set; }

		private int SkinColor { get; set; }

		private bool HasPreset { get; set; }

		public void SetPresetValue(string name, string value)
		{
			// Java parity: model/templates/cosmeticitems/CosmeticItemTemplate.Preset JAXB fields.
			HasPreset = true;
			switch (name)
			{
				case "scale":
					Scale = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScale) ? parsedScale : 0f;
					break;
				case "hair_type":
					HairType = ParseInt(value);
					break;
				case "face_type":
					FaceType = ParseInt(value);
					break;
				case "hair_color":
					HairColor = ParseInt(value);
					break;
				case "lip_color":
					LipColor = ParseInt(value);
					break;
				case "eye_color":
					EyeColor = ParseInt(value);
					break;
				case "skin_color":
					SkinColor = ParseInt(value);
					break;
			}
		}

		public CosmeticItemSummary ToSummary()
		{
			return new CosmeticItemSummary(
				Type,
				CosmeticName,
				Id,
				Race,
				GenderPermitted,
				HasPreset
					? new CosmeticPresetSummary(Scale, HairType, FaceType, HairColor, LipColor, EyeColor, SkinColor)
					: null);
		}

		private static int ParseInt(string value)
		{
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
		}
	}

	private sealed class DecomposableItemBuilder
	{
		private readonly List<ExtractedItemsCollectionBuilder> _collections = [];
		private ExtractedItemsCollectionBuilder? _currentCollection;

		public DecomposableItemBuilder(int itemId, bool isSelectable)
		{
			ItemId = itemId;
			IsSelectable = isSelectable;
		}

		private int ItemId { get; }

		private bool IsSelectable { get; }

		public void StartCollection(float chance, int minLevel, int maxLevel)
		{
			_currentCollection = new ExtractedItemsCollectionBuilder(chance, minLevel, maxLevel);
			_collections.Add(_currentCollection);
		}

		public void AddItem(ResultedItemSummary item)
		{
			_currentCollection?.Items.Add(item);
		}

		public void AddRandomItem(RandomItemSummary item)
		{
			_currentCollection?.RandomItems.Add(item);
		}

		public void EndCollection()
		{
			_currentCollection = null;
		}

		public DecomposableItemSummary ToSummary()
		{
			// Java parity: dataholders/DecomposableItemsData maps normal groups separately from selectable rewards.
			return new DecomposableItemSummary(
				ItemId,
				IsSelectable,
				_collections.Select(collection => collection.ToSummary()).ToArray());
		}
	}

	private sealed class ExtractedItemsCollectionBuilder
	{
		public ExtractedItemsCollectionBuilder(float chance, int minLevel, int maxLevel)
		{
			Chance = chance;
			MinLevel = minLevel;
			MaxLevel = maxLevel;
		}

		private float Chance { get; }

		private int MinLevel { get; }

		private int MaxLevel { get; }

		public List<ResultedItemSummary> Items { get; } = [];

		public List<RandomItemSummary> RandomItems { get; } = [];

		public ExtractedItemsCollectionSummary ToSummary()
		{
			// Java parity: model/templates/rewards/ResultedItemsCollection fixed items plus random_item entries.
			return new ExtractedItemsCollectionSummary(
				Chance,
				MinLevel,
				MaxLevel,
				Items.ToArray(),
				RandomItems.ToArray());
		}
	}

	private sealed class NpcTemplateBuilder
	{
		public NpcTemplateBuilder(
			int templateId,
			string name,
			int nameId,
			int level,
			string rank,
			string rating,
			string race,
			string tribe,
			string type,
			int titleId,
			float height,
			int attackSpeed)
		{
			TemplateId = templateId;
			Name = name;
			NameId = nameId;
			Level = level;
			Rank = rank;
			Rating = rating;
			Race = race;
			Tribe = tribe;
			Type = type;
			TitleId = titleId;
			Height = height;
			AttackSpeed = attackSpeed;
		}

		private int TemplateId { get; }

		private string Name { get; }

		private int NameId { get; }

		private int Level { get; }

		private string Rank { get; }

		private string Rating { get; }

		private string Race { get; }

		private string Tribe { get; }

		private string Type { get; }

		private int TitleId { get; }

		private float Height { get; }

		private int AttackSpeed { get; }

		public int MaxHp { get; set; }

		public float RunSpeed { get; set; }

		public float BoundRadiusFront { get; set; }

		public float BoundRadiusSide { get; set; }

		public int TalkDistance { get; set; } = 2;

		public List<int> FunctionDialogIds { get; } = [];

		public NpcTemplateSummary ToSummary()
		{
			// Java parity: model/templates/npc/NpcTemplate fields consumed by SM_NPC_INFO.
			return new NpcTemplateSummary(
				TemplateId,
				Name,
				NameId,
				Level,
				Rank,
				Rating,
				Race,
				Tribe,
				Type,
				TitleId,
				Height,
				AttackSpeed,
				MaxHp,
				RunSpeed,
				Math.Max(BoundRadiusFront, BoundRadiusSide),
				TalkDistance,
				FunctionDialogIds.Count == 0 ? null : FunctionDialogIds.ToArray());
		}
	}

	private sealed class NpcSpawnBuilder
	{
		public NpcSpawnBuilder(
			int mapId,
			int npcId,
			int respawnSeconds,
			int poolSize,
			string handler,
			bool custom)
		{
			MapId = mapId;
			NpcId = npcId;
			RespawnSeconds = respawnSeconds;
			PoolSize = poolSize;
			Handler = handler;
			Custom = custom;
		}

		private int MapId { get; }

		private int NpcId { get; }

		private int RespawnSeconds { get; }

		private int PoolSize { get; }

		private string Handler { get; }

		private bool Custom { get; }

		public NpcSpawnSummary ToSummary(XmlReader reader)
		{
			// Java parity: model/templates/spawns/SpawnTemplate inherits group npc/respawn/handler metadata.
			return new NpcSpawnSummary(
				MapId,
				NpcId,
				ReadFloatAttribute(reader, "x"),
				ReadFloatAttribute(reader, "y"),
				ReadFloatAttribute(reader, "z"),
				(byte)ReadOptionalIntAttribute(reader, "h", 0),
				RespawnSeconds,
				PoolSize,
				Handler,
				ReadIntAttribute(reader, "static_id"),
				reader.GetAttribute("walker_id") ?? string.Empty,
				ReadIntAttribute(reader, "walker_index"),
				Custom);
		}
	}

	private static PlayerSpawnLocation ReadSpawnLocation(XmlReader reader)
	{
		// Java parity: dataholders/PlayerInitialData.LocationData.
		return new PlayerSpawnLocation(
			ReadRequiredIntAttribute(reader, "map_id"),
			ReadFloatAttribute(reader, "x"),
			ReadFloatAttribute(reader, "y"),
			ReadFloatAttribute(reader, "z"),
			ReadIntAttribute(reader, "heading"));
	}

	private static int ReadRequiredIntAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName);
		if (!int.TryParse(value, out var parsed))
			throw new FormatException($"Element <{reader.LocalName}> is missing required integer attribute '{attributeName}'.");

		return parsed;
	}

	private static int ReadIntAttribute(XmlReader reader, string attributeName)
	{
		return int.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : 0;
	}

	private static int ReadOptionalIntAttribute(XmlReader reader, string attributeName, int defaultValue)
	{
		return int.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : defaultValue;
	}

	private static float ReadOptionalFloatAttribute(XmlReader reader, string attributeName, float defaultValue)
	{
		return float.TryParse(reader.GetAttribute(attributeName), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: defaultValue;
	}

	private static int? ReadNullableIntAttribute(XmlReader reader, string attributeName)
	{
		return int.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : null;
	}

	private static float? ReadNullableFloatAttribute(XmlReader reader, string attributeName)
	{
		return float.TryParse(reader.GetAttribute(attributeName), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: null;
	}

	private static bool ReadBoolAttribute(XmlReader reader, string attributeName)
	{
		return bool.TryParse(reader.GetAttribute(attributeName), out var parsed) && parsed;
	}

	private static bool IsStatModifierElement(string elementName)
	{
		return elementName is "add" or "sub" or "rate" or "set" or "abs";
	}

	private static long ReadLongAttribute(XmlReader reader, string attributeName)
	{
		return long.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : 0;
	}

	private static int GetHouseTypeId(string size)
	{
		// Java parity: model/templates/housing/HouseType enum ids.
		return size.ToUpperInvariant() switch
		{
			"STUDIO" => 0,
			"HOUSE" => 1,
			"MANSION" => 2,
			"ESTATE" => 3,
			"PALACE" => 4,
			_ => 0,
		};
	}

	private static int GetDefaultBuildingId(
		int landId,
		IReadOnlyDictionary<int, int> defaultBuildingIds,
		IReadOnlyDictionary<int, int> firstBuildingIds)
	{
		// Java parity: model/templates/housing/HousingLand.getDefaultBuilding defaults to the first listed building.
		return defaultBuildingIds.GetValueOrDefault(landId, firstBuildingIds.GetValueOrDefault(landId));
	}

	private static IReadOnlyDictionary<string, int> ReadLevelRestrictions(string? restrict)
	{
		// Java parity: model/templates/item/ItemTemplate.levelRestrictions ordinal order from PlayerClass.
		if (string.IsNullOrWhiteSpace(restrict))
			return new Dictionary<string, int>(StringComparer.Ordinal);

		var restrictions = restrict.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var levelRestrictions = new Dictionary<string, int>(StringComparer.Ordinal);
		for (var i = 0; i < restrictions.Length && i < PlayerClasses.Length; i++)
		{
			if (int.TryParse(restrictions[i], out var requiredLevel) && requiredLevel > 0)
				levelRestrictions[PlayerClasses[i]] = requiredLevel;
		}

		return levelRestrictions;
	}

	private static IReadOnlySet<string> ReadPlayerClasses(string? playerClasses)
	{
		// Java parity: model/templates/rewards/ResultedItem.player_classes.
		return string.IsNullOrWhiteSpace(playerClasses)
			? new HashSet<string>(StringComparer.Ordinal)
			: playerClasses.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
	}

	private static IReadOnlyList<int> ReadIntListAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName);
		if (string.IsNullOrWhiteSpace(value))
			return Array.Empty<int>();

		return value
			.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Select(part => int.Parse(part, CultureInfo.InvariantCulture))
			.ToArray();
	}

	private static float ReadFloatAttribute(XmlReader reader, string attributeName)
	{
		return float.TryParse(reader.GetAttribute(attributeName), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
	}

	private static long GetItemGroupSlots(string? itemGroup)
	{
		// Java parity: model/templates/item/ItemTemplate.item_group -> model/items/ItemSlot mask.
		return itemGroup?.ToUpperInvariant() switch
		{
			"NOWEAPON" or "SWORD" or "GREATSWORD" or "DAGGER" or "MACE" or "ORB" or "SPELLBOOK" or "POLEARM" or "STAFF" or "BOW"
				or "HARP" or "GUN" or "CANNON" or "KEYBLADE" or "TOOLRODS" or "TOOLPICKS" => MainHand | SubHand,
			"NPC_MACE" or "TOOLHOES" => MainHand,
			"SHIELD" or "CL_SHIELD" => SubHand,
			"TORSO" or "RB_TORSO" or "CL_TORSO" or "LT_TORSO" or "CH_TORSO" or "PL_TORSO" => Torso,
			"GLOVE" or "RB_GLOVE" or "CL_GLOVE" or "LT_GLOVE" or "CH_GLOVE" or "PL_GLOVE" => Gloves,
			"SHOULDER" or "RB_SHOULDER" or "CL_SHOULDER" or "LT_SHOULDER" or "CH_SHOULDER" or "PL_SHOULDER" => Shoulder,
			"PANTS" or "RB_PANTS" or "CL_PANTS" or "LT_PANTS" or "CH_PANTS" or "PL_PANTS" => Pants,
			"SHOES" or "RB_SHOES" or "CL_SHOES" or "LT_SHOES" or "CH_SHOES" or "PL_SHOES" => Boots,
			"EARRING" => EarringsLeft | EarringsRight,
			"RING" => RingLeft | RingRight,
			"NECKLACE" => Necklace,
			"BELT" => Waist,
			"WING" => Wings,
			"PLUME" => Plume,
			"HEAD" or "LT_HEADS" or "CL_HEADS" => Helmet,
			"CL_MULTISLOT" => Torso | Pants,
			"POWER_SHARDS" => PowerShardLeft | PowerShardRight,
			"STIGMA" => RegularStigmas | AdvancedStigmas,
			_ => 0,
		};
	}

	private const long MainHand = 1L;
	private const long SubHand = 1L << 1;
	private const long Helmet = 1L << 2;
	private const long Torso = 1L << 3;
	private const long Gloves = 1L << 4;
	private const long Boots = 1L << 5;
	private const long EarringsLeft = 1L << 6;
	private const long EarringsRight = 1L << 7;
	private const long RingLeft = 1L << 8;
	private const long RingRight = 1L << 9;
	private const long Necklace = 1L << 10;
	private const long Shoulder = 1L << 11;
	private const long Pants = 1L << 12;
	private const long PowerShardRight = 1L << 13;
	private const long PowerShardLeft = 1L << 14;
	private const long Wings = 1L << 15;
	private const long Waist = 1L << 16;
	private const long Plume = 1L << 19;
	private const long RegularStigmas = (1L << 30) | (1L << 31) | (1L << 32);
	private const long AdvancedStigmas = (1L << 33) | (1L << 34) | (1L << 35);
	private static readonly string[] PlayerClasses =
	[
		"WARRIOR",
		"GLADIATOR",
		"TEMPLAR",
		"SCOUT",
		"ASSASSIN",
		"RANGER",
		"MAGE",
		"SORCERER",
		"SPIRIT_MASTER",
		"PRIEST",
		"CLERIC",
		"CHANTER",
		"ENGINEER",
		"RIDER",
		"GUNNER",
		"ARTIST",
		"BARD",
	];

	private static async Task<IReadOnlyList<long>> LoadExperienceTableFromImportedFilesAsync(
		IReadOnlyList<string> importedFiles,
		CancellationToken cancellationToken)
	{
		// Java parity: data/static_data/player_experience_table.xml fallback when merged text nodes are absent.
		var experienceFile = importedFiles.FirstOrDefault(file => Path.GetFileName(file).Equals("player_experience_table.xml", StringComparison.OrdinalIgnoreCase));
		if (experienceFile == null)
			return Array.Empty<long>();

		var experience = new List<long>();
		var settings = new XmlReaderSettings
		{
			Async = true,
			DtdProcessing = DtdProcessing.Prohibit,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
		};

		using var reader = XmlReader.Create(experienceFile, settings);
		while (await reader.ReadAsync())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "exp")
				continue;

			var value = await ReadElementTextAsync(reader, cancellationToken);
			if (long.TryParse(value, out var parsedExperience))
				experience.Add(parsedExperience);
		}

		return experience;
	}

	private static async Task<string> ReadElementTextAsync(XmlReader reader, CancellationToken cancellationToken)
	{
		if (reader.IsEmptyElement)
			return string.Empty;

		var depth = reader.Depth;
		while (await reader.ReadAsync())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
				return reader.Value;
			if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
				return string.Empty;
		}

		return string.Empty;
	}
}
