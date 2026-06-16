using System.Collections.ObjectModel;
using Aion.GameServer.Model.Vortex;
using System.Globalization;
using System.Xml;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Services;
using Aion.GameServer.Services.ToyPet;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Dataholders;

public sealed partial class StaticData
{
	private StaticData(
		string cacheFilePath,
		IReadOnlyList<string> importedFiles,
		IReadOnlyDictionary<string, int> elementCounts,
		IReadOnlyList<string> topLevelElements,
		IReadOnlyList<WorldMapSummary> worldMaps,
		FlightZoneTable flightZones,
		CreaturePvpZoneTable creaturePvpZones,
		PlayerExperienceTable playerExperienceTable,
		ItemTemplateTable itemTemplates,
		CosmeticItemTable cosmeticItems,
		DecomposableItemTable decomposableItems,
		AssemblyItemTable assemblyItems,
		ItemPurificationTable itemPurifications,
		ItemRestrictionCleanupTable itemRestrictionCleanups,
		RideTable rideInfos,
		ItemRandomBonusTable itemRandomBonuses,
		ItemSetTable itemSets,
		EnchantTable enchantTemplates,
		TemperingTable temperingTemplates,
		WalkerTemplateTable walkerTemplates,
		WalkerVersionTable walkerVersions,
		RiftLocationTable riftLocations,
		NpcSpawnTable npcSpawns,
		StaticDoorTable staticDoors,
		NpcRiftSpawnTable npcRiftSpawns,
		NpcVortexSpawnTable npcVortexSpawns,
		NpcFactionTable npcFactions,
		TradeListTable tradeLists,
		GoodsListTable goodsLists,
		CustomNpcDropTable customNpcDrops,
		QuestDropTable questDrops,
		NpcSkillTable npcSkills,
		PetSkillTable petSkills,
		PetTemplateTable petTemplates,
		PetDopingTable petDopings,
		TitleTemplateTable titleTemplates,
		RecipeTemplateTable recipeTemplates,
		WorkOrderRecipeTable workOrderRecipes,
		HousingTemplateTable housingTemplates,
		HousingObjectTemplateTable housingObjectTemplates,
		InstanceCooltimeTable instanceCooltimes,
		InstanceExitTable instanceExits,
		PortalLocTable portalLocs,
		AutoGroupTable autoGroups,
		PlayerInitialDataTable playerInitialData,
		StorageExpansionTemplateTable cubeExpansionTemplates,
		StorageExpansionTemplateTable warehouseExpansionTemplates,
		LegionDominionTable legionDominions,
		AtreianPassportTable atreianPassports,
		Task? validationTask)
	{
		CacheFilePath = cacheFilePath;
		ImportedFiles = importedFiles;
		ElementCounts = elementCounts;
		TopLevelElements = topLevelElements;
		WorldMaps = worldMaps;
		FlightZones = flightZones;
		CreaturePvpZones = creaturePvpZones;
		PlayerExperienceTable = playerExperienceTable;
		ItemTemplates = itemTemplates;
		CosmeticItems = cosmeticItems;
		DecomposableItems = decomposableItems;
		AssemblyItems = assemblyItems;
		ItemPurifications = itemPurifications;
		ItemRestrictionCleanups = itemRestrictionCleanups;
		RideInfos = rideInfos;
		ItemRandomBonuses = itemRandomBonuses;
		ItemSets = itemSets;
		EnchantTemplates = enchantTemplates;
		TemperingTemplates = temperingTemplates;
		WalkerTemplates = walkerTemplates;
		WalkerVersions = walkerVersions;
		RiftLocations = riftLocations;
		NpcSpawns = npcSpawns;
		StaticDoors = staticDoors;
		NpcRiftSpawns = npcRiftSpawns;
		NpcVortexSpawns = npcVortexSpawns;
		NpcFactions = npcFactions;
		TradeLists = tradeLists;
		GoodsLists = goodsLists;
		CustomNpcDrops = customNpcDrops;
		QuestDrops = questDrops;
		NpcSkills = npcSkills;
		PetSkills = petSkills;
		PetTemplates = petTemplates;
		PetDopings = petDopings;
		TitleTemplates = titleTemplates;
		RecipeTemplates = recipeTemplates;
		WorkOrderRecipes = workOrderRecipes;
		HousingTemplates = housingTemplates;
		HousingObjectTemplates = housingObjectTemplates;
		InstanceCooltimes = instanceCooltimes;
		InstanceExits = instanceExits;
		PortalLocs = portalLocs;
		AutoGroups = autoGroups;
		PlayerInitialData = playerInitialData;
		CubeExpansionTemplates = cubeExpansionTemplates;
		WarehouseExpansionTemplates = warehouseExpansionTemplates;
		LegionDominions = legionDominions;
		AtreianPassports = atreianPassports;
		ValidationTask = validationTask;
	}

	public string CacheFilePath { get; }

	public IReadOnlyList<string> ImportedFiles { get; }

	public int ImportedFileCount => ImportedFiles.Count;

	public IReadOnlyDictionary<string, int> ElementCounts { get; }

	public IReadOnlyList<string> TopLevelElements { get; }

	public IReadOnlyList<WorldMapSummary> WorldMaps { get; }

	public FlightZoneTable FlightZones { get; }

	public CreaturePvpZoneTable CreaturePvpZones { get; }

	public PlayerExperienceTable PlayerExperienceTable { get; }

	public ItemTemplateTable ItemTemplates { get; }

	public CosmeticItemTable CosmeticItems { get; }

	// Faithful CosmeticItemsData holder (empty-default; runtime XML load deferred) - summary->template re-point.
	public CosmeticItemsData CosmeticItemsDataDh { get; private set; } = new();

	public DecomposableItemTable DecomposableItems { get; }

	public AssemblyItemTable AssemblyItems { get; }

	public ItemPurificationTable ItemPurifications { get; }

	public ItemRestrictionCleanupTable ItemRestrictionCleanups { get; }

	public RideTable RideInfos { get; }

	public ItemRandomBonusTable ItemRandomBonuses { get; }

	public ItemSetTable ItemSets { get; }

	public EnchantTable EnchantTemplates { get; }

	public TemperingTable TemperingTemplates { get; }

	public WalkerTemplateTable WalkerTemplates { get; }

	public WalkerVersionTable WalkerVersions { get; }

	public RiftLocationTable RiftLocations { get; }

	// Faithful VortexData holder (empty-default; runtime XML load deferred) - summary->template re-point.
	public VortexData VortexDataDh { get; private set; } = new();

	// Faithful RiftData holder (dataholders/RiftData) feeds DataManager.RIFT_DATA; loaded from rift/rift_locations.xml.
	public RiftData RiftDataDh { get; private set; } = new();

	public NpcSpawnTable NpcSpawns { get; }

	public StaticDoorTable StaticDoors { get; }

	public NpcRiftSpawnTable NpcRiftSpawns { get; }

	public NpcVortexSpawnTable NpcVortexSpawns { get; }

	public NpcFactionTable NpcFactions { get; }

	public TradeListTable TradeLists { get; }

	public GoodsListTable GoodsLists { get; }

	public CustomNpcDropTable CustomNpcDrops { get; }

	public QuestDropTable QuestDrops { get; }

	public NpcSkillTable NpcSkills { get; }

	public PetSkillTable PetSkills { get; }

	public PetTemplateTable PetTemplates { get; }

	public PetDopingTable PetDopings { get; }

	// Faithful PetFeedData holder — populated from pets/pet_feed.xml at boot (LoadLeafHoldersFromFiles).
	public PetFeedData PetFeedDataDh { get; private set; } = new();

	public TitleTemplateTable TitleTemplates { get; }

	public RecipeTemplateTable RecipeTemplates { get; }

	public WorkOrderRecipeTable WorkOrderRecipes { get; }

	public HousingTemplateTable HousingTemplates { get; }

	public HousingObjectTemplateTable HousingObjectTemplates { get; }

	public InstanceCooltimeTable InstanceCooltimes { get; }

	public InstanceExitTable InstanceExits { get; }

	public PortalLocTable PortalLocs { get; }

	public AutoGroupTable AutoGroups { get; }

	public PlayerInitialDataTable PlayerInitialData { get; }

	public StorageExpansionTemplateTable CubeExpansionTemplates { get; }

	public CubeExpandData CubeExpandDataDh { get; private set; } = new();

	public StorageExpansionTemplateTable WarehouseExpansionTemplates { get; }

	public WarehouseExpandData WarehouseExpandDataDh { get; private set; } = new();

	public LegionDominionTable LegionDominions { get; }

	public LegionDominionData LegionDominionDataDh { get; private set; } = new();

	public AtreianPassportTable AtreianPassports { get; }

	public WindstreamData WindstreamDataDh { get; private set; } = new();

	public Task? ValidationTask { get; }

	// Java parity: DataManager.QUEST_DATA / TRIBE_RELATIONS_DATA / WORLD_MAPS_DATA / NPC_SHOUT_DATA / UPGRADE_ARCADE_DATA.
	// These faithful dataholder classes are not yet populated by the bespoke XML loader; exposed here with empty
	// defaults so the DataManager.*_DATA accessors compile. TODO(runtime): deserialize their source XML
	// (e.g. game-server/data/static_data/quest_data/quest_data.xml is a self-contained <quests> root) and assign here.
	public QuestsData Quests { get; private set; } = new();
	public TribeRelationsData TribeRelations { get; private set; } = new();
	public WorldMapsData WorldMaps2 { get; private set; } = new();
	public NpcShoutData NpcShouts { get; private set; } = new();
	public UpgradeArcadeData UpgradeArcade { get; private set; } = new();
	public SiegeLocationData SiegeLocations { get; private set; } = new();
	public ItemGroupsData ItemGroups { get; private set; } = new();
	public Aion.GameServer.Model.Templates.Mail.Mails SystemMailTemplates { get; private set; } = new();
	public HouseBuildingData HouseBuildings { get; private set; } = new();
	public GuideHtmlData GuideHtml { get; private set; } = new();
	public MaterialData Materials { get; private set; } = new();
	public ZoneData ZoneInfo { get; private set; } = new();
	public XMLQuests XmlQuests { get; private set; } = new();
	public TownSpawnsData TownSpawns { get; private set; } = new();
	public SkillChargeData SkillCharges { get; private set; } = new();
	public MotionData Motions { get; private set; } = new();
	public MapWeatherData MapWeathers { get; private set; } = new();
	public EventData Events { get; private set; } = new();
	public PanelSkillsData PanelSkillsDataDh { get; private set; } = new();
	public ItemRestrictionCleanupData ItemRestrictionCleanupDataDh { get; private set; } = new();
	public ConquerorAndProtectorData ConquerorAndProtectorDataDh { get; private set; } = new();
	public AbsoluteStatsData AbsoluteStatsDataDh { get; private set; } = new();
	public WorldRaidData WorldRaidDataDh { get; private set; } = new();
	public TeleporterData TeleporterDataDh { get; private set; } = new();
	public TeleLocationData TeleLocationDataDh { get; private set; } = new();
	public SkillAliasLocationData SkillAliasLocationDataDh { get; private set; } = new();
	public SignetDataTemplates SignetDataTemplatesDh { get; private set; } = new();
	public ShieldData ShieldDataDh { get; private set; } = new();
	public RoadData RoadDataDh { get; private set; } = new();
	public MultiReturnItemData MultiReturnItemDataDh { get; private set; } = new();
	public KillBountyData KillBountyDataDh { get; private set; } = new();
	public InstanceBuffData InstanceBuffDataDh { get; private set; } = new();
	public HousePartsData HousePartsDataDh { get; private set; } = new();
	public HouseNpcsData HouseNpcsDataDh { get; private set; } = new();
	public HotspotData HotspotDataDh { get; private set; } = new();
	public GatherableData GatherableDataDh { get; private set; } = new();
	public FlyRingData FlyRingDataDh { get; private set; } = new();
	public FlyPathData FlyPathDataDh { get; private set; } = new();
	public CuringObjectsData CuringObjectsDataDh { get; private set; } = new();
	public BaseData BaseDataDh { get; private set; } = new();
	public AssembledNpcsData AssembledNpcsDataDh { get; private set; } = new();
	public TitleData TitleDataDh { get; private set; } = new();
	public NpcData NpcDataDh { get; private set; } = new();
	public AIData AiDataDh { get; private set; } = new();
	public ChestData ChestDataDh { get; private set; } = new();
	public BindPointData BindPointDataDh { get; private set; } = new();
	public ItemData ItemDataDh { get; private set; } = new();
	public SkillData SkillDataDh { get; private set; } = new();
	public InstanceCooltimeData InstanceCooltimeDataDh { get; private set; } = new();
	public TradeListData TradeListDataDh { get; private set; } = new();
	public RecipeData RecipeDataDh { get; private set; } = new();
	public PetData PetDataDh { get; private set; } = new();
	public WalkerData WalkerDataDh { get; private set; } = new();
	public AutoGroupData AutoGroupDataDh { get; private set; } = new();
	public ItemSetData ItemSetDataDh { get; private set; } = new();
	public RideData RideDataDh { get; private set; } = new();
	public GoodsListData GoodsListDataDh { get; private set; } = new();
	public ItemPurificationData ItemPurificationDataDh { get; private set; } = new();
	public AtreianPassportData AtreianPassportDataDh { get; private set; } = new();
	public PetDopingData PetDopingDataDh { get; private set; } = new();
	public NpcSkillData NpcSkillDataDh { get; private set; } = new();
	public NpcFactionsData NpcFactionsDataDh { get; private set; } = new();
	public PortalLocData PortalLocDataDh { get; private set; } = new();
	public AssemblyItemsData AssemblyItemsDataDh { get; private set; } = new();
	public GlobalDropData GlobalDropDataDh { get; private set; } = new();
	public GlobalNpcExclusionData GlobalNpcExclusionDataDh { get; private set; } = new();
	public InstanceExitData InstanceExitDataDh { get; private set; } = new();

