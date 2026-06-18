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
		CreaturePvpZoneTable creaturePvpZones,
		PlayerExperienceTable playerExperienceTable,
		CosmeticItemTable cosmeticItems,
		DecomposableItemTable decomposableItems,
		AssemblyItemTable assemblyItems,
		ItemPurificationTable itemPurifications,
		ItemRestrictionCleanupTable itemRestrictionCleanups,
		RideTable rideInfos,
		ItemRandomBonusTable itemRandomBonuses,
		ItemSetTable itemSets,
		EnchantTable enchantTemplates,
		WalkerVersionTable walkerVersions,
		RiftLocationTable riftLocations,
		StaticDoorTable staticDoors,
		NpcFactionTable npcFactions,
		TradeListTable tradeLists,
		GoodsListTable goodsLists,
		QuestDropTable questDrops,
		NpcSkillTable npcSkills,
		PetSkillTable petSkills,
		PetDopingTable petDopings,
		WorkOrderRecipeTable workOrderRecipes,
		InstanceCooltimeTable instanceCooltimes,
		InstanceExitTable instanceExits,
		PortalLocTable portalLocs,
		AutoGroupTable autoGroups,
		PlayerInitialDataTable playerInitialData,
		LegionDominionTable legionDominions,
		AtreianPassportTable atreianPassports,
		Task? validationTask)
	{
		CacheFilePath = cacheFilePath;
		ImportedFiles = importedFiles;
		ElementCounts = elementCounts;
		TopLevelElements = topLevelElements;
		CreaturePvpZones = creaturePvpZones;
		PlayerExperienceTable = playerExperienceTable;
		CosmeticItems = cosmeticItems;
		DecomposableItems = decomposableItems;
		AssemblyItems = assemblyItems;
		ItemPurifications = itemPurifications;
		ItemRestrictionCleanups = itemRestrictionCleanups;
		RideInfos = rideInfos;
		ItemRandomBonuses = itemRandomBonuses;
		ItemSets = itemSets;
		EnchantTemplates = enchantTemplates;
		WalkerVersions = walkerVersions;
		RiftLocations = riftLocations;
		StaticDoors = staticDoors;
		NpcFactions = npcFactions;
		TradeLists = tradeLists;
		GoodsLists = goodsLists;
		QuestDrops = questDrops;
		NpcSkills = npcSkills;
		PetSkills = petSkills;
		PetDopings = petDopings;
		WorkOrderRecipes = workOrderRecipes;
		InstanceCooltimes = instanceCooltimes;
		InstanceExits = instanceExits;
		PortalLocs = portalLocs;
		AutoGroups = autoGroups;
		PlayerInitialData = playerInitialData;
		LegionDominions = legionDominions;
		AtreianPassports = atreianPassports;
		ValidationTask = validationTask;
	}

	public string CacheFilePath { get; }

	public IReadOnlyList<string> ImportedFiles { get; }

	public int ImportedFileCount => ImportedFiles.Count;

	public IReadOnlyDictionary<string, int> ElementCounts { get; }

	public IReadOnlyList<string> TopLevelElements { get; }

	public CreaturePvpZoneTable CreaturePvpZones { get; }

	public PlayerExperienceTable PlayerExperienceTable { get; }

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

	public WalkerVersionTable WalkerVersions { get; }

	public RiftLocationTable RiftLocations { get; }

	// Faithful VortexData holder (empty-default; runtime XML load deferred) - summary->template re-point.
	public VortexData VortexDataDh { get; private set; } = new();

	// Faithful RiftData holder (dataholders/RiftData) feeds DataManager.RIFT_DATA; loaded from rift/rift_locations.xml.
	public RiftData RiftDataDh { get; private set; } = new();

	public StaticDoorTable StaticDoors { get; }

	public NpcFactionTable NpcFactions { get; }

	public TradeListTable TradeLists { get; }

	public GoodsListTable GoodsLists { get; }

	public QuestDropTable QuestDrops { get; }

	public NpcSkillTable NpcSkills { get; }

	public PetSkillTable PetSkills { get; }

	public PetDopingTable PetDopings { get; }

	// Faithful PetFeedData holder — populated from pets/pet_feed.xml at boot (LoadLeafHoldersFromFiles).
	public PetFeedData PetFeedDataDh { get; private set; } = new();

	public WorkOrderRecipeTable WorkOrderRecipes { get; }

	public InstanceCooltimeTable InstanceCooltimes { get; }

	public InstanceExitTable InstanceExits { get; }

	public PortalLocTable PortalLocs { get; }

	public AutoGroupTable AutoGroups { get; }

	public PlayerInitialDataTable PlayerInitialData { get; }

	public CubeExpandData CubeExpandDataDh { get; private set; } = new();

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

	// Captured at boot by LoadLeafHoldersFromFiles so the operator-reload contract (admincommands/Reload)
	// can re-run the relevant leaf loaders against the same source tree. Java's Reload reads the fixed
	// "./data/static_data/..." paths directly; here the directory is whatever the loader was pointed at.
	public string? StaticDataDirectory { get; private set; }

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
	public SpawnsData SpawnsDh { get; private set; } = new();
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
	public StaticDoorData StaticDoorDataDh { get; private set; } = new();
	public SkillTreeData SkillTreeDataDh { get; private set; } = new();
	public DecomposableItemsData DecomposableItemsDataDh { get; private set; } = new();
	public ChallengeData ChallengeDataDh { get; private set; } = new();
	public TemperingData TemperingDataDh { get; private set; } = new();
	public Portal2Data Portal2DataDh { get; private set; } = new();
	public ItemRandomBonusData ItemRandomBonusDataDh { get; private set; } = new();
	public HouseData HouseDataDh { get; private set; } = new();
	public CustomDrop CustomNpcDropDh { get; private set; } = new();
	public HousingObjectData HousingObjectDataDh { get; private set; } = new();
	public PlayerInitialData PlayerInitialDataDh { get; private set; } = new();

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
		StaticDataDirectory = staticDataDirectory;
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
		// Java imports the spawns/ dir with singleRootTag="true" (every file is a <spawns> root of <spawn_map> rows,
		// recursive across Npcs/Instances/Bases/Rifts/Sieges/Mercenaries/Statics/Gather/AhserionsFlight). Merge every
		// file's spawn_map rows then run AfterUnmarshal once → SpawnsData.Initialize builds the regular/base/rift/siege/
		// vortex/mercenary/ahserion spawn maps. Spawn/SpawnSpotTemplate nullable attrs bind via string proxies; the
		// per-spawn-type named element lists (spawn/base_spawn/rift_spawn/siege_spawn/vortex_spawn/mercenary_spawn/
		// ahserion_spawn) + nested enum tokens (handler/occupier/race/mod/state/faction) are all covered (no silent drop).
		// Feeds DataManager.SPAWNS_DATA → SpawnEngine.SpawnAll spawns the world NPCs.
		SpawnsDh = TryLoadMergedHolder<SpawnsData>(Path.Combine(staticDataDirectory, "spawns"), (m, p) => m.MergePending(p), logger);
		// Java imports the events/timed_events/ dir (custom_events.xml + retail_events.xml), each its own
		// <timed_events> root; merge every file's <event> rows then run AfterUnmarshal once (validates dates +
		// fires each event's SpawnsData.Initialize children-first). EventTemplate's GlobalRule drop-rule cone
		// binds nullable restriction_race via a string proxy. Feeds DataManager.EVENT_DATA.
		Events = TryLoadMergedHolder<EventData>(Path.Combine(staticDataDirectory, "events", "timed_events"), (m, p) => m.MergePending(p), logger);
		// <warehouse_expander> root (single file): faithful WarehouseExpandData feeds DataManager.WAREHOUSEEXPANDER_DATA.
		// ExpandTemplate ids="..." space-separated int[] binds via the IdsRaw string proxy; <expand> rows public.
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
		// Java imports the single file staticdoors/staticdoor_templates.xml (<staticdoor_templates> root) and binds
		// it to StaticData.staticDoorData; feeds DataManager.STATICDOOR_DATA, read by StaticDoorSpawnManager to spawn
		// per-world static doors. Was a hollow new() -> always empty -> no static doors spawned. StaticDoorWorld/
		// StaticDoorTemplate primitive attrs bind via the now-public fields; StaticDoorData.AfterUnmarshal cascades
		// each StaticDoorWorld.AfterUnmarshal children-first (XmlSerializer doesn't auto-call nested callbacks).
		StaticDoorDataDh = TryLoadHolder(StaticDoorDataDh, Path.Combine(staticDataDirectory, "staticdoors", "staticdoor_templates.xml"), logger);
		// Java imports the skill_tree dir with singleRootTag (<import file="skill_tree" singleRootTag="true"/>):
		// skill_tree.xml + craft_skill_tree.xml both <skill_tree> roots, merged into StaticData.skillTreeData; feeds
		// DataManager.SKILL_TREE_DATA, read by StigmaService/SkillLearnService/PlayerSkillList for stigma + skill-learn.
		// Was a hollow new() -> always empty -> stigma/skill-learn trees silently empty. Each file's <skill> rows
		// (now-public skillTemplates) are merged via MergePending, then the single AfterUnmarshal builds the
		// per-class/race templates map + templatesById (XmlSerializer doesn't auto-fire JAXB's afterUnmarshal).
		SkillTreeDataDh = TryLoadMergedHolder<SkillTreeData>(Path.Combine(staticDataDirectory, "skill_tree"), (m, p) => m.MergePending(p), logger);
		// Java imports the single file decomposable_items/decomposable_items.xml (<decomposable_items> root) and binds
		// it to StaticData.decomposableItemsData; feeds DataManager.DECOMPOSABLE_ITEMS_DATA, read by DecomposeAction +
		// CM_SELECT_DECOMPOSABLE for item-decompose rewards. Was a hollow new() -> always empty -> decompose yielded
		// nothing. DecomposableItemInfo/ExtractedItemsCollection(:ResultedItemsCollection)/ResultedItem/RandomItem
		// primitive+enum attrs bind via the now-public fields; DecomposableItemsData.AfterUnmarshal cascades each
		// ResultedItem/RandomItem.AfterUnmarshal children-first (validates reward item ids vs live ITEM_DATA +
		// defaults/validates min/max counts) before the parent indexing (XmlSerializer doesn't auto-fire nested callbacks).
		DecomposableItemsDataDh = TryLoadDecomposableItems(Path.Combine(staticDataDirectory, "decomposable_items", "decomposable_items.xml"), ItemDataDh, logger);
		// Java imports the single file quest_data/challenge_tasks.xml (<challenge_tasks> root) and binds it to
		// StaticData.challengeData; feeds DataManager.CHALLENGE_DATA, read by ChallengeTaskService/ChallengeTasksDAO
		// for legion/town challenge tasks. Was a hollow new() -> always empty -> challenge tasks unavailable.
		// ChallengeTaskTemplate/ChallengeQuestTemplate/ContributionReward/ChallengeReward attrs bind via the now-
		// public fields; the nullable Integer attrs (prev_task/msg_id/value) bind via string proxies; race/type
		// enums bind by member name; ChallengeData.AfterUnmarshal (tasksById index) fires inside TryLoadHolder.
		ChallengeDataDh = TryLoadHolder(ChallengeDataDh, Path.Combine(staticDataDirectory, "quest_data", "challenge_tasks.xml"), logger);
		// Java imports the single file enchants/tempering_templates.xml (<tempering_templates> root) and binds it to
		// StaticData.temperingData; feeds DataManager.TEMPERING_DATA, read by TemperingEffect for the item-tempering
		// stat bonus. Was a hollow new() -> always empty -> tempering granted no stats. TemperingList/
		// TemperingTemplateData/TemperingStat primitive+enum (StatEnum by member name) attrs bind via the now-public
		// fields; TemperingData.AfterUnmarshal (item_group -> level -> stats map) fires inside TryLoadHolder.
		TemperingDataDh = TryLoadHolder(TemperingDataDh, Path.Combine(staticDataDirectory, "enchants", "tempering_templates.xml"), logger);
		// Java imports the single file portals/portal_template2.xml (<portal_templates2> root) and binds it to
		// StaticData.portalTemplate2; feeds DataManager.PORTAL2_DATA, read by TeleportService + the PortalAI handlers
		// (PortalAI/PortalDialogAI/LegionDominionPortalAI/BeshmundirsWalkAI/SealedDanuarMysticariumPortals). Was a
		// hollow new() -> always empty -> portals never teleported. PortalUse/PortalDialog/PortalScroll/PortalPath
		// (+ QuestReq/ItemReq) primitive attrs + Race (by member name, PC_ALL default) bind via the now-public fields;
		// Portal2Data.AfterUnmarshal builds the per-npcId/per-name indices inside TryLoadHolder.
		Portal2DataDh = TryLoadHolder(Portal2DataDh, Path.Combine(staticDataDirectory, "portals", "portal_template2.xml"), logger);
		// Java imports the single file items/item_random_bonuses.xml (<random_bonuses> root) and binds it to
		// StaticData.itemRandomBonuses; feeds DataManager.ITEM_RANDOM_BONUSES, read by ItemPurificationService/
		// PolishAction/TuningAction/RandomBonusEffect for item random-bonus rolls. Was a hollow new() -> always empty
		// -> random bonuses never rolled. RandomBonusSet (id/type=StatBonusType by member name) + the shared
		// ModifiersTemplate cone (polymorphic add/rate/sub/set/abs StatFunctions) bind via the now-public fields;
		// ItemRandomBonusData.AfterUnmarshal builds the per-StatBonusType id map inside TryLoadHolder.
		ItemRandomBonusDataDh = TryLoadHolder(ItemRandomBonusDataDh, Path.Combine(staticDataDirectory, "items", "item_random_bonuses.xml"), logger);
		// Java imports the single file housing/houses.xml (<house_lands> root) and binds it to StaticData.houseData;
		// feeds DataManager.HOUSE_DATA, read by TownService + HousingService (and HouseController/SM_HOUSE_* via
		// HouseAddress.GetLand()). Was a hollow new() -> always empty -> no house addresses/lands. HousingLand/
		// HouseAddress/Sale/BuildingCapabilities bind via the now-public fields; HouseAddress's nullable Float/Integer
		// exit_* attrs bind via string proxies (XmlSerializer can't bind Nullable attrs). HouseData.AfterUnmarshal
		// cascades each HouseAddress.AfterUnmarshal(land) + Building.AfterUnmarshal(land) children-first (threads the
		// load-bearing owning HousingLand) before building addressesById (XmlSerializer doesn't auto-fire nested callbacks).
		HouseDataDh = TryLoadHolder(HouseDataDh, Path.Combine(staticDataDirectory, "housing", "houses.xml"), logger);
		// Java imports the single file custom_drop/custom_drop.xml (<custom_drop> root) and binds it to
		// StaticData.customNpcDrop; feeds DataManager.CUSTOM_NPC_DROP, read by DropRegistrationService + DropInfo for
		// per-npc custom drops (currently the only drop source for chests, since global drops exclude chest npcs).
		// Was a hollow new() -> always empty -> chests/custom-drop npcs dropped nothing. NpcDrop/DropGroup/Drop bind
		// via the now-public fields (Drop's deserialization ctor made public for XmlSerializer; Race by member name);
		// CustomDrop.AfterUnmarshal cascades each Drop.AfterUnmarshal() children-first (validates chance/minAmount +
		// defaults maxAmount=minAmount, load-bearing) before indexing by npc id (XmlSerializer fires no nested callbacks).
		CustomNpcDropDh = TryLoadHolder(CustomNpcDropDh, Path.Combine(staticDataDirectory, "custom_drop", "custom_drop.xml"), logger);
		// Java imports the single file housing/housing_objects.xml (<housing_objects> root) and binds it to
		// StaticData.housingObjectData; feeds DataManager.HOUSING_OBJECT_DATA, read by HouseObjectFactory +
		// HouseObject (the placeable-house-decoration template lookup). Was a hollow new() -> always empty ->
		// no house decoration objects could be created. The 11 polymorphic subtypes bind via the holder's stacked
		// [XmlElement(typeof)] coverage (postbox/use_item/move_item/chair/picture/passive/npc/storage/jukebox/
		// moviejukebox/emblem). AbstractHouseObject/PlaceableHouseObject + subtype [Xml*] members are now public;
		// every Nullable<int>/Nullable<enum> [XmlAttribute] is string-proxied (PlaceableHouseObject.use_days/limit/
		// location/area; HousingUseableItem.cd/use_count/required_item; UseItemAction.final_reward_id/reward_id/
		// remove_count/check_type) — XmlSerializer can't bind Nullable attrs (a single missed one aborts the whole
		// load). HousingObjectData.AfterUnmarshal indexes by template id (fires inside TryLoadHolder).
		HousingObjectDataDh = TryLoadHolder(HousingObjectDataDh, Path.Combine(staticDataDirectory, "housing", "housing_objects.xml"), logger);
		// Java imports the single file player_initial_data.xml (<player_initial_data> root) and binds it to
		// StaticData.playerInitialData; feeds DataManager.PLAYER_INITIAL_DATA, read by PlayerService.NewPlayer
		// (per-class starting items + spawn location) and the spawn-location path (TeleportService/World/Player
		// enter/leave). Was a hollow new() -> empty -> newly-created characters got NO starting items and no spawn.
		// PlayerCreationData/ItemsType/ItemType/LocationData attrs bind via the now-public fields; ItemType.template
		// is a JAXB @XmlIDREF (id attr -> ItemTemplate) which XmlSerializer can't bind to an object, so the id is held
		// in a string proxy and resolved via the in-progress ITEM_DATA in a children-first AfterUnmarshal cascade
		// (mirrors Java's @XmlIDREF + StaticDataListener handing afterUnmarshal the in-progress StaticData/ItemData).
		PlayerInitialDataDh = TryLoadPlayerInitialData(Path.Combine(staticDataDirectory, "player_initial_data.xml"), ItemDataDh, logger);
		try
		{
			GlobalDropDataDh.ProcessRules(NpcDataDh.GetNpcData());
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Failed to process global drop rules (gd_npc_names expansion).");
		}
		// Java parity: DataManager.init() calls DecomposeAction.validateRandomItemIds() AFTER the holders are wired
		// into the DataManager bridge — so it runs in GameServerBootstrapService right after
		// DataManager.RegisterInstance, NOT here (the bridge isn't bound during leaf-holder load, which produced the
		// "DataManager singleton bridge not initialized" error reading DataManager.ITEM_DATA).
	}

	// ---------------------------------------------------------------------------------------------------------
	// Operator-reload surface (Java parity: admincommands/Reload). Java's Reload re-deserializes a fixed source
	// XML/dir into the DataManager static field (or runs a setter on the existing holder); here the matching
	// leaf loaders are re-run against the captured StaticDataDirectory and the *Dh slot is reassigned, so the
	// DataManager.*_DATA accessors (which delegate to these slots) immediately reflect the reloaded data. Only
	// the tables Java's Reload supports are exposed; each method returns the resulting holder so the handler can
	// report its size (mirrors Java's "<n> ... loaded." messages). A null StaticDataDirectory (loader never ran)
	// throws so a reload before boot is a hard error rather than a silent empty.
	private string RequireStaticDataDirectory()
		=> StaticDataDirectory ?? throw new InvalidOperationException(
			"StaticData leaf holders were never loaded; cannot reload before boot.");

	public ItemData ReloadItemData(Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		var dir = RequireStaticDataDirectory();
		ItemDataDh = TryLoadHolder(new ItemData(), Path.Combine(dir, "items", "item_templates.xml"), logger);
		ItemDataDh.Cleanup();
		return ItemDataDh;
	}

	public SkillData ReloadSkillData(Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		var dir = RequireStaticDataDirectory();
		SkillDataDh = TryLoadHolder(new SkillData(), Path.Combine(dir, "skills", "skill_templates.xml"), logger);
		return SkillDataDh;
	}

	public QuestsData ReloadQuestData(Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		var dir = RequireStaticDataDirectory();
		Quests = TryLoadHolder(new QuestsData(), Path.Combine(dir, "quest_data", "quest_data.xml"), logger);
		return Quests;
	}

	public XMLQuests ReloadXmlQuests(Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		var dir = RequireStaticDataDirectory();
		XmlQuests = TryLoadMergedHolder<XMLQuests>(Path.Combine(dir, "quest_script_data"), (m, p) => m.MergePending(p), logger);
		return XmlQuests;
	}

	public CustomDrop ReloadCustomDrop(Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		var dir = RequireStaticDataDirectory();
		CustomNpcDropDh = TryLoadHolder(new CustomDrop(), Path.Combine(dir, "custom_drop", "custom_drop.xml"), logger);
		return CustomNpcDropDh;
	}

	public UpgradeArcadeData ReloadUpgradeArcadeData(Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		var dir = RequireStaticDataDirectory();
		UpgradeArcade = TryLoadHolder(new UpgradeArcadeData(), Path.Combine(dir, "events", "arcadelist.xml"), logger);
		return UpgradeArcade;
	}

	public DecomposableItemsData ReloadDecomposableItemsData(Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		var dir = RequireStaticDataDirectory();
		DecomposableItemsDataDh = TryLoadDecomposableItems(Path.Combine(dir, "decomposable_items", "decomposable_items.xml"), ItemDataDh, logger);
		return DecomposableItemsDataDh;
	}

	// NPC_SKILL and EVENT: Java builds a flat template list from the re-imported dir and calls the holder's
	// setter (setNpcSkillTemplates / setEvents). The C# merged loader already runs the equivalent merge +
	// AfterUnmarshal, producing a fully-built holder, so the *Dh slot is reassigned to it — the observable
	// result (the DataManager accessor returning the freshly-indexed data) is identical to Java's setter path.
	public NpcSkillData ReloadNpcSkillData(Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		var dir = RequireStaticDataDirectory();
		NpcSkillDataDh = TryLoadMergedHolder<NpcSkillData>(Path.Combine(dir, "npc_skills"), (m, p) => m.MergePending(p), logger);
		return NpcSkillDataDh;
	}

	public EventData ReloadEventData(Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		var dir = RequireStaticDataDirectory();
		Events = TryLoadMergedHolder<EventData>(Path.Combine(dir, "events", "timed_events"), (m, p) => m.MergePending(p), logger);
		return Events;
	}

	// DECOMPOSABLE_ITEMS needs the in-progress ItemData for its children-first AfterUnmarshal (ResultedItem validates
	// reward ids vs ITEM_DATA), but the DataManager singleton bridge is not registered yet during this load. So
	// deserialize WITHOUT the auto-AfterUnmarshal (which would route through the un-registered DataManager.ITEM_DATA)
	// and invoke the ItemData overload explicitly with the already-loaded ItemDataDh. Mirrors Java's StaticDataListener.
	private static DecomposableItemsData TryLoadDecomposableItems(string xmlFilePath, ItemData itemData, Microsoft.Extensions.Logging.ILogger? logger)
	{
		try
		{
			if (!File.Exists(xmlFilePath))
			{
				logger?.LogWarning("Static data holder file not found, leaving DecomposableItemsData empty: {Path}", xmlFilePath);
				return new DecomposableItemsData();
			}

			var holder = LoadingUtils.JaxbHolderLoader.DeserializeFile<DecomposableItemsData>(xmlFilePath);
			holder.AfterUnmarshal(itemData, null);
			return holder;
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Failed to load DecomposableItemsData from {Path}; leaving it empty.", xmlFilePath);
			return new DecomposableItemsData();
		}
	}

	// PLAYER_INITIAL_DATA's ItemType.template is a JAXB @XmlIDREF that resolves against the in-progress ITEM_DATA, but
	// the DataManager singleton bridge is not registered yet during this load. So deserialize WITHOUT auto-AfterUnmarshal
	// (which would route through the un-registered DataManager.ITEM_DATA) and invoke the ItemData overload explicitly with
	// the already-loaded ItemDataDh. Mirrors Java's StaticDataListener handing afterUnmarshal the in-progress StaticData.
	private static PlayerInitialData TryLoadPlayerInitialData(string xmlFilePath, ItemData itemData, Microsoft.Extensions.Logging.ILogger? logger)
	{
		try
		{
			if (!File.Exists(xmlFilePath))
			{
				logger?.LogWarning("Static data holder file not found, leaving PlayerInitialData empty: {Path}", xmlFilePath);
				return new PlayerInitialData();
			}

			var holder = LoadingUtils.JaxbHolderLoader.DeserializeFile<PlayerInitialData>(xmlFilePath);
			holder.AfterUnmarshal(itemData, null);
			return holder;
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Failed to load PlayerInitialData from {Path}; leaving it empty.", xmlFilePath);
			return new PlayerInitialData();
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
		var creaturePvpZones = new List<CreaturePvpZoneSummary>();
		var experience = new List<long>();
		var cosmeticItems = new List<CosmeticItemSummary>();
		var decomposableItems = new List<DecomposableItemSummary>();
		var assemblyItems = new List<AssemblyItemSummary>();
		var itemPurifications = new List<ItemPurificationSummary>();
		var itemRestrictionCleanups = new List<ItemRestrictionCleanupSummary>();
		var rideInfos = new List<RideInfoSummary>();
		var itemRandomBonuses = new List<ItemRandomBonusSummary>();
		var itemSets = new List<ItemSetSummary>();
		var enchantGroups = new List<EnchantGroupSummary>();
		var walkerVersionParents = new Dictionary<string, string>(StringComparer.Ordinal);
		var riftLocations = new List<RiftLocationSummary>();
		var staticDoors = new List<StaticDoorSummary>();
		var npcFactions = new List<NpcFactionSummary>();
		var tradeLists = new List<TradeListTemplateSummary>();
		var tradeInLists = new List<TradeListTemplateSummary>();
		var purchaseLists = new List<TradeListTemplateSummary>();
		var goodsLists = new List<GoodsListSummary>();
		var goodsInLists = new List<GoodsListSummary>();
		var goodsPurchaseLists = new List<GoodsListSummary>();
		var questDrops = new List<QuestDropSummary>();
		var npcSkillLists = new List<NpcSkillListSummary>();
		var instanceCooltimes = new List<InstanceCooltimeSummary>();
		var instanceExits = new List<InstanceExitSummary>();
		var portalLocs = new List<PortalLocSummary>();
		var autoGroups = new List<AutoGroupSummary>();
		var petSkills = new List<PetSkillSummary>();
		var petDopings = new List<PetDopingEntrySummary>();
		var legionDominions = new List<LegionDominionLocationSummary>();
		var atreianPassports = new List<AtreianPassportSummary>();
		var creationItemsByClass = new Dictionary<string, List<StartingItem>>(StringComparer.OrdinalIgnoreCase);
		var spawnLocationsByRace = new Dictionary<string, PlayerSpawnLocation>(StringComparer.OrdinalIgnoreCase);
		string? currentPlayerCreationClass = null;
		InstanceCooltimeBuilder? currentInstanceCooltime = null;
		ItemRandomBonusBuilder? currentItemRandomBonus = null;
		ItemSetBuilder? currentItemSet = null;
		EnchantGroupBuilder? currentEnchantGroup = null;
		TradeListTemplateBuilder? currentTradeListTemplate = null;
		TradeListTemplateKind currentTradeListTemplateKind = TradeListTemplateKind.TradeList;
		int currentTradeListTemplateDepth = -1;
		GoodsListBuilder? currentGoodsList = null;
		GoodsListKind currentGoodsListKind = GoodsListKind.List;
		int currentGoodsListDepth = -1;
		QuestDropBuilder? currentQuestDropBuilder = null;
		int currentStaticDoorWorldId = 0;
		string currentWalkerParentRouteId = string.Empty;
		NpcSkillListBuilder? currentNpcSkillList = null;
		NpcSkillTemplateBuilder? currentNpcSkill = null;
		CosmeticItemBuilder? currentCosmeticItem = null;
		DecomposableItemBuilder? currentDecomposableItem = null;
		int currentItemPurificationBaseItemId = 0;
		List<ItemPurificationResultSummary>? currentItemPurificationResults = null;
		ItemPurificationResultBuilder? currentItemPurificationResult = null;
		CreaturePvpZoneBuilder? currentCreaturePvpZone = null;
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

				if (reader.Depth == 2 && reader.LocalName == "enchant_list" && currentEnchantGroup != null)
				{
					enchantGroups.Add(currentEnchantGroup.ToSummary());
					currentEnchantGroup = null;
				}

				if (reader.Depth == 3 && reader.LocalName == "enchant_data" && currentEnchantGroup != null)
					currentEnchantGroup.EndLevel();

				if (reader.Depth == 2 && reader.LocalName == "zone" && currentCreaturePvpZone != null)
				{
					if (currentCreaturePvpZone.HasEnoughPoints)
						creaturePvpZones.Add(currentCreaturePvpZone.ToSummary());
					currentCreaturePvpZone = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "walk_parent" && elementPath.GetValueOrDefault(1) == "walker_versions")
					currentWalkerParentRouteId = string.Empty;

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

				if (reader.Depth == 2 && reader.LocalName == "quest" && currentQuestDropBuilder != null)
				{
					questDrops.AddRange(currentQuestDropBuilder.ToQuestDrops());
					currentQuestDropBuilder = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "world" && elementPath.GetValueOrDefault(1) == "staticdoor_templates")
					currentStaticDoorWorldId = 0;


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
				currentCreaturePvpZone = CreaturePvpZoneBuilder.TryCreate(reader);
				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "points" && currentCreaturePvpZone != null)
			{
				var bottom = ReadFloatAttribute(reader, "bottom");
				var top = ReadFloatAttribute(reader, "top");
				currentCreaturePvpZone.SetVerticalBounds(bottom, top);
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "point" && currentCreaturePvpZone != null)
			{
				var x = ReadFloatAttribute(reader, "x");
				var y = ReadFloatAttribute(reader, "y");
				currentCreaturePvpZone.AddPoint(x, y);
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

			if (reader.Depth == 3 && reader.LocalName == "modifiers" && currentItemRandomBonus != null)
			{
				currentItemRandomBonus.AddModifierGroup(ReadFloatAttribute(reader, "chance"));
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
		var workOrderRecipes = WorkOrderRecipeTable.LoadFromImportedFiles(importedFiles);

		return new StaticData(
			cacheFilePath,
			importedFiles,
			new ReadOnlyDictionary<string, int>(counts),
			topLevelElements.AsReadOnly(),
			new CreaturePvpZoneTable(creaturePvpZones.AsReadOnly()),
			new PlayerExperienceTable(experience.AsReadOnly()),
			new CosmeticItemTable(cosmeticItems.AsReadOnly()),
			new DecomposableItemTable(decomposableItems.AsReadOnly()),
			new AssemblyItemTable(assemblyItems.AsReadOnly()),
			new ItemPurificationTable(itemPurifications.AsReadOnly()),
			new ItemRestrictionCleanupTable(itemRestrictionCleanups.AsReadOnly()),
			new RideTable(rideInfos.AsReadOnly()),
			new ItemRandomBonusTable(itemRandomBonuses.AsReadOnly()),
			new ItemSetTable(itemSets.AsReadOnly()),
			new EnchantTable(enchantGroups.AsReadOnly()),
			new WalkerVersionTable(new ReadOnlyDictionary<string, string>(walkerVersionParents)),
			new RiftLocationTable(riftLocations.AsReadOnly()),
			new StaticDoorTable(staticDoors.AsReadOnly()),
			new NpcFactionTable(npcFactions.AsReadOnly()),
			new TradeListTable(
				tradeLists.AsReadOnly(),
				tradeInLists.AsReadOnly(),
				purchaseLists.AsReadOnly()),
			new GoodsListTable(
				goodsLists.AsReadOnly(),
				goodsInLists.AsReadOnly(),
				goodsPurchaseLists.AsReadOnly()),
			new QuestDropTable(questDrops.AsReadOnly()),
			new NpcSkillTable(npcSkillLists.AsReadOnly()),
			new PetSkillTable(petSkills.AsReadOnly()),
			new PetDopingTable(petDopings.AsReadOnly()),
			workOrderRecipes,
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
			new LegionDominionTable(legionDominions.AsReadOnly()),
			new AtreianPassportTable(atreianPassports.AsReadOnly()),
			validationTask);
	}

}
