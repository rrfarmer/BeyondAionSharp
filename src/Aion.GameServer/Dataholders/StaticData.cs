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
		NpcTemplateTable npcTemplates,
		SkillTemplateTable skillTemplates,
		RecipeTemplateTable recipeTemplates,
		HousingTemplateTable housingTemplates,
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
		NpcTemplates = npcTemplates;
		SkillTemplates = skillTemplates;
		RecipeTemplates = recipeTemplates;
		HousingTemplates = housingTemplates;
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

	public NpcTemplateTable NpcTemplates { get; }

	public SkillTemplateTable SkillTemplates { get; }

	public RecipeTemplateTable RecipeTemplates { get; }

	public HousingTemplateTable HousingTemplates { get; }

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
		var npcTemplates = new List<NpcTemplateSummary>();
		var skillTemplates = new List<SkillTemplateSummary>();
		var recipeTemplates = new List<RecipeTemplateSummary>();
		var housingAddresses = new List<HousingAddressSummary>();
		var housingLandMinLevels = new Dictionary<int, int>();
		var housingBuildings = new List<HousingBuildingSummary>();
		var instanceCooltimes = new List<InstanceCooltimeSummary>();
		var skillTree = new List<SkillLearnSummary>();
		var creationItemsByClass = new Dictionary<string, List<StartingItem>>(StringComparer.OrdinalIgnoreCase);
		var spawnLocationsByRace = new Dictionary<string, PlayerSpawnLocation>(StringComparer.OrdinalIgnoreCase);
		string? currentPlayerCreationClass = null;
		InstanceCooltimeBuilder? currentInstanceCooltime = null;
		ItemTemplateBuilder? currentItemTemplate = null;
		NpcTemplateBuilder? currentNpcTemplate = null;
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

				if (reader.Depth == 2 && reader.LocalName == "npc_template" && currentNpcTemplate != null)
				{
					npcTemplates.Add(currentNpcTemplate.ToSummary());
					currentNpcTemplate = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "player_data")
					currentPlayerCreationClass = null;
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
				// Java parity: model/templates/housing/HouseAddress links an address back to its HousingLand.
				housingAddresses.Add(
					new HousingAddressSummary(
						ReadRequiredIntAttribute(reader, "id"),
						currentHousingLandId,
						currentHousingManagerNpcId));
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "sale" && currentHousingLandId != 0)
			{
				// Java parity: model/templates/housing/Sale.level used as fallback minimum bid level.
				housingLandMinLevels[currentHousingLandId] = ReadIntAttribute(reader, "level");
				continue;
			}

			if (reader.Depth == 2
				&& reader.LocalName == "building"
				&& elementPath.TryGetValue(1, out var rootElement)
				&& rootElement == "buildings")
			{
				// Java parity: model/templates/housing/Building.size -> HouseType id written in SM_HOUSE_BIDS.
				var size = reader.GetAttribute("size") ?? string.Empty;
				housingBuildings.Add(
					new HousingBuildingSummary(
						ReadRequiredIntAttribute(reader, "id"),
						size,
						GetHouseTypeId(size)));
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
					ReadOptionalIntAttribute(reader, "max_stack_count", 1),
					ReadLongAttribute(reader, "price"),
					GetItemGroupSlots(reader.GetAttribute("item_group")),
					ReadClassRestrictions(reader.GetAttribute("restrict")),
					ReadIntAttribute(reader, "activate_count"),
					ReadIntAttribute(reader, "expire_time"),
					ReadIntAttribute(reader, "enchant_type"),
					ReadIntAttribute(reader, "max_enchant_bonus"),
					ReadIntAttribute(reader, "option_slot_bonus"),
					ReadIntAttribute(reader, "rnd_bonus"),
					ReadOptionalIntAttribute(reader, "rnd_count", -1));
				if (reader.IsEmptyElement)
				{
					itemTemplates.Add(currentItemTemplate.ToSummary());
					currentItemTemplate = null;
				}

				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "disposition" && currentItemTemplate != null)
			{
				currentItemTemplate.DispositionItemId = ReadIntAttribute(reader, "id");
				currentItemTemplate.DispositionItemCount = ReadIntAttribute(reader, "count");
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "improve" && currentItemTemplate != null)
			{
				currentItemTemplate.ConditioningMaxLevel = ReadIntAttribute(reader, "level");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "craftlearn" && currentItemTemplate != null)
			{
				currentItemTemplate.CraftLearnRecipeId = ReadIntAttribute(reader, "recipeid");
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

			if (reader.Depth == 2 && reader.LocalName == "skill_template")
			{
				skillTemplates.Add(new SkillTemplateSummary(
					ReadRequiredIntAttribute(reader, "skill_id"),
					reader.GetAttribute("name") ?? string.Empty,
					ReadIntAttribute(reader, "nameId"),
					ReadIntAttribute(reader, "lvl"),
					reader.GetAttribute("group") ?? string.Empty,
					reader.GetAttribute("stack") ?? string.Empty,
					reader.GetAttribute("skilltype") ?? string.Empty,
					reader.GetAttribute("skillsubtype") ?? string.Empty,
					ReadIntAttribute(reader, "cooldownId"),
					ReadIntAttribute(reader, "cooldown")));
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
			new ItemTemplateTable(itemTemplates.AsReadOnly()),
			new NpcTemplateTable(npcTemplates.AsReadOnly()),
			new SkillTemplateTable(skillTemplates.AsReadOnly()),
			new RecipeTemplateTable(recipeTemplates.AsReadOnly()),
			new HousingTemplateTable(
				housingAddresses
					.Select(address => address with { MinLevel = housingLandMinLevels.GetValueOrDefault(address.LandId) })
					.ToArray(),
				housingBuildings.AsReadOnly()),
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
			int maxStackCount,
			long price,
			long validEquipmentSlots,
			IReadOnlySet<string> classRestrictions,
			int activationCount,
			int expireTimeMinutes,
			int enchantType,
			int maxEnchantBonus,
			int optionSlotBonus,
			int randomBonusId,
			int maxTuneCount)
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
			MaxStackCount = maxStackCount;
			Price = price;
			ValidEquipmentSlots = validEquipmentSlots;
			ClassRestrictions = classRestrictions;
			ActivationCount = activationCount;
			ExpireTimeMinutes = expireTimeMinutes;
			EnchantType = enchantType;
			CanTune = CalculateCanTune(validEquipmentSlots, maxTuneCount, maxEnchantBonus, optionSlotBonus, randomBonusId);
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

		private int MaxStackCount { get; }

		private long Price { get; }

		private long ValidEquipmentSlots { get; }

		private IReadOnlySet<string> ClassRestrictions { get; }

		private int ActivationCount { get; }

		private int ExpireTimeMinutes { get; }

		private int EnchantType { get; }

		private bool CanTune { get; }

		public int DispositionItemId { get; set; }

		public int DispositionItemCount { get; set; }

		public int CraftLearnRecipeId { get; set; }

		public int ConditioningMaxLevel { get; set; }

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
				ActivationCount,
				ExpireTimeMinutes,
				EnchantType,
				CanTune,
				ConditioningMaxLevel);
		}

		private static bool CalculateCanTune(
			long validEquipmentSlots,
			int maxTuneCount,
			int maxEnchantBonus,
			int optionSlotBonus,
			int randomBonusId)
		{
			// Java parity: model/templates/item/ItemTemplate.afterUnmarshal + canTune.
			if (validEquipmentSlots == 0)
				return false;

			if (maxTuneCount != -1)
				return maxTuneCount != 0;

			return maxEnchantBonus != 0 || optionSlotBonus != 0 || randomBonusId != 0;
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
				Math.Max(BoundRadiusFront, BoundRadiusSide));
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

	private static int? ReadNullableIntAttribute(XmlReader reader, string attributeName)
	{
		return int.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : null;
	}

	private static bool ReadBoolAttribute(XmlReader reader, string attributeName)
	{
		return bool.TryParse(reader.GetAttribute(attributeName), out var parsed) && parsed;
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

	private static IReadOnlySet<string> ReadClassRestrictions(string? restrict)
	{
		// Java parity: model/templates/item/ItemTemplate.levelRestrictions ordinal order from PlayerClass.
		if (string.IsNullOrWhiteSpace(restrict))
			return new HashSet<string>(StringComparer.Ordinal);

		var restrictions = restrict.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var classRestrictions = new HashSet<string>(StringComparer.Ordinal);
		for (var i = 0; i < restrictions.Length && i < PlayerClasses.Length; i++)
		{
			if (int.TryParse(restrictions[i], out var requiredLevel) && requiredLevel > 0)
				classRestrictions.Add(PlayerClasses[i]);
		}

		return classRestrictions;
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