	public int GetElementCount(string elementName)
	{
		return ElementCounts.TryGetValue(elementName, out var count) ? count : 0;
	}

	/// <summary>
	/// Java parity: JAXB unmarshal populates the faithful per-feature data holders from the static_data graph.
	/// The C# model-A streaming parser does not touch these holders, so this loads the proven leaf holders
	/// directly from their per-feature XML files (game-server/data/static_data/...) via
	/// <see cref="LoadingUtils.JaxbHolderLoader"/> and assigns them into the *Dh slots that
	/// <see cref="DataManager"/> delegates to (BIND_POINT_DATA, CHEST_DATA, CURING_OBJECTS_DATA, ROAD_DATA,
	/// HOTSPOT_DATA). Each load is guarded by a file-exists check and try/catch so a missing or malformed
	/// feature file never aborts boot (mirrors how Java DataManager tolerates per-holder load failures).
	/// Only the leaf holders proven against their real XML are wired here; the large/cross-referenced holders
	/// (Item/Npc/Skill/Quest/AI) remain deferred.
	/// </summary>
	public void LoadLeafHoldersFromFiles(string staticDataDirectory, Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		BindPointDataDh = TryLoadHolder(BindPointDataDh, Path.Combine(staticDataDirectory, "bind_points", "bind_points.xml"), logger);
		ChestDataDh = TryLoadHolder(ChestDataDh, Path.Combine(staticDataDirectory, "chests", "chest_templates.xml"), logger);
		CuringObjectsDataDh = TryLoadHolder(CuringObjectsDataDh, Path.Combine(staticDataDirectory, "curing_objects", "curing_objects.xml"), logger);
		RoadDataDh = TryLoadHolder(RoadDataDh, Path.Combine(staticDataDirectory, "roads", "roads.xml"), logger);
		HotspotDataDh = TryLoadHolder(HotspotDataDh, Path.Combine(staticDataDirectory, "hotspot_template.xml"), logger);
		MapWeathers = TryLoadHolder(MapWeathers, Path.Combine(staticDataDirectory, "weather_table.xml"), logger);
		KillBountyDataDh = TryLoadHolder(KillBountyDataDh, Path.Combine(staticDataDirectory, "bounties", "kill_bounties.xml"), logger);
		BaseDataDh = TryLoadHolder(BaseDataDh, Path.Combine(staticDataDirectory, "base", "base_locations.xml"), logger);
		LegionDominionDataDh = TryLoadHolder(LegionDominionDataDh, Path.Combine(staticDataDirectory, "legion_dominion_template.xml"), logger);
		GatherableDataDh = TryLoadHolder(GatherableDataDh, Path.Combine(staticDataDirectory, "gatherables", "gatherable_templates.xml"), logger);
		MultiReturnItemDataDh = TryLoadHolder(MultiReturnItemDataDh, Path.Combine(staticDataDirectory, "items", "multi_return_item.xml"), logger);
		FlyRingDataDh = TryLoadHolder(FlyRingDataDh, Path.Combine(staticDataDirectory, "fly_rings", "fly_rings.xml"), logger);
		WindstreamDataDh = TryLoadHolder(WindstreamDataDh, Path.Combine(staticDataDirectory, "windstreams", "windstreams.xml"), logger);
		TeleLocationDataDh = TryLoadHolder(TeleLocationDataDh, Path.Combine(staticDataDirectory, "teleport_location.xml"), logger);
		PetDopingDataDh = TryLoadHolder(PetDopingDataDh, Path.Combine(staticDataDirectory, "pets", "pet_doping.xml"), logger);
		FlyPathDataDh = TryLoadHolder(FlyPathDataDh, Path.Combine(staticDataDirectory, "flypath_template.xml"), logger);
		ShieldDataDh = TryLoadHolder(ShieldDataDh, Path.Combine(staticDataDirectory, "siege", "siege_shields.xml"), logger);
		PortalLocDataDh = TryLoadHolder(PortalLocDataDh, Path.Combine(staticDataDirectory, "portals", "portal_loc.xml"), logger);
		SkillAliasLocationDataDh = TryLoadHolder(SkillAliasLocationDataDh, Path.Combine(staticDataDirectory, "skills", "alias_locations.xml"), logger);
		InstanceBuffDataDh = TryLoadHolder(InstanceBuffDataDh, Path.Combine(staticDataDirectory, "instance_bonusattr", "instance_bonusattr.xml"), logger);
		HouseNpcsDataDh = TryLoadHolder(HouseNpcsDataDh, Path.Combine(staticDataDirectory, "housing", "house_npcs.xml"), logger);
		CosmeticItemsDataDh = TryLoadHolder(CosmeticItemsDataDh, Path.Combine(staticDataDirectory, "cosmetic_items", "cosmetic_items.xml"), logger);
		AssembledNpcsDataDh = TryLoadHolder(AssembledNpcsDataDh, Path.Combine(staticDataDirectory, "assembled_npcs", "assembled_npcs.xml"), logger);
		SignetDataTemplatesDh = TryLoadHolder(SignetDataTemplatesDh, Path.Combine(staticDataDirectory, "skills", "signet_data_templates.xml"), logger);
		ItemPurificationDataDh = TryLoadHolder(ItemPurificationDataDh, Path.Combine(staticDataDirectory, "items", "item_purifications.xml"), logger);
		PanelSkillsDataDh = TryLoadHolder(PanelSkillsDataDh, Path.Combine(staticDataDirectory, "polymorph_panels", "polymorph_panels.xml"), logger);
		RideDataDh = TryLoadHolder(RideDataDh, Path.Combine(staticDataDirectory, "ride", "ride.xml"), logger);
		WorldRaidDataDh = TryLoadHolder(WorldRaidDataDh, Path.Combine(staticDataDirectory, "world_raid", "world_raids.xml"), logger);
		GoodsListDataDh = TryLoadHolder(GoodsListDataDh, Path.Combine(staticDataDirectory, "goodslists", "goodslists.xml"), logger);
		NpcFactionsDataDh = TryLoadHolder(NpcFactionsDataDh, Path.Combine(staticDataDirectory, "npc_factions", "npc_factions.xml"), logger);
		TeleporterDataDh = TryLoadHolder(TeleporterDataDh, Path.Combine(staticDataDirectory, "npc_teleporter.xml"), logger);
		HousePartsDataDh = TryLoadHolder(HousePartsDataDh, Path.Combine(staticDataDirectory, "housing", "house_parts.xml"), logger);
		ItemRestrictionCleanupDataDh = TryLoadHolder(ItemRestrictionCleanupDataDh, Path.Combine(staticDataDirectory, "items", "item_restriction_cleanups.xml"), logger);
		AssemblyItemsDataDh = TryLoadHolder(AssemblyItemsDataDh, Path.Combine(staticDataDirectory, "items", "assembly_items.xml"), logger);
		AtreianPassportDataDh = TryLoadHolder(AtreianPassportDataDh, Path.Combine(staticDataDirectory, "events", "login_events.xml"), logger);
		AbsoluteStatsDataDh = TryLoadHolder(AbsoluteStatsDataDh, Path.Combine(staticDataDirectory, "stats", "absolute_stats.xml"), logger);
		ItemSetDataDh = TryLoadHolder(ItemSetDataDh, Path.Combine(staticDataDirectory, "item_sets", "item_sets.xml"), logger);
		TitleDataDh = TryLoadHolder(TitleDataDh, Path.Combine(staticDataDirectory, "player_titles.xml"), logger);
		ConquerorAndProtectorDataDh = TryLoadHolder(ConquerorAndProtectorDataDh, Path.Combine(staticDataDirectory, "conqueror_protector_ranks", "conqueror_protector_ranks.xml"), logger);
		VortexDataDh = TryLoadHolder(VortexDataDh, Path.Combine(staticDataDirectory, "vortex", "dimensional_vortex.xml"), logger);
		RiftDataDh = TryLoadHolder(RiftDataDh, Path.Combine(staticDataDirectory, "rift", "rift_locations.xml"), logger);
		// Standalone <npc_templates> root (~35MB, no cache imports / no @XmlIDREF resolution at this stage):
		// the faithful NpcData holder feeds DataManager.NPC_DATA (~25 gameplay consumers).
		NpcDataDh = TryLoadHolder(NpcDataDh, Path.Combine(staticDataDirectory, "npcs", "npc_templates.xml"), logger);
		// Standalone <item_templates> root (~65MB; ItemTemplate is the @XmlID *target*, no IDREF source here):
		// the faithful ItemData holder feeds DataManager.ITEM_DATA (~221 gameplay consumers) and lights up the
		// already-wired NPC equipment id->ItemTemplate lazy resolution.
		ItemDataDh = TryLoadHolder(ItemDataDh, Path.Combine(staticDataDirectory, "items", "item_templates.xml"), logger);
		// Standalone <skill_data> root (~12MB): the faithful SkillData holder feeds DataManager.SKILL_DATA
		// (~81 gameplay consumers). Deep polymorphic effect/condition/property cones bind via the existing
		// [XmlElement(typeof(...))] subtype maps; SkillData.AfterUnmarshal fires each Effects.AfterUnmarshal
		// (effectTypes set) children-first since XmlSerializer skips JAXB callbacks.
		SkillDataDh = TryLoadHolder(SkillDataDh, Path.Combine(staticDataDirectory, "skills", "skill_templates.xml"), logger);
		// AIData feeds DataManager.AI_DATA (the 462 ported AI handlers + SummonerAI). Java's static_data import
		// graph merges every <ai_templates> source into one holder; here we deserialize each ai/*.xml raw, merge
		// their pending <ai> rows, then run AfterUnmarshal once (fires SummonGroup min/maxCount validation).
		AiDataDh = TryLoadAiData(Path.Combine(staticDataDirectory, "ai"), logger);
		// Standalone <quests> root (~82k lines, self-contained, no imports / no IDREF resolution): the faithful
		// QuestsData holder feeds DataManager.QUEST_DATA (the 1025 ported quest handlers).
		Quests = TryLoadHolder(Quests, Path.Combine(staticDataDirectory, "quest_data", "quest_data.xml"), logger);
		// Standalone <auto_groups> root: faithful AutoGroupData feeds DataManager.AUTO_GROUP (instance auto-group templates).
		AutoGroupDataDh = TryLoadHolder(AutoGroupDataDh, Path.Combine(staticDataDirectory, "auto_group", "auto_group.xml"), logger);
		// Standalone <recipe_templates> root: faithful RecipeData feeds DataManager.RECIPE_DATA (crafting recipes).
		RecipeDataDh = TryLoadHolder(RecipeDataDh, Path.Combine(staticDataDirectory, "recipe", "recipe_templates.xml"), logger);
		// <npc_trade_list> root (single file): faithful TradeListData feeds DataManager.TRADE_LIST_DATA (merchant lists).
		TradeListDataDh = TryLoadHolder(TradeListDataDh, Path.Combine(staticDataDirectory, "npc_trade_list.xml"), logger);
		// <pets> root (single file): faithful PetData feeds DataManager.PET_DATA (toypet templates).
		PetDataDh = TryLoadHolder(PetDataDh, Path.Combine(staticDataDirectory, "pets", "pets.xml"), logger);
		// <npc_skill_templates> folder (recursive: npc_skills.xml + guard/siege/rift + instances/* + open_worlds/*):
		// Java imports the npc_skills/ dir with singleRootTag+recursiveImport, so merge every file's <npc_skills>
		// rows then run AfterUnmarshal once. Feeds DataManager.NPC_SKILL_DATA.
		NpcSkillDataDh = TryLoadMergedHolder<NpcSkillData>(Path.Combine(staticDataDirectory, "npc_skills"), (m, p) => m.MergePending(p), logger);
		// <npc_walker> folder (recursive: per-instance route files): Java imports the npc_walker/ dir with
		// singleRootTag+recursiveImport, so merge every file's <walker_template> rows then run AfterUnmarshal once.
		// Feeds DataManager.WALKER_DATA.
		WalkerDataDh = TryLoadMergedHolder<WalkerData>(Path.Combine(staticDataDirectory, "npc_walker"), (m, p) => m.MergePending(p), logger);
		// <tribe_relations> root (single file): faithful TribeRelationsData feeds DataManager.TRIBE_RELATIONS_DATA.
		TribeRelations = TryLoadHolder(TribeRelations, Path.Combine(staticDataDirectory, "tribe", "tribe_relations.xml"), logger);
		// <npc_shouts> root (single file): faithful NpcShoutData feeds DataManager.NPC_SHOUT_DATA.
		NpcShouts = TryLoadHolder(NpcShouts, Path.Combine(staticDataDirectory, "npc_shouts", "npc_shouts.xml"), logger);
		// <arcadelist> root (single file): faithful UpgradeArcadeData feeds DataManager.UPGRADE_ARCADE_DATA.
		UpgradeArcade = TryLoadHolder(UpgradeArcade, Path.Combine(staticDataDirectory, "events", "arcadelist.xml"), logger);
		// <siege_locations> root (single file): faithful SiegeLocationData feeds DataManager.SIEGE_LOCATION_DATA.
		SiegeLocations = TryLoadHolder(SiegeLocations, Path.Combine(staticDataDirectory, "siege", "siege_locations.xml"), logger);
		// <item_groups> root (single file): faithful ItemGroupsData feeds DataManager.ITEM_GROUPS_DATA.
		ItemGroups = TryLoadHolder(ItemGroups, Path.Combine(staticDataDirectory, "items", "item_groups.xml"), logger);
		// <material_templates> root (single file): faithful MaterialData feeds DataManager.MATERIAL_DATA.
		Materials = TryLoadHolder(Materials, Path.Combine(staticDataDirectory, "mesh_materials", "material_templates.xml"), logger);
		// <skill_charge> root (single file): faithful SkillChargeData feeds DataManager.SKILL_CHARGE_DATA.
		SkillCharges = TryLoadHolder(SkillCharges, Path.Combine(staticDataDirectory, "skills", "skill_charge.xml"), logger);
		// <guides> root (single file): faithful GuideHtmlData feeds DataManager.GUIDE_HTML_DATA.
		GuideHtml = TryLoadHolder(GuideHtml, Path.Combine(staticDataDirectory, "guides", "guide.xml"), logger);
		// <instance_cooltimes> root (single file): faithful InstanceCooltimeData feeds DataManager.INSTANCE_COOLTIME_DATA.
		InstanceCooltimeDataDh = TryLoadHolder(InstanceCooltimeDataDh, Path.Combine(staticDataDirectory, "instance_cooltimes", "instance_cooltimes.xml"), logger);
		// <buildings> root (single file): faithful HouseBuildingData feeds DataManager.HOUSE_BUILDING_DATA.
		HouseBuildings = TryLoadHolder(HouseBuildings, Path.Combine(staticDataDirectory, "housing", "house_buildings.xml"), logger);
		// <motion_times> root (single file): faithful MotionData feeds DataManager.MOTION_DATA; AfterUnmarshal fires
		// each MotionTime.AfterUnmarshal children-first so the per-weapon-type Times maps are parsed.
		Motions = TryLoadHolder(Motions, Path.Combine(staticDataDirectory, "skills", "motion_times.xml"), logger);
		// <zones> folder (recursive: zones_*.xml + zone_abyss_shields/zones_quest/zones_weather, all <zones>/<zone>):
		// Java imports the zones/ dir with singleRootTag+recursiveImport, so merge every file's <zone> rows then run
		// AfterUnmarshal once (builds Poly/Cylinder/Sphere/Semisphere areas + weather-zone numbering). Feeds ZONE_DATA.
		ZoneInfo = TryLoadMergedHolder<ZoneData>(Path.Combine(staticDataDirectory, "zones"), (m, p) => m.MergePending(p), logger);
		// <mails> root (single file): faithful Mails feeds DataManager.SYSTEM_MAIL_TEMPLATES; AfterUnmarshal cascades
		// each MailTemplate/SysMail AfterUnmarshal children-first to build the part/case indices.
		SystemMailTemplates = TryLoadHolder(SystemMailTemplates, Path.Combine(staticDataDirectory, "mail_templates.xml"), logger);
		// Java imports the town_spawns/ dir file-by-file (each its own <town_spawns_data> root); merge every file's
		// spawn_map rows then run AfterUnmarshal once. Spawn/SpawnSpotTemplate nullable attrs bind via string proxies.
		TownSpawns = TryLoadMergedHolder<TownSpawnsData>(Path.Combine(staticDataDirectory, "town_spawns"), (m, p) => m.MergePending(p), logger);
		// Java imports the events/timed_events/ dir (custom_events.xml + retail_events.xml), each its own
		// <timed_events> root; merge every file's <event> rows then run AfterUnmarshal once (validates dates +
		// fires each event's SpawnsData.Initialize children-first). EventTemplate's GlobalRule drop-rule cone
		// binds nullable restriction_race via a string proxy. Feeds DataManager.EVENT_DATA.
		Events = TryLoadMergedHolder<EventData>(Path.Combine(staticDataDirectory, "events", "timed_events"), (m, p) => m.MergePending(p), logger);
		// <warehouse_expander> root (single file): faithful WarehouseExpandData feeds DataManager.WAREHOUSEEXPANDER_DATA.
		// StorageExpansionTemplate ids="..." space-separated int[] binds via the IdsRaw string proxy; <expand> rows public.
		WarehouseExpandDataDh = TryLoadHolder(WarehouseExpandDataDh, Path.Combine(staticDataDirectory, "storage_expander", "warehouse_expander.xml"), logger);
		// <cube_expander> root (single file): faithful CubeExpandData feeds DataManager.CUBEEXPANDER_DATA (same template shape).
		CubeExpandDataDh = TryLoadHolder(CubeExpandDataDh, Path.Combine(staticDataDirectory, "storage_expander", "cube_expander.xml"), logger);
		// <pet_feed> root (single file): faithful PetFeedData feeds DataManager.PET_FEED_DATA. PetFlavour/PetRewards/
		// PetFeedResult bind public fields; <food group="..."> maps to the FoodType enum by wire-name.
		PetFeedDataDh = TryLoadHolder(PetFeedDataDh, Path.Combine(staticDataDirectory, "pets", "pet_feed.xml"), logger);
		// Java imports the quest_script_data/ dir (89 files, each its own <quest_scripts> root) with
		// recursiveImport, so deserialize each file raw, merge every file's polymorphic <xml_quest>/<monster_hunt>/...
		// rows then run AfterUnmarshal once (builds questsById). The 16-subtype [XmlElement(typeof(...))] map +
		// deep Events/Conditions/Operations cone bind via the now-public [Xml*] members. Feeds DataManager.XML_QUESTS.
		XmlQuests = TryLoadMergedHolder<XMLQuests>(Path.Combine(staticDataDirectory, "quest_script_data"), (m, p) => m.MergePending(p), logger);
		// <world_maps> root (single file): faithful WorldMapsData feeds DataManager.WORLD_MAPS_DATA. The holder is
		// IEnumerable (collection-typed to XmlSerializer), so the file is read into WorldMapsDataDto and SetData builds
		// the mapsById index. WorldMapTemplate flags="..." wire tokens map to ZoneAttributes via the FlagsRaw proxy.
		WorldMaps2 = TryLoadWorldMaps(Path.Combine(staticDataDirectory, "world_maps.xml"), logger);
		// Java imports the single file global_drops/global_npc_exclusions.xml (<global_npc_exclusions> root) and binds it
		// to StaticData.globalExclusionData; feeds DataManager.GLOBAL_EXCLUSION_DATA, read by DropRegistrationService.
		// HasGlobalNpcExclusions. The @XmlList Set<...> elements bind via the holder's Raw space-split proxy properties,
		// and afterUnmarshal (computing isEmpty) fires inside TryLoadHolder's JaxbHolderLoader.LoadFromFile.
		GlobalNpcExclusionDataDh = TryLoadHolder(GlobalNpcExclusionDataDh, Path.Combine(staticDataDirectory, "global_drops", "global_npc_exclusions.xml"), logger);
		// Java imports the global_drops/rules/ dir with singleRootTag (every file is a <global_rules> root of <gd_rule>
		// rows) and binds it to StaticData.globalDropData; merge every file then run AfterUnmarshal-free. Feeds
		// DataManager.GLOBAL_DROP_DATA. After NPC data is loaded above, run the gd_npc_names -> gd_npc id expansion
		// (Java parity: DataManager.init() calls GLOBAL_DROP_DATA.processRules(NPC_DATA.getNpcData()) after field assignment).
		GlobalDropDataDh = TryLoadMergedHolder<GlobalDropData>(Path.Combine(staticDataDirectory, "global_drops", "rules"), (m, p) => m.MergePending(p), logger);
		// Java imports the single file instance_exit/instance_exit.xml (<instance_exits> root) and binds it to
		// StaticData.instanceExitData; feeds DataManager.INSTANCE_EXIT_DATA, read by TeleportService for the
		// per-world instance-exit teleport. Was a hollow new() -> always empty -> exit lookups returned null.
		// InstanceExit's primitive attrs (incl. race="PC_ALL"/"ELYOS"/"ASMODIANS" -> Race by member name) bind via
		// the now-public fields; the holder's AfterUnmarshal (computeIfAbsent index) fires inside TryLoadHolder.
		InstanceExitDataDh = TryLoadHolder(InstanceExitDataDh, Path.Combine(staticDataDirectory, "instance_exit", "instance_exit.xml"), logger);
		try
		{
			GlobalDropDataDh.ProcessRules(NpcDataDh.GetNpcData());
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Failed to process global drop rules (gd_npc_names expansion).");
		}
	}

	private static WorldMapsData TryLoadWorldMaps(string xmlFilePath, Microsoft.Extensions.Logging.ILogger? logger)
	{
		try
		{
			if (!File.Exists(xmlFilePath))
			{
				logger?.LogWarning("Static data holder file not found, leaving WorldMapsData empty: {Path}", xmlFilePath);
				return new WorldMapsData();
			}

			var dto = LoadingUtils.JaxbHolderLoader.DeserializeFile<WorldMapsDataDto>(xmlFilePath);
			var holder = new WorldMapsData();
			holder.SetData(dto.Maps);
			return holder;
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Failed to load WorldMapsData from {Path}; leaving it empty.", xmlFilePath);
			return new WorldMapsData();
		}
	}

	private static T TryLoadMergedHolder<T>(string directory, Action<T, T> mergePending, Microsoft.Extensions.Logging.ILogger? logger) where T : class, new()
	{
		try
		{
			if (!Directory.Exists(directory))
			{
				logger?.LogWarning("Static data holder directory not found, leaving {Holder} empty: {Path}", typeof(T).Name, directory);
				return new T();
			}

			T? merged = null;
			foreach (var file in Directory.EnumerateFiles(directory, "*.xml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
			{
				var part = LoadingUtils.JaxbHolderLoader.DeserializeFile<T>(file);
				if (merged == null)
					merged = part;
				else
					mergePending(merged, part);
			}

			if (merged == null)
				return new T();

			LoadingUtils.JaxbHolderLoader.RunAfterUnmarshal(merged);
			return merged;
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Failed to load merged static data holder {Holder} from {Path}; leaving it empty.", typeof(T).Name, directory);
			return new T();
		}
	}

	private static AIData TryLoadAiData(string aiDirectory, Microsoft.Extensions.Logging.ILogger? logger)
	{
		try
		{
			if (!Directory.Exists(aiDirectory))
			{
				logger?.LogWarning("AI data directory not found, leaving AIData empty: {Path}", aiDirectory);
				return new AIData();
			}

			AIData? merged = null;
			foreach (var file in Directory.EnumerateFiles(aiDirectory, "*.xml").OrderBy(f => f, StringComparer.Ordinal))
			{
				var part = LoadingUtils.JaxbHolderLoader.DeserializeFile<AIData>(file);
				if (merged == null)
					merged = part;
				else
					merged.MergePending(part);
			}

			if (merged == null)
				return new AIData();

			LoadingUtils.JaxbHolderLoader.RunAfterUnmarshal(merged);
			return merged;
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Failed to load AIData from {Path}; leaving it empty.", aiDirectory);
			return new AIData();
		}
	}

	private static T TryLoadHolder<T>(T fallback, string xmlFilePath, Microsoft.Extensions.Logging.ILogger? logger) where T : class
	{
		try
		{
			if (!File.Exists(xmlFilePath))
			{
				logger?.LogWarning("Static data holder file not found, leaving {Holder} empty: {Path}", typeof(T).Name, xmlFilePath);
				return fallback;
			}

			return LoadingUtils.JaxbHolderLoader.LoadFromFile<T>(xmlFilePath);
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Failed to load static data holder {Holder} from {Path}; leaving it empty.", typeof(T).Name, xmlFilePath);
			return fallback;
		}
	}

	public static async Task<StaticData> LoadFromCacheAsync(
		string cacheFilePath,
		IReadOnlyList<string> importedFiles,
		Task? validationTask = null,
		CancellationToken cancellationToken = default)
	{
		return await LoadFromCacheAsync(cacheFilePath, importedFiles, null, validationTask, cancellationToken);
	}

	public static async Task<StaticData> LoadFromCacheAsync(
		string cacheFilePath,
		IReadOnlyList<string> importedFiles,
		string? questHandlerDirectory,
		Task? validationTask = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dataholders/DataManager static_data.xml import graph plus typed DataHolder caches.
		var counts = new Dictionary<string, int>(StringComparer.Ordinal);
		var topLevelElements = new List<string>();
		var worldMaps = new List<WorldMapSummary>();
		var flightZones = new List<FlightZoneSummary>();
		var creaturePvpZones = new List<CreaturePvpZoneSummary>();
		var experience = new List<long>();
		var itemTemplates = new List<ItemTemplateSummary>();
		var cosmeticItems = new List<CosmeticItemSummary>();
		var decomposableItems = new List<DecomposableItemSummary>();
		var assemblyItems = new List<AssemblyItemSummary>();
		var itemPurifications = new List<ItemPurificationSummary>();
		var itemRestrictionCleanups = new List<ItemRestrictionCleanupSummary>();
		var rideInfos = new List<RideInfoSummary>();
		var itemRandomBonuses = new List<ItemRandomBonusSummary>();
		var itemSets = new List<ItemSetSummary>();
		var enchantGroups = new List<EnchantGroupSummary>();
		var temperingGroups = new List<TemperingGroupSummary>();
		var walkerTemplates = new List<WalkerTemplateSummary>();
		var walkerVersionParents = new Dictionary<string, string>(StringComparer.Ordinal);
		var riftLocations = new List<RiftLocationSummary>();
		var npcSpawns = new List<NpcSpawnSummary>();
		var staticDoors = new List<StaticDoorSummary>();
		var npcRiftSpawns = new List<NpcRiftSpawnSummary>();
		var npcVortexSpawns = new List<NpcVortexSpawnSummary>();
		var npcFactions = new List<NpcFactionSummary>();
		var tradeLists = new List<TradeListTemplateSummary>();
		var tradeInLists = new List<TradeListTemplateSummary>();
		var purchaseLists = new List<TradeListTemplateSummary>();
		var goodsLists = new List<GoodsListSummary>();
		var goodsInLists = new List<GoodsListSummary>();
		var goodsPurchaseLists = new List<GoodsListSummary>();
		var questDrops = new List<QuestDropSummary>();
		var npcSkillLists = new List<NpcSkillListSummary>();
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
		var instanceExits = new List<InstanceExitSummary>();
		var portalLocs = new List<PortalLocSummary>();
		var autoGroups = new List<AutoGroupSummary>();
		var petSkills = new List<PetSkillSummary>();
		var petTemplates = new List<PetTemplateSummary>();
		var petDopings = new List<PetDopingEntrySummary>();
		var cubeExpansionTemplates = new List<StorageExpansionTemplateSummary>();
		var warehouseExpansionTemplates = new List<StorageExpansionTemplateSummary>();
		var legionDominions = new List<LegionDominionLocationSummary>();
		var atreianPassports = new List<AtreianPassportSummary>();
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
		WalkerTemplateBuilder? currentWalkerTemplate = null;
		NpcSpawnBuilder? currentNpcSpawn = null;
		NpcSpawnSpotBuilder? currentNpcSpawnSpot = null;
		NpcRiftSpawnBuilder? currentNpcRiftSpawn = null;
		NpcSpawnSpotBuilder? currentNpcRiftSpawnSpot = null;
		NpcVortexSpawnBuilder? currentNpcVortexSpawn = null;
		NpcSpawnSpotBuilder? currentNpcVortexSpawnSpot = null;
		TradeListTemplateBuilder? currentTradeListTemplate = null;
		TradeListTemplateKind currentTradeListTemplateKind = TradeListTemplateKind.TradeList;
		int currentTradeListTemplateDepth = -1;
		GoodsListBuilder? currentGoodsList = null;
		GoodsListKind currentGoodsListKind = GoodsListKind.List;
		int currentGoodsListDepth = -1;
		QuestDropBuilder? currentQuestDropBuilder = null;
		int currentNpcSpawnMapId = 0;
		int currentNpcSpawnDepth = -1;
		int currentNpcSpawnSpotDepth = -1;
		int currentNpcRiftSpawnId = 0;
		int currentNpcRiftSpawnDepth = -1;
		int currentNpcRiftSpawnGroupIndex = 0;
		int currentNpcRiftSpawnGroupDepth = -1;
		int currentNpcRiftSpawnSpotDepth = -1;
		int currentNpcVortexSpawnId = 0;
		int currentNpcVortexSpawnDepth = -1;
		int currentNpcVortexSpawnStateDepth = -1;
		int currentNpcVortexSpawnGroupIndex = 0;
		int currentNpcVortexSpawnGroupDepth = -1;
		int currentNpcVortexSpawnSpotDepth = -1;
		VortexStateType currentNpcVortexSpawnStateType = default;
		int currentStaticDoorWorldId = 0;
		string currentWalkerParentRouteId = string.Empty;
		NpcSkillListBuilder? currentNpcSkillList = null;
		NpcSkillTemplateBuilder? currentNpcSkill = null;
		PetTemplateBuilder? currentPetTemplate = null;
		int currentPetTemplateDepth = -1;
		TitleTemplateBuilder? currentTitleTemplate = null;
		RecipeTemplateBuilder? currentRecipeTemplate = null;
		List<int>? currentStorageExpansionNpcIds = null;
		List<StorageExpansionPrice>? currentStorageExpansionPrices = null;
		bool currentStorageExpansionIsCube = false;
		CosmeticItemBuilder? currentCosmeticItem = null;
		DecomposableItemBuilder? currentDecomposableItem = null;
		int currentItemPurificationBaseItemId = 0;
		List<ItemPurificationResultSummary>? currentItemPurificationResults = null;
		ItemPurificationResultBuilder? currentItemPurificationResult = null;
		HousingBuildingBuilder? currentHousingBuilding = null;
		FlightZoneBuilder? currentFlightZone = null;
		CreaturePvpZoneBuilder? currentCreaturePvpZone = null;
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

				if (reader.Depth == 2 && reader.LocalName == "expansion_npc" && currentStorageExpansionNpcIds != null && currentStorageExpansionPrices != null)
				{
					var summary = new StorageExpansionTemplateSummary(
						currentStorageExpansionNpcIds.AsReadOnly(),
						currentStorageExpansionPrices.AsReadOnly());
					if (currentStorageExpansionIsCube)
						cubeExpansionTemplates.Add(summary);
					else
						warehouseExpansionTemplates.Add(summary);
					currentStorageExpansionNpcIds = null;
					currentStorageExpansionPrices = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "cosmetic_item" && currentCosmeticItem != null)
				{
					cosmeticItems.Add(currentCosmeticItem.ToSummary());
					currentCosmeticItem = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "recipe_template" && currentRecipeTemplate != null)
				{
					recipeTemplates.Add(currentRecipeTemplate.ToSummary());
					currentRecipeTemplate = null;
				}

				if (reader.Depth == 3 && reader.LocalName == "components_data" && currentRecipeTemplate != null)
					currentRecipeTemplate.EndComponentData();

				if (reader.Depth == 2 && reader.LocalName == "decomposable" && currentDecomposableItem != null)
				{
					decomposableItems.Add(currentDecomposableItem.ToSummary());
					currentDecomposableItem = null;
				}

				if (reader.Depth == 3 && reader.LocalName == "items" && currentDecomposableItem != null)
					currentDecomposableItem.EndCollection();

				if (reader.Depth == 3 && reader.LocalName == "purification_result" && currentItemPurificationResult != null && currentItemPurificationResults != null)
				{
					currentItemPurificationResults.Add(currentItemPurificationResult.ToSummary());
					currentItemPurificationResult = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "item_purification" && currentItemPurificationResults != null)
				{
					itemPurifications.Add(new ItemPurificationSummary(
						currentItemPurificationBaseItemId,
						currentItemPurificationResults.AsReadOnly()));
					currentItemPurificationBaseItemId = 0;
					currentItemPurificationResults = null;
				}

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

				if (reader.Depth == 2 && reader.LocalName == "walker_template" && currentWalkerTemplate != null)
				{
					walkerTemplates.Add(currentWalkerTemplate.ToSummary());
					currentWalkerTemplate = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "zone" && currentFlightZone != null)
				{
					if (currentFlightZone.HasEnoughPoints)
						flightZones.Add(currentFlightZone.ToSummary());
					currentFlightZone = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "zone" && currentCreaturePvpZone != null)
				{
					if (currentCreaturePvpZone.HasEnoughPoints)
						creaturePvpZones.Add(currentCreaturePvpZone.ToSummary());
					currentCreaturePvpZone = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "walk_parent" && elementPath.GetValueOrDefault(1) == "walker_versions")
					currentWalkerParentRouteId = string.Empty;

				if (reader.Depth == currentPetTemplateDepth && reader.LocalName == "pet" && currentPetTemplate != null)
				{
					petTemplates.Add(currentPetTemplate.ToSummary());
					currentPetTemplate = null;
					currentPetTemplateDepth = -1;
				}

				if (reader.Depth == currentTradeListTemplateDepth && currentTradeListTemplate != null)
				{
					AddTradeListTemplate(
						currentTradeListTemplate.ToSummary(),
						currentTradeListTemplateKind,
						tradeLists,
						tradeInLists,
						purchaseLists);
					currentTradeListTemplate = null;
					currentTradeListTemplateDepth = -1;
				}

				if (reader.Depth == currentGoodsListDepth && currentGoodsList != null)
				{
					AddGoodsListSummary(
						currentGoodsList.ToSummary(),
						currentGoodsListKind,
						goodsLists,
						goodsInLists,
						goodsPurchaseLists);
					currentGoodsList = null;
					currentGoodsListDepth = -1;
				}

				if (reader.Depth == currentNpcSpawnDepth && reader.LocalName == "spawn")
				{
					currentNpcSpawn = null;
					currentNpcSpawnDepth = -1;
				}

				if (reader.Depth == currentNpcSpawnSpotDepth && reader.LocalName == "spot" && currentNpcSpawn != null && currentNpcSpawnSpot != null)
				{
					npcSpawns.Add(currentNpcSpawn.ToSummary(currentNpcSpawnSpot));
					currentNpcSpawnSpot = null;
					currentNpcSpawnSpotDepth = -1;
				}

				if (reader.Depth == currentNpcRiftSpawnGroupDepth && reader.LocalName == "spawn")
				{
					currentNpcRiftSpawn = null;
					currentNpcRiftSpawnGroupDepth = -1;
				}

				if (reader.Depth == currentNpcRiftSpawnSpotDepth && reader.LocalName == "spot" && currentNpcRiftSpawn != null && currentNpcRiftSpawnSpot != null)
				{
					npcRiftSpawns.Add(currentNpcRiftSpawn.ToSummary(currentNpcRiftSpawnSpot));
					currentNpcRiftSpawnSpot = null;
					currentNpcRiftSpawnSpotDepth = -1;
				}

				if (reader.Depth == currentNpcRiftSpawnDepth && reader.LocalName == "rift_spawn")
				{
					currentNpcRiftSpawnId = 0;
					currentNpcRiftSpawnDepth = -1;
					currentNpcRiftSpawnGroupIndex = 0;
				}

				if (reader.Depth == currentNpcVortexSpawnGroupDepth && reader.LocalName == "spawn")
				{
					currentNpcVortexSpawn = null;
					currentNpcVortexSpawnGroupDepth = -1;
				}

				if (reader.Depth == currentNpcVortexSpawnSpotDepth && reader.LocalName == "spot" && currentNpcVortexSpawn != null && currentNpcVortexSpawnSpot != null)
				{
					npcVortexSpawns.Add(currentNpcVortexSpawn.ToSummary(currentNpcVortexSpawnSpot));
					currentNpcVortexSpawnSpot = null;
					currentNpcVortexSpawnSpotDepth = -1;
				}

				if (reader.Depth == currentNpcVortexSpawnStateDepth && reader.LocalName == "state_type")
				{
					currentNpcVortexSpawnStateType = default;
					currentNpcVortexSpawnStateDepth = -1;
				}

				if (reader.Depth == currentNpcVortexSpawnDepth && reader.LocalName == "vortex_spawn")
				{
					currentNpcVortexSpawnId = 0;
					currentNpcVortexSpawnDepth = -1;
					currentNpcVortexSpawnGroupIndex = 0;
				}

				if (reader.Depth == 2 && reader.LocalName == "quest" && currentQuestDropBuilder != null)
				{
					questDrops.AddRange(currentQuestDropBuilder.ToQuestDrops());
					currentQuestDropBuilder = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "spawn_map" && elementPath.GetValueOrDefault(1) == "spawns")
					currentNpcSpawnMapId = 0;

				if (reader.Depth == 2 && reader.LocalName == "world" && elementPath.GetValueOrDefault(1) == "staticdoor_templates")
					currentStaticDoorWorldId = 0;

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

				if (reader.LocalName == "npc_skill" && currentNpcSkillList != null && currentNpcSkill != null)
				{
					currentNpcSkillList.AddSkill(currentNpcSkill.ToSummary());
					currentNpcSkill = null;
				}

				if (reader.LocalName == "npc_skills" && currentNpcSkillList != null)
				{
					npcSkillLists.Add(currentNpcSkillList.ToSummary());
					currentNpcSkillList = null;
				}

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
					var flags = WorldMapSummary.ParseFlags(reader.GetAttribute("flags"));
					worldMaps.Add(new WorldMapSummary(
						mapId,
						isInstance,
						twinCount,
						reader.GetAttribute("drop_type") ?? "NONE",
						flags,
						reader.GetAttribute("world_type") ?? "NONE"));
				}
			}

			if (reader.Depth == 2
				&& reader.LocalName == "cleanup"
				&& elementPath.GetValueOrDefault(1) == "item_restriction_cleanups")
			{
				itemRestrictionCleanups.Add(new ItemRestrictionCleanupSummary(
					ReadRequiredIntAttribute(reader, "id"),
					(sbyte)ReadOptionalIntAttribute(reader, "trade", -1),
					(sbyte)ReadOptionalIntAttribute(reader, "sell", -1),
					(sbyte)ReadOptionalIntAttribute(reader, "wh", -1),
					(sbyte)ReadOptionalIntAttribute(reader, "awh", -1),
					(sbyte)ReadOptionalIntAttribute(reader, "lwh", -1)));
				continue;
			}

			if (reader.Depth == 2
				&& elementPath.GetValueOrDefault(1) == "npc_trade_list"
				&& TryGetTradeListTemplateKind(reader.LocalName, out var tradeListKind))
			{
				// Java parity: dataholders/TradeListData JAXB templates indexed by npc_id after unmarshal.
				currentTradeListTemplate = new TradeListTemplateBuilder(
					ReadRequiredIntAttribute(reader, "npc_id"),
					reader.GetAttribute("npc_type") ?? "NORMAL",
					ReadOptionalIntAttribute(reader, "sell_price_rate", 100),
					ReadOptionalIntAttribute(reader, "sell_price_rate2", 100),
					ReadOptionalIntAttribute(reader, "ap_sell_price_rate2", 100),
					ReadIntAttribute(reader, "buy_price_rate"),
					ReadIntAttribute(reader, "save_count"));
				currentTradeListTemplateKind = tradeListKind;
				currentTradeListTemplateDepth = reader.Depth;
				if (reader.IsEmptyElement)
				{
					AddTradeListTemplate(
						currentTradeListTemplate.ToSummary(),
						currentTradeListTemplateKind,
						tradeLists,
						tradeInLists,
						purchaseLists);
					currentTradeListTemplate = null;
					currentTradeListTemplateDepth = -1;
				}

				continue;
			}

			if (currentTradeListTemplate != null
				&& reader.Depth == currentTradeListTemplateDepth + 1
				&& reader.LocalName == "tradelist")
			{
				// Java parity: model/templates/tradelist/TradeTab stores the referenced goods-list id.
				currentTradeListTemplate.AddGoodsListId(ReadRequiredIntAttribute(reader, "id"));
				continue;
			}

			if (reader.Depth == 2
				&& elementPath.GetValueOrDefault(1) == "goodslists"
				&& reader.LocalName is "list" or "in_list" or "purchase_list")
			{
				// Java parity: dataholders/GoodsListData separates ordinary, trade-in, and purchase lists by element name.
				currentGoodsList = new GoodsListBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					ReadIntAttribute(reader, "legion_lvl"));
				currentGoodsListKind = reader.LocalName switch
				{
					"in_list" => GoodsListKind.InList,
					"purchase_list" => GoodsListKind.PurchaseList,
					_ => GoodsListKind.List,
				};
				currentGoodsListDepth = reader.Depth;
				if (reader.IsEmptyElement)
				{
					AddGoodsListSummary(
						currentGoodsList.ToSummary(),
						currentGoodsListKind,
						goodsLists,
						goodsInLists,
						goodsPurchaseLists);
					currentGoodsList = null;
					currentGoodsListDepth = -1;
				}

				continue;
			}

			if (currentGoodsList != null
				&& reader.Depth == currentGoodsListDepth + 1
				&& reader.LocalName == "item")
			{
				// Java parity: model/templates/goods/GoodsList.Item stores optional sell_limit and buy_limit.
				currentGoodsList.AddItem(new GoodsListItemSummary(
					ReadRequiredIntAttribute(reader, "id"),
					ReadNullableIntAttribute(reader, "sell_limit"),
					ReadNullableIntAttribute(reader, "buy_limit")));
				continue;
			}

			if (currentGoodsList != null
				&& reader.Depth == currentGoodsListDepth + 1
				&& reader.LocalName == "salestime")
			{
				// Java parity: model/templates/goods/GoodsList.salestime is passed into LimitedItem.
				currentGoodsList.SalesTime = await ReadElementTextAsync(reader, cancellationToken);
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "instance_exit")
			{
				// Java parity: model/templates/portal/InstanceExit scalar JAXB attributes, race defaults to PC_ALL.
				instanceExits.Add(
					new InstanceExitSummary(
						ReadRequiredIntAttribute(reader, "instance_id"),
						ReadRequiredIntAttribute(reader, "exit_world"),
						reader.GetAttribute("race") ?? "PC_ALL",
						ReadFloatAttribute(reader, "x"),
						ReadFloatAttribute(reader, "y"),
						ReadFloatAttribute(reader, "z"),
						(byte)ReadIntAttribute(reader, "h")));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "portal_loc")
			{
				// Java parity: model/templates/portal/PortalLoc scalar JAXB attributes consumed by PortalLocData.
				portalLocs.Add(
					new PortalLocSummary(
						ReadIntAttribute(reader, "world_id"),
						ReadIntAttribute(reader, "loc_id"),
						ReadFloatAttribute(reader, "x"),
						ReadFloatAttribute(reader, "y"),
						ReadFloatAttribute(reader, "z"),
						(byte)ReadIntAttribute(reader, "h")));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "auto_group" && elementPath.GetValueOrDefault(1) == "auto_groups")
			{
				// Java parity: model/autogroup/AutoGroup JAXB scalar attributes used by AutoGroupData.
				autoGroups.Add(
					new AutoGroupSummary(
						ReadRequiredIntAttribute(reader, "id"),
						ReadRequiredIntAttribute(reader, "instanceId"),
						ReadIntAttribute(reader, "name_id"),
						ReadIntAttribute(reader, "title_id"),
						ReadIntAttribute(reader, "min_lvl"),
						ReadIntAttribute(reader, "max_lvl"),
						ReadBoolAttribute(reader, "register_quick"),
						ReadBoolAttribute(reader, "register_group"),
						ReadBoolAttribute(reader, "register_new"),
						ReadXmlIntListAttribute(reader, "npc_ids")));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "zone" && elementPath.GetValueOrDefault(1) == "zones")
			{
				currentFlightZone = FlightZoneBuilder.TryCreate(reader);
				currentCreaturePvpZone = CreaturePvpZoneBuilder.TryCreate(reader);
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "points" && (currentFlightZone != null || currentCreaturePvpZone != null))
			{
				var bottom = ReadFloatAttribute(reader, "bottom");
				var top = ReadFloatAttribute(reader, "top");
				currentFlightZone?.SetVerticalBounds(bottom, top);
				currentCreaturePvpZone?.SetVerticalBounds(bottom, top);
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "point" && (currentFlightZone != null || currentCreaturePvpZone != null))
			{
				var x = ReadFloatAttribute(reader, "x");
				var y = ReadFloatAttribute(reader, "y");
				currentFlightZone?.AddPoint(x, y);
				currentCreaturePvpZone?.AddPoint(x, y);
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "spawn_map" && elementPath.GetValueOrDefault(1) == "spawns")
			{
				currentNpcSpawnMapId = ReadRequiredIntAttribute(reader, "map_id");
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "world" && elementPath.GetValueOrDefault(1) == "staticdoor_templates")
			{
				currentStaticDoorWorldId = ReadRequiredIntAttribute(reader, "world");
				continue;
			}

			if (currentStaticDoorWorldId != 0
				&& reader.Depth == 3
				&& reader.LocalName == "staticdoor"
				&& elementPath.GetValueOrDefault(2) == "world")
			{
				// Java parity: dataholders/StaticDoorData indexes StaticDoorTemplate entries by world and static door id.
				staticDoors.Add(new StaticDoorSummary(
					currentStaticDoorWorldId,
					ReadRequiredIntAttribute(reader, "id"),
					ReadIntAttribute(reader, "keyid"),
					ReadFloatAttribute(reader, "x"),
					ReadFloatAttribute(reader, "y"),
					ReadFloatAttribute(reader, "z"),
					ReadIntAttribute(reader, "state")));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "rift_location" && elementPath.GetValueOrDefault(1) == "rift_locations")
			{
				// Java parity: dataholders/RiftData converts every RiftTemplate into a RiftLocation keyed by id.
				var autoCloseableAttribute = reader.GetAttribute("auto_closeable");
				riftLocations.Add(
					new RiftLocationSummary(
						ReadRequiredIntAttribute(reader, "id"),
						ReadRequiredIntAttribute(reader, "world"),
						ReadBoolAttribute(reader, "has_spawns"),
						string.IsNullOrEmpty(autoCloseableAttribute) || bool.TryParse(autoCloseableAttribute, out var autoCloseable) && autoCloseable));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "npc_faction" && elementPath.GetValueOrDefault(1) == "npc_factions")
			{
				npcFactions.Add(
					new NpcFactionSummary(
						ReadRequiredIntAttribute(reader, "id"),
						reader.GetAttribute("name") ?? string.Empty,
						ReadIntAttribute(reader, "name_id"),
						reader.GetAttribute("category") ?? string.Empty,
						ReadIntAttribute(reader, "min_level"),
						ReadOptionalIntAttribute(reader, "max_level", 99),
						reader.GetAttribute("race") ?? string.Empty,
						ReadXmlIntListAttribute(reader, "npc_ids"),
						ReadIntAttribute(reader, "skill_points")));
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "quest" && elementPath.GetValueOrDefault(1) == "quests")
			{
				// Java parity: questEngine/Aion.GameServer.QuestEngine.QuestEngine.init transfers QuestTemplate.questDrop entries into QuestService by NPC id.
				currentQuestDropBuilder = new QuestDropBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					reader.GetAttribute("target") ?? "NONE",
					reader.GetAttribute("mentor_type") ?? "NONE");
				if (reader.IsEmptyElement)
				{
					questDrops.AddRange(currentQuestDropBuilder.ToQuestDrops());
					currentQuestDropBuilder = null;
				}
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "quest_drop" && currentQuestDropBuilder != null)
			{
				// Java parity: model/templates/quest/QuestDrop defaults chance to 100 and collecting_step/drop_each_member to 0.
				currentQuestDropBuilder.AddQuestDrop(
					ReadRequiredIntAttribute(reader, "npc_id"),
					ReadRequiredIntAttribute(reader, "item_id"),
					ReadOptionalIntAttribute(reader, "chance", 100),
					ReadIntAttribute(reader, "drop_each_member"),
					ReadIntAttribute(reader, "collecting_step"));
				continue;
			}

			if (reader.Depth == 4
				&& reader.LocalName == "collect_item"
				&& currentQuestDropBuilder != null
				&& elementPath.GetValueOrDefault(3) == "collect_items")
			{
				// Java parity: QuestService.isQuestDrop checks CollectItems before granting quest-drop loot.
				currentQuestDropBuilder.AddCollectItem(
					ReadRequiredIntAttribute(reader, "item_id"),
					ReadOptionalIntAttribute(reader, "count", 1));
				continue;
			}

			if (currentNpcSpawnMapId != 0
				&& reader.LocalName == "rift_spawn"
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spawn_map")
			{
				// Java parity: dataholders/SpawnsData.addRiftSpawns groups rift_spawn entries by id before RiftManager indexes their anchors.
				currentNpcRiftSpawnId = ReadRequiredIntAttribute(reader, "id");
				currentNpcRiftSpawnDepth = reader.Depth;
				currentNpcRiftSpawnGroupIndex = 0;
				continue;
			}

			if (currentNpcSpawnMapId != 0
				&& reader.LocalName == "vortex_spawn"
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spawn_map")
			{
				// Java parity: dataholders/SpawnsData.addVortexSpawns groups vortex_spawn entries by location id.
				currentNpcVortexSpawnId = ReadRequiredIntAttribute(reader, "id");
				currentNpcVortexSpawnDepth = reader.Depth;
				currentNpcVortexSpawnGroupIndex = 0;
				continue;
			}

			if (currentNpcVortexSpawnDepth != -1
				&& reader.LocalName == "state_type"
				&& reader.Depth == currentNpcVortexSpawnDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "vortex_spawn")
			{
				// Java parity: model/templates/spawns/vortexspawns/VortexSpawn.VortexStateTemplate state attribute.
				currentNpcVortexSpawnStateType = ReadVortexStateTypeAttribute(reader, "state");
				currentNpcVortexSpawnStateDepth = reader.Depth;
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "walk_parent" && elementPath.GetValueOrDefault(1) == "walker_versions")
			{
				// Java parity: dataholders/WalkerVersionsData groups route variants by parent route id.
				currentWalkerParentRouteId = reader.GetAttribute("id") ?? string.Empty;
				continue;
			}

			if (reader.Depth == 3
				&& reader.LocalName == "version"
				&& elementPath.GetValueOrDefault(2) == "walk_parent"
				&& !string.IsNullOrEmpty(currentWalkerParentRouteId))
			{
				var versionRouteId = reader.GetAttribute("id");
				if (!string.IsNullOrWhiteSpace(versionRouteId))
					walkerVersionParents[versionRouteId] = currentWalkerParentRouteId;
				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "walker_template" && elementPath.GetValueOrDefault(1) == "npc_walker")
			{
				// Java parity: dataholders/WalkerData loads npc_walker WalkerTemplate routes by route_id.
				currentWalkerTemplate = new WalkerTemplateBuilder(
					reader.GetAttribute("route_id") ?? string.Empty,
					ReadOptionalIntAttribute(reader, "pool", 1),
					reader.GetAttribute("formation") ?? "POINT",
					reader.GetAttribute("loop_type") ?? "NORMAL",
					reader.GetAttribute("rows") ?? string.Empty);
				if (reader.IsEmptyElement)
				{
					walkerTemplates.Add(currentWalkerTemplate.ToSummary());
					currentWalkerTemplate = null;
				}
				continue;
			}

			if (reader.Depth == 3
				&& reader.LocalName == "routestep"
				&& currentWalkerTemplate != null
				&& elementPath.GetValueOrDefault(2) == "walker_template")
			{
				// Java parity: model/templates/walker/RouteStep x/y/z/rest_time route points.
				currentWalkerTemplate.AddRouteStep(
					ReadFloatAttribute(reader, "x"),
					ReadFloatAttribute(reader, "y"),
					ReadFloatAttribute(reader, "z"),
					ReadIntAttribute(reader, "rest_time"));
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
					(byte)ReadIntAttribute(reader, "difficult_id"),
					reader.GetAttribute("handler") ?? string.Empty,
					ReadBoolAttribute(reader, "custom"));
				currentNpcSpawnDepth = reader.Depth;
				continue;
			}

			if (currentNpcRiftSpawnId != 0
				&& reader.LocalName == "spawn"
				&& reader.Depth == currentNpcRiftSpawnDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "rift_spawn")
			{
				// Java parity: model/templates/spawns/riftspawns/RiftSpawn nested Spawn groups keep ordinary spawn metadata plus the rift id.
				currentNpcRiftSpawn = new NpcRiftSpawnBuilder(
					currentNpcSpawnMapId,
					currentNpcRiftSpawnId,
					currentNpcRiftSpawnGroupIndex++,
					ReadRequiredIntAttribute(reader, "npc_id"),
					ReadIntAttribute(reader, "respawn_time"),
					ReadIntAttribute(reader, "pool"));
				currentNpcRiftSpawnGroupDepth = reader.Depth;
				continue;
			}

			if (currentNpcVortexSpawnStateDepth != -1
				&& reader.LocalName == "spawn"
				&& reader.Depth == currentNpcVortexSpawnStateDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "state_type")
			{
				// Java parity: SpawnGroup(worldId, spawn, id, VortexStateType) keeps ordinary spawn metadata plus vortex location/state.
				currentNpcVortexSpawn = new NpcVortexSpawnBuilder(
					currentNpcSpawnMapId,
					currentNpcVortexSpawnId,
					currentNpcVortexSpawnGroupIndex++,
					currentNpcVortexSpawnStateType,
					ReadRequiredIntAttribute(reader, "npc_id"),
					ReadIntAttribute(reader, "respawn_time"),
					ReadIntAttribute(reader, "pool"),
					(byte)ReadIntAttribute(reader, "difficult_id"),
					reader.GetAttribute("handler") ?? string.Empty,
					ReadBoolAttribute(reader, "custom"));
				currentNpcVortexSpawnGroupDepth = reader.Depth;
				continue;
			}

			if (currentNpcSpawn != null
				&& reader.LocalName == "temporary_spawn"
				&& reader.Depth == currentNpcSpawnDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spawn")
			{
				// Java parity: model/templates/spawns/Spawn.temporary_spawn registers a group with TemporarySpawnEngine.
				currentNpcSpawn.TemporarySchedule = TemporarySpawnSchedule.FromAttributes(
					reader.GetAttribute("weekdays"),
					reader.GetAttribute("spawn_time"),
					reader.GetAttribute("despawn_time"));
				continue;
			}

			if (currentNpcVortexSpawn != null
				&& reader.LocalName == "temporary_spawn"
				&& reader.Depth == currentNpcVortexSpawnGroupDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spawn")
			{
				// Java parity: Vortex Spawn still uses ordinary Spawn.temporary_spawn metadata.
				currentNpcVortexSpawn.TemporarySchedule = TemporarySpawnSchedule.FromAttributes(
					reader.GetAttribute("weekdays"),
					reader.GetAttribute("spawn_time"),
					reader.GetAttribute("despawn_time"));
				continue;
			}

			if (currentNpcSpawn != null
				&& reader.LocalName == "spot"
				&& reader.Depth == currentNpcSpawnDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spawn")
			{
				// Java parity: model/templates/spawns/SpawnSpotTemplate spot coordinates and movement metadata.
				currentNpcSpawnSpot = NpcSpawnSpotBuilder.FromReader(reader);
				currentNpcSpawnSpotDepth = reader.Depth;
				if (reader.IsEmptyElement)
				{
					npcSpawns.Add(currentNpcSpawn.ToSummary(currentNpcSpawnSpot));
					currentNpcSpawnSpot = null;
					currentNpcSpawnSpotDepth = -1;
				}
				continue;
			}

			if (currentNpcVortexSpawn != null
				&& reader.LocalName == "spot"
				&& reader.Depth == currentNpcVortexSpawnGroupDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spawn")
			{
				// Java parity: VortexSpawnTemplate inherits SpawnSpotTemplate coordinate and movement metadata.
				currentNpcVortexSpawnSpot = NpcSpawnSpotBuilder.FromReader(reader);
				currentNpcVortexSpawnSpotDepth = reader.Depth;
				if (reader.IsEmptyElement)
				{
					npcVortexSpawns.Add(currentNpcVortexSpawn.ToSummary(currentNpcVortexSpawnSpot));
					currentNpcVortexSpawnSpot = null;
					currentNpcVortexSpawnSpotDepth = -1;
				}
				continue;
			}

			if (currentNpcRiftSpawn != null
				&& reader.LocalName == "spot"
				&& reader.Depth == currentNpcRiftSpawnGroupDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spawn")
			{
				// Java parity: RiftSpawnTemplate extends SpawnTemplate, preserving spot anchor metadata for RiftManager.
				currentNpcRiftSpawnSpot = NpcSpawnSpotBuilder.FromReader(reader);
				currentNpcRiftSpawnSpotDepth = reader.Depth;
				if (reader.IsEmptyElement)
				{
					npcRiftSpawns.Add(currentNpcRiftSpawn.ToSummary(currentNpcRiftSpawnSpot));
					currentNpcRiftSpawnSpot = null;
					currentNpcRiftSpawnSpotDepth = -1;
				}
				continue;
			}

			if (currentNpcSpawnSpot != null
				&& reader.LocalName == "temporary_spawn"
				&& reader.Depth == currentNpcSpawnSpotDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spot")
			{
				// Java parity: model/templates/spawns/SpawnSpotTemplate.temporary_spawn gates only this spot.
				currentNpcSpawnSpot.TemporarySchedule = TemporarySpawnSchedule.FromAttributes(
					reader.GetAttribute("weekdays"),
					reader.GetAttribute("spawn_time"),
					reader.GetAttribute("despawn_time"));
				continue;
			}

			if (currentNpcVortexSpawnSpot != null
				&& reader.LocalName == "temporary_spawn"
				&& reader.Depth == currentNpcVortexSpawnSpotDepth + 1
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "spot")
			{
				// Java parity: Vortex SpawnSpotTemplate can carry spot-local temporary spawn metadata.
				currentNpcVortexSpawnSpot.TemporarySchedule = TemporarySpawnSchedule.FromAttributes(
					reader.GetAttribute("weekdays"),
					reader.GetAttribute("spawn_time"),
					reader.GetAttribute("despawn_time"));
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

			if (reader.Depth == 2
				&& reader.LocalName == "item_purification"
				&& elementPath.GetValueOrDefault(1) == "item_purifications")
			{
				// Java parity: dataholders/ItemPurificationData maps base_item_id to possible result items.
				currentItemPurificationBaseItemId = ReadRequiredIntAttribute(reader, "base_item_id");
				currentItemPurificationResults = [];
				if (reader.IsEmptyElement)
				{
					itemPurifications.Add(new ItemPurificationSummary(
						currentItemPurificationBaseItemId,
						currentItemPurificationResults.AsReadOnly()));
					currentItemPurificationBaseItemId = 0;
					currentItemPurificationResults = null;
				}

				continue;
			}

			if (reader.Depth == 3
				&& reader.LocalName == "purification_result"
				&& currentItemPurificationResults != null)
			{
				// Java parity: model/templates/item/purification/PurificationResult JAXB scalar attributes.
				currentItemPurificationResult = new ItemPurificationResultBuilder(
					ReadRequiredIntAttribute(reader, "result_item_id"),
					ReadIntAttribute(reader, "min_enchant_count"),
					ReadIntAttribute(reader, "necessary_abyss_points"),
					ReadLongAttribute(reader, "necessary_kinah"));
				if (reader.IsEmptyElement)
				{
					currentItemPurificationResults.Add(currentItemPurificationResult.ToSummary());
					currentItemPurificationResult = null;
				}

				continue;
			}

			if (reader.Depth == 4
				&& reader.LocalName == "req_material"
				&& currentItemPurificationResult != null)
			{
				// Java parity: model/templates/item/purification/RequiredMaterial item_id/item_count.
				currentItemPurificationResult.AddRequiredMaterial(
					new ItemPurificationMaterialSummary(
						ReadRequiredIntAttribute(reader, "item_id"),
						ReadLongAttribute(reader, "item_count")));
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

			if (reader.Depth == 3 && reader.LocalName == "type" && currentInstanceCooltime != null)
			{
				currentInstanceCooltime.CoolTimeType = await ReadElementTextAsync(reader, cancellationToken);
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "typevalue" && currentInstanceCooltime != null)
			{
				currentInstanceCooltime.TypeValue = await ReadElementTextAsync(reader, cancellationToken);
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "ent_cool_time" && currentInstanceCooltime != null)
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentInstanceCooltime.EntCoolTime = int.TryParse(value, out var parsedEntCoolTime) ? parsedEntCoolTime : 0;
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "maxcount" && currentInstanceCooltime != null)
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentInstanceCooltime.MaxCount = int.TryParse(value, out var parsedMaxCount) ? parsedMaxCount : 0;
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "max_member_light" && currentInstanceCooltime != null)
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentInstanceCooltime.MaxMemberLight = int.TryParse(value, out var parsedMaxMemberLight) ? parsedMaxMemberLight : 0;
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "max_member_dark" && currentInstanceCooltime != null)
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentInstanceCooltime.MaxMemberDark = int.TryParse(value, out var parsedMaxMemberDark) ? parsedMaxMemberDark : 0;
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "enter_min_level_light" && currentInstanceCooltime != null)
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentInstanceCooltime.EnterMinLevelLight = int.TryParse(value, out var parsedEnterMinLevelLight) ? parsedEnterMinLevelLight : 0;
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "enter_max_level_light" && currentInstanceCooltime != null)
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentInstanceCooltime.EnterMaxLevelLight = int.TryParse(value, out var parsedEnterMaxLevelLight) ? parsedEnterMaxLevelLight : 0;
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "enter_min_level_dark" && currentInstanceCooltime != null)
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentInstanceCooltime.EnterMinLevelDark = int.TryParse(value, out var parsedEnterMinLevelDark) ? parsedEnterMinLevelDark : 0;
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "enter_max_level_dark" && currentInstanceCooltime != null)
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentInstanceCooltime.EnterMaxLevelDark = int.TryParse(value, out var parsedEnterMaxLevelDark) ? parsedEnterMaxLevelDark : 0;
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "can_enter_mentor" && currentInstanceCooltime != null)
			{
				var value = await ReadElementTextAsync(reader, cancellationToken);
				currentInstanceCooltime.CanEnterMentor = bool.TryParse(value, out var parsedCanEnterMentor) && parsedCanEnterMentor;
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
					ReadIntAttribute(reader, "max_tampering"),
					ReadIntAttribute(reader, "weapon_boost"));
				if (reader.IsEmptyElement)
				{
					itemTemplates.Add(currentItemTemplate.ToSummary());
					currentItemTemplate = null;
				}

				continue;
			}

			if (reader.Depth == 2
				&& reader.LocalName == "expansion_npc"
				&& elementPath.GetValueOrDefault(1) is "cube_expander" or "warehouse_expander")
			{
				// Java parity: dataholders/CubeExpandData and WarehouseExpandData afterUnmarshal flatten ids to template lookup maps.
				currentStorageExpansionNpcIds = ReadIntListAttribute(reader, "ids").ToList();
				currentStorageExpansionPrices = [];
				currentStorageExpansionIsCube = elementPath.GetValueOrDefault(1) == "cube_expander";
				if (reader.IsEmptyElement)
				{
					var summary = new StorageExpansionTemplateSummary(
						currentStorageExpansionNpcIds.AsReadOnly(),
						currentStorageExpansionPrices.AsReadOnly());
					if (currentStorageExpansionIsCube)
						cubeExpansionTemplates.Add(summary);
					else
						warehouseExpansionTemplates.Add(summary);
					currentStorageExpansionNpcIds = null;
					currentStorageExpansionPrices = null;
				}

				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "expand" && currentStorageExpansionPrices != null)
			{
				// Java parity: model/templates/expand/Expand level/price attributes.
				currentStorageExpansionPrices.Add(
					new StorageExpansionPrice(
						ReadRequiredIntAttribute(reader, "level"),
						ReadRequiredIntAttribute(reader, "price")));
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
				// Java parity: model/templates/item/Acquisition is consumed by AP extraction and TradeList.calculateAbyssRewardBuyList.
				currentItemTemplate.RequiredAbyssPoints = ReadIntAttribute(reader, "ap");
				currentItemTemplate.AcquisitionType = reader.GetAttribute("type") ?? string.Empty;
				currentItemTemplate.AcquisitionItemId = ReadIntAttribute(reader, "item");
				currentItemTemplate.AcquisitionItemCount = ReadIntAttribute(reader, "count");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "ride" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/RideAction.npcId.
				currentItemTemplate.RideNpcId = ReadIntAttribute(reader, "npc_id");
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "toypetspawn" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/ToyPetSpawnAction.npcid/time.
				currentItemTemplate.ToyPetSpawnNpcId = ReadIntAttribute(reader, "npcid");
				currentItemTemplate.ToyPetSpawnTime = ReadIntAttribute(reader, "time");
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

			if (reader.Depth == 2
				&& reader.LocalName == "legion_dominion_location"
				&& elementPath.GetValueOrDefault(1) == "legion_dominion_template")
			{
				// Java parity: model/templates/LegionDominionLocationTemplate id/name_id used by L10n.getL10n.
				legionDominions.Add(new LegionDominionLocationSummary(
					ReadRequiredIntAttribute(reader, "id"),
					ReadRequiredIntAttribute(reader, "name_id")));
				continue;
			}

			if (reader.Depth == 2
				&& reader.LocalName == "login_event"
				&& elementPath.GetValueOrDefault(1) == "login_events")
			{
				// Java parity: model/templates/event/AtreianPassport JAXB attributes.
				atreianPassports.Add(new AtreianPassportSummary(
					ReadRequiredIntAttribute(reader, "id"),
					ReadXmlBoolAttribute(reader, "active"),
					ReadRequiredDateTimeAttribute(reader, "period_start"),
					ReadRequiredDateTimeAttribute(reader, "period_end"),
					reader.GetAttribute("attend_type") ?? throw new FormatException("login_event is missing required attend_type."),
					ReadOptionalIntAttribute(reader, "attend_num", 0),
					ReadRequiredIntAttribute(reader, "reward_item"),
					ReadRequiredIntAttribute(reader, "reward_item_num"),
					ReadOptionalIntAttribute(reader, "reward_item_expire_time", 0),
					ReadOptionalIntAttribute(reader, "reward_permit_level", 0)));
				continue;
			}

			if (reader.Depth == 2
				&& reader.LocalName == "pet"
				&& elementPath.GetValueOrDefault(1) == "pets")
			{
				// Java parity: dataholders/PetData indexes PetTemplate by pet id after static_data/pets/pets.xml unmarshalling.
				currentPetTemplate = new PetTemplateBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					reader.GetAttribute("name") ?? string.Empty,
					ReadIntAttribute(reader, "nameid"),
					ReadIntAttribute(reader, "condition_reward"));
				currentPetTemplateDepth = reader.Depth;
				if (reader.IsEmptyElement)
				{
					petTemplates.Add(currentPetTemplate.ToSummary());
					currentPetTemplate = null;
					currentPetTemplateDepth = -1;
				}

				continue;
			}

			if (reader.Depth == 2
				&& reader.LocalName == "doping"
				&& elementPath.GetValueOrDefault(1) == "dopings")
			{
				// Java parity: dataholders/PetDopingData indexes pet_doping.xml rows by id after unmarshalling.
				petDopings.Add(new PetDopingEntrySummary(
					ReadRequiredIntAttribute(reader, "id"),
					ReadRequiredBoolAttribute(reader, "usedrink"),
					ReadRequiredBoolAttribute(reader, "usefood"),
					ReadRequiredIntAttribute(reader, "usescroll")));
				continue;
			}

			if (currentPetTemplate != null
				&& reader.Depth == currentPetTemplateDepth + 1
				&& reader.LocalName == "petfunction")
			{
				// Java parity: model/templates/pet/PetFunction fields id/type/slots/rate_price.
				currentPetTemplate.AddFunction(new PetFunctionSummary(
					ReadIntAttribute(reader, "id"),
					ReadPetFunctionTypeAttribute(reader.GetAttribute("type")),
					ReadIntAttribute(reader, "slots"),
					ReadIntAttribute(reader, "rate_price")));
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "queststart" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/QuestStartAction.questid.
				currentItemTemplate.QuestStartQuestId = ReadIntAttribute(reader, "questid");
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

			if (reader.Depth == 4 && reader.LocalName == "tuning" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/TuningAction target/no_reduce metadata.
				currentItemTemplate.TuningAction = new ItemTuningActionInfo(
					ParseItemActionUseTargetType(reader.GetAttribute("target") ?? throw new FormatException("Missing required attribute 'target'.")),
					ReadBoolAttribute(reader, "no_reduce"));
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "tampering" && currentItemTemplate != null)
			{
				// Java parity: model/templates/item/actions/TamperingAction marker.
				currentItemTemplate.HasTamperingAction = true;
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

			if (reader.LocalName == "npc_skills" && elementPath.GetValueOrDefault(reader.Depth - 1) == "npc_skill_templates")
			{
				// Java parity: model/templates/npcskill/NpcSkillTemplates uses JAXB @XmlList npc_ids.
				currentNpcSkillList = new NpcSkillListBuilder(ReadXmlIntListAttribute(reader, "npc_ids"));
				if (reader.IsEmptyElement)
				{
					npcSkillLists.Add(currentNpcSkillList.ToSummary());
					currentNpcSkillList = null;
				}
				continue;
			}

			if (reader.LocalName == "npc_skill" && currentNpcSkillList != null)
			{
				// Java parity: model/templates/npcskill/NpcSkillTemplate scalar JAXB attributes and defaults.
				currentNpcSkill = new NpcSkillTemplateBuilder(
					ReadIntAttribute(reader, "id"),
					ReadIntAttribute(reader, "lv"),
					ReadIntAttribute(reader, "prob"),
					ReadOptionalIntAttribute(reader, "min_hp", 0),
					ReadOptionalIntAttribute(reader, "max_hp", 100),
					ReadOptionalIntAttribute(reader, "max_time", 0),
					ReadOptionalIntAttribute(reader, "min_time", 0),
					reader.GetAttribute("conjunction") ?? "AND",
					ReadOptionalIntAttribute(reader, "cd", 0),
					ReadBoolAttribute(reader, "is_post_spawn"),
					ReadOptionalIntAttribute(reader, "prio", 0),
					ReadOptionalIntAttribute(reader, "next_skill_time", -1),
					ReadOptionalIntAttribute(reader, "next_chain_id", 0),
					ReadOptionalIntAttribute(reader, "chain_id", 0),
					ReadOptionalIntAttribute(reader, "max_chain_time", 15000),
					reader.GetAttribute("target") ?? "MOST_HATED");
				if (reader.IsEmptyElement)
				{
					currentNpcSkillList.AddSkill(currentNpcSkill.ToSummary());
					currentNpcSkill = null;
				}
				continue;
			}

			if (reader.LocalName == "spawn_npc" && currentNpcSkill != null)
			{
				// Java parity: model/templates/npcskill/NpcSkillSpawn defaults min_count=1 and max_count=0.
				currentNpcSkill.Spawn = new NpcSkillSpawnSummary(
					ReadIntAttribute(reader, "npc_id"),
					ReadIntAttribute(reader, "delay"),
					ReadIntAttribute(reader, "min_distance"),
					ReadIntAttribute(reader, "max_distance"),
					ReadOptionalIntAttribute(reader, "min_count", 1),
					ReadOptionalIntAttribute(reader, "max_count", 0));
				continue;
			}

			if (reader.LocalName == "cond" && currentNpcSkill != null)
			{
				// Java parity: model/templates/npcskill/NpcSkillConditionTemplate JAXB defaults.
				currentNpcSkill.Condition = new NpcSkillConditionSummary(
					reader.GetAttribute("cond_type") ?? "NONE",
					ReadOptionalIntAttribute(reader, "hp_below", 50),
					ReadOptionalIntAttribute(reader, "range", 10),
					ReadIntAttribute(reader, "npc_id"),
					ReadIntAttribute(reader, "delay"),
					ReadOptionalBoolAttribute(reader, "can_die", true),
					ReadOptionalIntAttribute(reader, "despawn_time", 500));
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
				currentRecipeTemplate = new RecipeTemplateBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					ReadIntAttribute(reader, "nameid"),
					ReadIntAttribute(reader, "skillid"),
					reader.GetAttribute("race") ?? string.Empty,
					ReadIntAttribute(reader, "skillpoint"),
					ReadIntAttribute(reader, "dp"),
					ReadIntAttribute(reader, "autolearn"),
					ReadIntAttribute(reader, "productid"),
					ReadIntAttribute(reader, "quantity"),
					ReadNullableIntAttribute(reader, "craft_delay_id"),
					ReadNullableIntAttribute(reader, "craft_delay_time"),
					ReadNullableIntAttribute(reader, "max_production_count"));
				if (reader.IsEmptyElement)
				{
					recipeTemplates.Add(currentRecipeTemplate.ToSummary());
					currentRecipeTemplate = null;
				}
				continue;
			}

			if (reader.Depth == 3
				&& currentRecipeTemplate != null
				&& reader.LocalName == "components_data")
			{
				currentRecipeTemplate.BeginComponentData();
				if (reader.IsEmptyElement)
					currentRecipeTemplate.EndComponentData();
				continue;
			}

			if (reader.Depth == 4
				&& currentRecipeTemplate != null
				&& reader.LocalName == "component")
			{
				currentRecipeTemplate.AddComponent(
					ReadRequiredIntAttribute(reader, "itemid"),
					ReadLongAttribute(reader, "quantity"));
				continue;
			}

			if (reader.Depth == 3
				&& currentRecipeTemplate != null
				&& reader.LocalName == "comboproduct")
			{
				currentRecipeTemplate.AddComboProduct(ReadRequiredIntAttribute(reader, "itemid"));
				continue;
			}

			if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "pet_skill")
			{
				// Java parity: dataholders/PetSkillData afterUnmarshal indexes each PetSkillTemplate by order skill and pet id.
				petSkills.Add(new PetSkillSummary(
					ReadIntAttribute(reader, "skill_id"),
					ReadIntAttribute(reader, "pet_id"),
					ReadIntAttribute(reader, "order_skill")));
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
		var customNpcDrops = await CustomNpcDropTable.LoadFromImportedFilesAsync(importedFiles, cancellationToken);
		var workOrderRecipes = WorkOrderRecipeTable.LoadFromImportedFiles(importedFiles);

		return new StaticData(
			cacheFilePath,
			importedFiles,
			new ReadOnlyDictionary<string, int>(counts),
			topLevelElements.AsReadOnly(),
			worldMaps.AsReadOnly(),
			new FlightZoneTable(flightZones.AsReadOnly()),
			new CreaturePvpZoneTable(creaturePvpZones.AsReadOnly()),
			new PlayerExperienceTable(experience.AsReadOnly()),
			new ItemTemplateTable(itemTemplates.AsReadOnly(), learnableEmotionIds),
			new CosmeticItemTable(cosmeticItems.AsReadOnly()),
			new DecomposableItemTable(decomposableItems.AsReadOnly()),
			new AssemblyItemTable(assemblyItems.AsReadOnly()),
			new ItemPurificationTable(itemPurifications.AsReadOnly()),
			new ItemRestrictionCleanupTable(itemRestrictionCleanups.AsReadOnly()),
			new RideTable(rideInfos.AsReadOnly()),
			new ItemRandomBonusTable(itemRandomBonuses.AsReadOnly()),
			new ItemSetTable(itemSets.AsReadOnly()),
			new EnchantTable(enchantGroups.AsReadOnly()),
			new TemperingTable(temperingGroups.AsReadOnly()),
			new WalkerTemplateTable(walkerTemplates.AsReadOnly()),
			new WalkerVersionTable(new ReadOnlyDictionary<string, string>(walkerVersionParents)),
			new RiftLocationTable(riftLocations.AsReadOnly()),
			new NpcSpawnTable(npcSpawns.AsReadOnly()),
			new StaticDoorTable(staticDoors.AsReadOnly()),
			new NpcRiftSpawnTable(npcRiftSpawns.AsReadOnly()),
			new NpcVortexSpawnTable(npcVortexSpawns.AsReadOnly()),
			new NpcFactionTable(npcFactions.AsReadOnly()),
			new TradeListTable(
				tradeLists.AsReadOnly(),
				tradeInLists.AsReadOnly(),
				purchaseLists.AsReadOnly()),
			new GoodsListTable(
				goodsLists.AsReadOnly(),
				goodsInLists.AsReadOnly(),
				goodsPurchaseLists.AsReadOnly()),
			customNpcDrops,
			new QuestDropTable(questDrops.AsReadOnly()),
			new NpcSkillTable(npcSkillLists.AsReadOnly()),
			new PetSkillTable(petSkills.AsReadOnly()),
			new PetTemplateTable(petTemplates.AsReadOnly()),
			new PetDopingTable(petDopings.AsReadOnly()),
			new TitleTemplateTable(titleTemplates.AsReadOnly()),
			new RecipeTemplateTable(recipeTemplates.AsReadOnly()),
			workOrderRecipes,
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
			new InstanceExitTable(instanceExits.AsReadOnly()),
			new PortalLocTable(portalLocs.AsReadOnly()),
			new AutoGroupTable(autoGroups.AsReadOnly()),
			new PlayerInitialDataTable(
				creationItemsByClass.ToDictionary(
					pair => pair.Key,
					pair => new PlayerCreationData(pair.Key, pair.Value.AsReadOnly()),
					StringComparer.OrdinalIgnoreCase),
				spawnLocationsByRace),
			new StorageExpansionTemplateTable(cubeExpansionTemplates.AsReadOnly()),
			new StorageExpansionTemplateTable(warehouseExpansionTemplates.AsReadOnly()),
			new LegionDominionTable(legionDominions.AsReadOnly()),
			new AtreianPassportTable(atreianPassports.AsReadOnly()),
			validationTask);
	}

}
