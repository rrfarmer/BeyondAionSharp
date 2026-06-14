using System.Collections.ObjectModel;
using Aion.GameServer.Model.Vortex;
using System.Globalization;
using System.Xml;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Services;
using Aion.GameServer.Services.ToyPet;

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
		VortexLocationTable vortexLocations,
		NpcTemplateTable npcTemplates,
		NpcSpawnTable npcSpawns,
		StaticDoorTable staticDoors,
		NpcRiftSpawnTable npcRiftSpawns,
		NpcVortexSpawnTable npcVortexSpawns,
		NpcFactionTable npcFactions,
		TradeListTable tradeLists,
		GoodsListTable goodsLists,
		CustomNpcDropTable customNpcDrops,
		QuestDropTable questDrops,
		QuestUpdateItemTable questUpdateItems,
		GlobalDropTable globalDrops,
		EventDropTable eventDrops,
		GlobalNpcExclusionTable globalNpcExclusions,
		SkillTemplateTable skillTemplates,
		NpcSkillTable npcSkills,
		PetSkillTable petSkills,
		PetTemplateTable petTemplates,
		PetDopingTable petDopings,
		PetFeedDataTable petFeedData,
		TitleTemplateTable titleTemplates,
		RecipeTemplateTable recipeTemplates,
		WorkOrderRecipeTable workOrderRecipes,
		HousingTemplateTable housingTemplates,
		HousingObjectTemplateTable housingObjectTemplates,
		InstanceCooltimeTable instanceCooltimes,
		InstanceExitTable instanceExits,
		PortalPathTable portalPaths,
		PortalLocTable portalLocs,
		AutoGroupTable autoGroups,
		PlayerInitialDataTable playerInitialData,
		SkillTreeTable skillTree,
		StorageExpansionTemplateTable cubeExpansionTemplates,
		StorageExpansionTemplateTable warehouseExpansionTemplates,
		NearbyQuestTemplateTable nearbyQuestTemplates,
		QuestHandlerAvailabilityTable questHandlers,
		QuestNpcStartTable questNpcStarts,
		QuestCompletionFollowUpTable questCompletionFollowUps,
		QuestBonusItemGroupTable questBonusItemGroups,
		ChallengeTaskTable challengeTasks,
		LegionDominionTable legionDominions,
		AtreianPassportTable atreianPassports,
		WindstreamTable windstreamLocations,
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
		VortexLocations = vortexLocations;
		NpcTemplates = npcTemplates;
		NpcSpawns = npcSpawns;
		StaticDoors = staticDoors;
		NpcRiftSpawns = npcRiftSpawns;
		NpcVortexSpawns = npcVortexSpawns;
		NpcFactions = npcFactions;
		TradeLists = tradeLists;
		GoodsLists = goodsLists;
		CustomNpcDrops = customNpcDrops;
		QuestDrops = questDrops;
		QuestUpdateItems = questUpdateItems;
		GlobalDrops = globalDrops;
		EventDrops = eventDrops;
		GlobalNpcExclusions = globalNpcExclusions;
		SkillTemplates = skillTemplates;
		NpcSkills = npcSkills;
		PetSkills = petSkills;
		PetTemplates = petTemplates;
		PetDopings = petDopings;
		PetFeedData = petFeedData;
		TitleTemplates = titleTemplates;
		RecipeTemplates = recipeTemplates;
		WorkOrderRecipes = workOrderRecipes;
		HousingTemplates = housingTemplates;
		HousingObjectTemplates = housingObjectTemplates;
		InstanceCooltimes = instanceCooltimes;
		InstanceExits = instanceExits;
		PortalPaths = portalPaths;
		PortalLocs = portalLocs;
		AutoGroups = autoGroups;
		PlayerInitialData = playerInitialData;
		SkillTree = skillTree;
		CubeExpansionTemplates = cubeExpansionTemplates;
		WarehouseExpansionTemplates = warehouseExpansionTemplates;
		NearbyQuestTemplates = nearbyQuestTemplates;
		QuestHandlers = questHandlers;
		QuestNpcStarts = questNpcStarts;
		QuestCompletionFollowUps = questCompletionFollowUps;
		QuestBonusItemGroups = questBonusItemGroups;
		ChallengeTasks = challengeTasks;
		LegionDominions = legionDominions;
		AtreianPassports = atreianPassports;
		WindstreamLocations = windstreamLocations;
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
	public CosmeticItemsData CosmeticItemsDataDh { get; } = new();

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

	public VortexLocationTable VortexLocations { get; }

	// Faithful VortexData holder (empty-default; runtime XML load deferred) - summary->template re-point.
	public VortexData VortexDataDh { get; } = new();

	public NpcTemplateTable NpcTemplates { get; }

	public NpcSpawnTable NpcSpawns { get; }

	public StaticDoorTable StaticDoors { get; }

	public NpcRiftSpawnTable NpcRiftSpawns { get; }

	public NpcVortexSpawnTable NpcVortexSpawns { get; }

	public NpcFactionTable NpcFactions { get; }

	public TradeListTable TradeLists { get; }

	public GoodsListTable GoodsLists { get; }

	public CustomNpcDropTable CustomNpcDrops { get; }

	public QuestDropTable QuestDrops { get; }

	public QuestUpdateItemTable QuestUpdateItems { get; }

	public GlobalDropTable GlobalDrops { get; }

	public EventDropTable EventDrops { get; }

	public GlobalNpcExclusionTable GlobalNpcExclusions { get; }

	public SkillTemplateTable SkillTemplates { get; }

	public NpcSkillTable NpcSkills { get; }

	public PetSkillTable PetSkills { get; }

	public PetTemplateTable PetTemplates { get; }

	public PetDopingTable PetDopings { get; }

	public PetFeedDataTable PetFeedData { get; }

	// Faithful PetFeedData holder (empty-default; runtime XML load deferred) - see summary->template re-point.
	public PetFeedData PetFeedDataDh { get; } = new();

	public TitleTemplateTable TitleTemplates { get; }

	public RecipeTemplateTable RecipeTemplates { get; }

	public WorkOrderRecipeTable WorkOrderRecipes { get; }

	public HousingTemplateTable HousingTemplates { get; }

	public HousingObjectTemplateTable HousingObjectTemplates { get; }

	public InstanceCooltimeTable InstanceCooltimes { get; }

	public InstanceExitTable InstanceExits { get; }

	public PortalPathTable PortalPaths { get; }

	public PortalLocTable PortalLocs { get; }

	public AutoGroupTable AutoGroups { get; }

	public PlayerInitialDataTable PlayerInitialData { get; }

	public SkillTreeTable SkillTree { get; }

	public StorageExpansionTemplateTable CubeExpansionTemplates { get; }

	public CubeExpandData CubeExpandDataDh { get; } = new();

	public StorageExpansionTemplateTable WarehouseExpansionTemplates { get; }

	public WarehouseExpandData WarehouseExpandDataDh { get; } = new();

	public NearbyQuestTemplateTable NearbyQuestTemplates { get; }

	public QuestHandlerAvailabilityTable QuestHandlers { get; }

	public QuestNpcStartTable QuestNpcStarts { get; }

	public QuestCompletionFollowUpTable QuestCompletionFollowUps { get; }

	public QuestBonusItemGroupTable QuestBonusItemGroups { get; }

	public ChallengeTaskTable ChallengeTasks { get; }

	public LegionDominionTable LegionDominions { get; }

	public LegionDominionData LegionDominionDataDh { get; } = new();

	public AtreianPassportTable AtreianPassports { get; }

	public WindstreamTable WindstreamLocations { get; }

	public WindstreamData WindstreamDataDh { get; } = new();

	public Task? ValidationTask { get; }

	// Java parity: DataManager.QUEST_DATA / TRIBE_RELATIONS_DATA / WORLD_MAPS_DATA / NPC_SHOUT_DATA / UPGRADE_ARCADE_DATA.
	// These faithful dataholder classes are not yet populated by the bespoke XML loader; exposed here with empty
	// defaults so the DataManager.*_DATA accessors compile. TODO(runtime): deserialize their source XML
	// (e.g. game-server/data/static_data/quest_data/quest_data.xml is a self-contained <quests> root) and assign here.
	public QuestsData Quests { get; } = new();
	public TribeRelationsData TribeRelations { get; } = new();
	public WorldMapsData WorldMaps2 { get; } = new();
	public NpcShoutData NpcShouts { get; } = new();
	public UpgradeArcadeData UpgradeArcade { get; } = new();
	public SiegeLocationData SiegeLocations { get; } = new();
	public ItemGroupsData ItemGroups { get; } = new();
	public Aion.GameServer.Model.Templates.Mail.Mails SystemMailTemplates { get; } = new();
	public HouseBuildingData HouseBuildings { get; } = new();
	public GuideHtmlData GuideHtml { get; } = new();
	public MaterialData Materials { get; } = new();
	public ZoneData ZoneInfo { get; } = new();
	public XMLQuests XmlQuests { get; } = new();
	public TownSpawnsData TownSpawns { get; } = new();
	public SkillChargeData SkillCharges { get; } = new();
	public MotionData Motions { get; } = new();
	public MapWeatherData MapWeathers { get; } = new();
	public EventData Events { get; } = new();
	public PanelSkillsData PanelSkillsDataDh { get; } = new();
	public ItemRestrictionCleanupData ItemRestrictionCleanupDataDh { get; } = new();
	public ConquerorAndProtectorData ConquerorAndProtectorDataDh { get; } = new();
	public AbsoluteStatsData AbsoluteStatsDataDh { get; } = new();
	public WorldRaidData WorldRaidDataDh { get; } = new();
	public TeleporterData TeleporterDataDh { get; } = new();
	public TeleLocationData TeleLocationDataDh { get; } = new();
	public SkillAliasLocationData SkillAliasLocationDataDh { get; } = new();
	public SignetDataTemplates SignetDataTemplatesDh { get; } = new();
	public ShieldData ShieldDataDh { get; } = new();
	public RoadData RoadDataDh { get; } = new();
	public MultiReturnItemData MultiReturnItemDataDh { get; } = new();
	public KillBountyData KillBountyDataDh { get; } = new();
	public InstanceBuffData InstanceBuffDataDh { get; } = new();
	public HousePartsData HousePartsDataDh { get; } = new();
	public HouseNpcsData HouseNpcsDataDh { get; } = new();
	public HotspotData HotspotDataDh { get; } = new();
	public GatherableData GatherableDataDh { get; } = new();
	public FlyRingData FlyRingDataDh { get; } = new();
	public FlyPathData FlyPathDataDh { get; } = new();
	public CuringObjectsData CuringObjectsDataDh { get; } = new();
	public BaseData BaseDataDh { get; } = new();
	public AssembledNpcsData AssembledNpcsDataDh { get; } = new();
	public TitleData TitleDataDh { get; } = new();
	public NpcData NpcDataDh { get; } = new();
	public ItemData ItemDataDh { get; } = new();
	public SkillData SkillDataDh { get; } = new();
	public InstanceCooltimeData InstanceCooltimeDataDh { get; } = new();
	public TradeListData TradeListDataDh { get; } = new();
	public RecipeData RecipeDataDh { get; } = new();
	public PetData PetDataDh { get; } = new();
	public WalkerData WalkerDataDh { get; } = new();
	public AutoGroupData AutoGroupDataDh { get; } = new();
	public ItemSetData ItemSetDataDh { get; } = new();
	public RideData RideDataDh { get; } = new();
	public GoodsListData GoodsListDataDh { get; } = new();
	public ItemPurificationData ItemPurificationDataDh { get; } = new();
	public AtreianPassportData AtreianPassportDataDh { get; } = new();
	public PetDopingData PetDopingDataDh { get; } = new();
	public NpcSkillData NpcSkillDataDh { get; } = new();
	public NpcFactionsData NpcFactionsDataDh { get; } = new();
	public PortalLocData PortalLocDataDh { get; } = new();
	public AssemblyItemsData AssemblyItemsDataDh { get; } = new();

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
		var vortexLocations = new List<VortexLocationSummary>();
		var npcTemplates = new List<NpcTemplateSummary>();
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
		var questUpdateItemIds = new List<int>();
		var questUpdateItemIdSet = new HashSet<int>();
		var globalDropRules = new List<GlobalDropRuleSummary>();
		var eventTemplates = new List<EventTemplateSummary>();
		var globalNpcExclusionNpcIds = new HashSet<int>();
		var globalNpcExclusionNpcNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var globalNpcExclusionNpcTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var globalNpcExclusionNpcTribes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var globalNpcExclusionNpcAbyssTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var skillTemplates = new List<SkillTemplateSummary>();
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
		var portalUsePaths = new List<PortalPathSummary>();
		var portalDialogPaths = new List<PortalPathSummary>();
		var portalScrollPaths = new List<PortalPathSummary>();
		var portalDialogTeleportIds = new Dictionary<int, int>();
		var portalLocs = new List<PortalLocSummary>();
		var autoGroups = new List<AutoGroupSummary>();
		var petSkills = new List<PetSkillSummary>();
		var petTemplates = new List<PetTemplateSummary>();
		var petDopings = new List<PetDopingEntrySummary>();
		var petFeedFlavours = new Dictionary<int, PetFeedFlavourProjection>();
		var petFoodGroupItems = new Dictionary<PetFoodType, HashSet<int>>();
		var skillTree = new List<SkillLearnSummary>();
		var cubeExpansionTemplates = new List<StorageExpansionTemplateSummary>();
		var warehouseExpansionTemplates = new List<StorageExpansionTemplateSummary>();
		var questBonusItemGroups = new List<QuestBonusItemGroupProjection>();
		var challengeTasks = new List<ChallengeTaskSummary>();
		var legionDominions = new List<LegionDominionLocationSummary>();
		var atreianPassports = new List<AtreianPassportSummary>();
		var windstreamLocations = new List<WindstreamLocationSummary>();
		int currentWindstreamMapId = 0;
		var learnableEmotionIds = new HashSet<int>();
		var creationItemsByClass = new Dictionary<string, List<StartingItem>>(StringComparer.OrdinalIgnoreCase);
		var spawnLocationsByRace = new Dictionary<string, PlayerSpawnLocation>(StringComparer.OrdinalIgnoreCase);
		string? currentPlayerCreationClass = null;
		InstanceCooltimeBuilder? currentInstanceCooltime = null;
		PortalPathParent? currentPortalPathParent = null;
		PortalPathBuilder? currentPortalPath = null;
		ItemTemplateBuilder? currentItemTemplate = null;
		ItemRandomBonusBuilder? currentItemRandomBonus = null;
		ItemSetBuilder? currentItemSet = null;
		EnchantGroupBuilder? currentEnchantGroup = null;
		TemperingGroupBuilder? currentTemperingGroup = null;
		WalkerTemplateBuilder? currentWalkerTemplate = null;
		NpcTemplateBuilder? currentNpcTemplate = null;
		NpcSpawnBuilder? currentNpcSpawn = null;
		NpcSpawnSpotBuilder? currentNpcSpawnSpot = null;
		NpcRiftSpawnBuilder? currentNpcRiftSpawn = null;
		NpcSpawnSpotBuilder? currentNpcRiftSpawnSpot = null;
		NpcVortexSpawnBuilder? currentNpcVortexSpawn = null;
		NpcSpawnSpotBuilder? currentNpcVortexSpawnSpot = null;
		VortexLocationBuilder? currentVortexLocation = null;
		TradeListTemplateBuilder? currentTradeListTemplate = null;
		TradeListTemplateKind currentTradeListTemplateKind = TradeListTemplateKind.TradeList;
		int currentTradeListTemplateDepth = -1;
		GoodsListBuilder? currentGoodsList = null;
		GoodsListKind currentGoodsListKind = GoodsListKind.List;
		int currentGoodsListDepth = -1;
		QuestDropBuilder? currentQuestDropBuilder = null;
		EventTemplateBuilder? currentEventTemplate = null;
		int currentEventTemplateDepth = -1;
		GlobalDropRuleBuilder? currentGlobalDropRule = null;
		int currentGlobalDropRuleDepth = -1;
		bool currentGlobalDropRuleIsEventDrop = false;
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
		SkillTemplateBuilder? currentSkillTemplate = null;
		NpcSkillListBuilder? currentNpcSkillList = null;
		NpcSkillTemplateBuilder? currentNpcSkill = null;
		PetTemplateBuilder? currentPetTemplate = null;
		int currentPetTemplateDepth = -1;
		PetFeedFlavourBuilder? currentPetFeedFlavour = null;
		PetFeedRewardGroupBuilder? currentPetFeedRewardGroup = null;
		int currentPetFeedFlavourDepth = -1;
		int currentPetFeedRewardGroupDepth = -1;
		PetFoodType? currentPetFoodItemGroup = null;
		int currentPetFoodItemGroupDepth = -1;
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
		string currentQuestBonusGroupElementName = string.Empty;
		string currentQuestBonusGroupBonusType = string.Empty;
		float currentQuestBonusGroupChance = 100f;
		QuestBonusItemShape currentQuestBonusGroupShape = default;
		List<QuestBonusItemProjection>? currentQuestBonusItems = null;
		ChallengeTaskBuilder? currentChallengeTask = null;
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

				if (reader.Depth == 2 && reader.LocalName is "portal_use" or "portal_dialog" or "portal_scroll")
					currentPortalPathParent = null;

				if (reader.Depth == 3 && reader.LocalName == "portal_path" && currentPortalPath != null)
				{
					AddPortalPathSummary(
						currentPortalPath.ToSummary(),
						portalUsePaths,
						portalDialogPaths,
						portalScrollPaths);
					currentPortalPath = null;
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

				if (reader.Depth == 2 && reader.LocalName == "npc_template" && currentNpcTemplate != null)
				{
					npcTemplates.Add(currentNpcTemplate.ToSummary());
					currentNpcTemplate = null;
				}

				if (reader.Depth == currentPetTemplateDepth && reader.LocalName == "pet" && currentPetTemplate != null)
				{
					petTemplates.Add(currentPetTemplate.ToSummary());
					currentPetTemplate = null;
					currentPetTemplateDepth = -1;
				}

				if (reader.Depth == currentPetFeedRewardGroupDepth && reader.LocalName == "food" && currentPetFeedRewardGroup != null)
				{
					currentPetFeedFlavour?.AddRewardGroup(currentPetFeedRewardGroup.ToSummary());
					currentPetFeedRewardGroup = null;
					currentPetFeedRewardGroupDepth = -1;
				}

				if (reader.Depth == currentPetFeedFlavourDepth && reader.LocalName == "flavour" && currentPetFeedFlavour != null)
				{
					var flavour = currentPetFeedFlavour.ToSummary();
					// Java parity: PetFeedData.afterUnmarshal Map.put replaces duplicate flavour ids with the later row.
					petFeedFlavours[flavour.Id] = flavour;
					currentPetFeedFlavour = null;
					currentPetFeedFlavourDepth = -1;
				}

				if (reader.Depth == currentPetFoodItemGroupDepth && currentPetFoodItemGroup.HasValue)
				{
					currentPetFoodItemGroup = null;
					currentPetFoodItemGroupDepth = -1;
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

				if (reader.Depth == 2 && reader.LocalName == "vortex_location" && currentVortexLocation != null)
				{
					vortexLocations.Add(currentVortexLocation.ToSummary());
					currentVortexLocation = null;
				}

				if (reader.Depth == 2 && reader.LocalName == "quest" && currentQuestDropBuilder != null)
				{
					questDrops.AddRange(currentQuestDropBuilder.ToQuestDrops());
					currentQuestDropBuilder = null;
				}

				if (reader.Depth == currentGlobalDropRuleDepth && reader.LocalName == "gd_rule" && currentGlobalDropRule != null)
				{
					if (currentGlobalDropRuleIsEventDrop && currentEventTemplate != null)
						currentEventTemplate.AddDropRule(currentGlobalDropRule.ToSummary());
					else
						globalDropRules.Add(currentGlobalDropRule.ToSummary());
					currentGlobalDropRule = null;
					currentGlobalDropRuleDepth = -1;
					currentGlobalDropRuleIsEventDrop = false;
				}

				if (reader.Depth == currentEventTemplateDepth && reader.LocalName == "event" && currentEventTemplate != null)
				{
					eventTemplates.Add(currentEventTemplate.ToSummary());
					currentEventTemplate = null;
					currentEventTemplateDepth = -1;
				}

				if (reader.Depth == 2 && reader.LocalName == "spawn_map" && elementPath.GetValueOrDefault(1) == "spawns")
					currentNpcSpawnMapId = 0;

				if (reader.Depth == 2 && reader.LocalName == "world" && elementPath.GetValueOrDefault(1) == "staticdoor_templates")
					currentStaticDoorWorldId = 0;

				if (reader.Depth == 2 && reader.LocalName == "windstream" && elementPath.GetValueOrDefault(1) == "windstreams")
					currentWindstreamMapId = 0;

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

				if (reader.Depth == 4 && IsDropBoostStatEffectElement(reader.LocalName))
					currentSkillTemplate?.EndBuffStatEffect();

				if (reader.Depth == 5 && reader.LocalName == "change")
					currentSkillTemplate?.EndCurrentStatChangeConditions();

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
				if (reader.Depth == 2 && currentQuestBonusItems != null)
				{
					questBonusItemGroups.Add(new QuestBonusItemGroupProjection(
						currentQuestBonusGroupElementName,
						currentQuestBonusGroupBonusType,
						currentQuestBonusGroupChance,
						currentQuestBonusGroupShape,
						currentQuestBonusItems.AsReadOnly()));
					currentQuestBonusGroupElementName = string.Empty;
					currentQuestBonusGroupBonusType = string.Empty;
					currentQuestBonusGroupChance = 100f;
					currentQuestBonusGroupShape = default;
					currentQuestBonusItems = null;
				}
				if (reader.Depth == 2 && reader.LocalName == "task" && currentChallengeTask != null)
				{
					challengeTasks.Add(currentChallengeTask.ToSummary());
					currentChallengeTask = null;
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
			if (reader.Depth == 2
				&& elementPath.GetValueOrDefault(1) == "item_groups"
				&& QuestBonusItemGroupXmlProjectionExtractor.TryGetSupportedGroup(reader.LocalName, out var defaultBonusType, out var itemShape))
			{
				currentQuestBonusGroupElementName = reader.LocalName;
				currentQuestBonusGroupBonusType = reader.GetAttribute("bonusType") ?? defaultBonusType;
				currentQuestBonusGroupChance = ReadOptionalFloatAttribute(reader, "chance", 100f);
				currentQuestBonusGroupShape = itemShape;
				currentQuestBonusItems = [];
				if (reader.IsEmptyElement)
				{
					questBonusItemGroups.Add(new QuestBonusItemGroupProjection(
						currentQuestBonusGroupElementName,
						currentQuestBonusGroupBonusType,
						currentQuestBonusGroupChance,
						currentQuestBonusGroupShape,
						currentQuestBonusItems.AsReadOnly()));
					currentQuestBonusGroupElementName = string.Empty;
					currentQuestBonusGroupBonusType = string.Empty;
					currentQuestBonusGroupChance = 100f;
					currentQuestBonusGroupShape = default;
					currentQuestBonusItems = null;
				}

				continue;
			}

			if (reader.Depth == 3
				&& reader.LocalName == "item"
				&& currentQuestBonusItems != null
				&& elementPath.GetValueOrDefault(1) == "item_groups")
			{
				currentQuestBonusItems.Add(new QuestBonusItemProjection(
					ReadRequiredIntAttribute(reader, "id"),
					Race: reader.GetAttribute("race"),
					Level: ReadNullableIntAttribute(reader, "level"),
					Count: ReadNullableLongAttribute(reader, "count"),
					Chance: ReadNullableFloatAttribute(reader, "chance"),
					Skill: ReadNullableIntAttribute(reader, "skill"),
					MinLevel: ReadNullableIntAttribute(reader, "minLevel"),
					MaxLevel: ReadNullableIntAttribute(reader, "maxLevel")));
				continue;
			}

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

			if (reader.Depth == 2 && reader.LocalName == "portal_use")
			{
				// Java parity: model/templates/portal/PortalUse grouped by npc_id in dataholders/Portal2Data.
				currentPortalPathParent = PortalPathParent.ForUse(ReadRequiredIntAttribute(reader, "npc_id"));
				if (reader.IsEmptyElement)
					currentPortalPathParent = null;

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

			if (reader.Depth == 2 && reader.LocalName == "portal_dialog")
			{
				// Java parity: model/templates/portal/PortalDialog teleport_dialog_id defaults to 1011.
				var npcId = ReadRequiredIntAttribute(reader, "npc_id");
				portalDialogTeleportIds[npcId] = ReadOptionalIntAttribute(reader, "teleport_dialog_id", 1011);
				currentPortalPathParent = PortalPathParent.ForDialog(npcId);
				if (reader.IsEmptyElement)
					currentPortalPathParent = null;

				continue;
			}

			if (reader.Depth == 2 && reader.LocalName == "portal_scroll")
			{
				// Java parity: model/templates/portal/PortalScroll is keyed by scroll template name.
				currentPortalPathParent = PortalPathParent.ForScroll(reader.GetAttribute("name") ?? string.Empty);
				if (reader.IsEmptyElement)
					currentPortalPathParent = null;

				continue;
			}

			if (reader.Depth == 3 && reader.LocalName == "portal_path" && currentPortalPathParent != null)
			{
				// Java parity: model/templates/portal/PortalPath scalar JAXB attributes plus child requirements.
				currentPortalPath = currentPortalPathParent.CreateBuilder(
					ReadIntAttribute(reader, "dialog"),
					ReadIntAttribute(reader, "loc_id"),
					ReadIntAttribute(reader, "siege_id"),
					reader.GetAttribute("race") ?? "PC_ALL",
					ReadIntAttribute(reader, "min_level"),
					ReadIntAttribute(reader, "min_rank"),
					ReadIntAttribute(reader, "kinah"),
					ReadIntAttribute(reader, "title_id"),
					ReadIntAttribute(reader, "err_group"),
					ReadIntAttribute(reader, "err_level"));
				if (reader.IsEmptyElement)
				{
					AddPortalPathSummary(
						currentPortalPath.ToSummary(),
						portalUsePaths,
						portalDialogPaths,
						portalScrollPaths);
					currentPortalPath = null;
				}

				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "quest_req" && currentPortalPath != null)
			{
				// Java parity: model/templates/portal/QuestReq child entries are carried structurally for future checkQuests parity.
				currentPortalPath.AddQuestRequirement(
					new PortalQuestRequirementSummary(
						ReadIntAttribute(reader, "quest_id"),
						ReadIntAttribute(reader, "quest_step")));
				continue;
			}

			if (reader.Depth == 4 && reader.LocalName == "item_req" && currentPortalPath != null)
			{
				// Java parity: model/templates/portal/ItemReq child entries are carried structurally for future item removal parity.
				currentPortalPath.AddItemRequirement(
					new PortalItemRequirementSummary(
						ReadIntAttribute(reader, "item_id"),
						ReadIntAttribute(reader, "item_count")));
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

			if (reader.Depth == 2 && reader.LocalName == "windstream" && elementPath.GetValueOrDefault(1) == "windstreams")
			{
				// Java parity: dataholders/WindstreamData indexes WindstreamTemplate entries by mapid attribute.
				currentWindstreamMapId = ReadRequiredIntAttribute(reader, "mapid");
				continue;
			}

			if (currentWindstreamMapId != 0
				&& reader.Depth == 4
				&& reader.LocalName == "location"
				&& elementPath.GetValueOrDefault(3) == "locations")
			{
				// Java parity: model/templates/windstreams/Location2D with fly_path mapped to FlyPathType.getId(): GEYSER=0, ONE_WAY=1, TWO_WAY=2.
				var flyPath = reader.GetAttribute("fly_path") ?? string.Empty;
				var flyPathId = flyPath switch
				{
					"GEYSER" => 0,
					"ONE_WAY" => 1,
					"TWO_WAY" => 2,
					_ => 0,
				};
				windstreamLocations.Add(new WindstreamLocationSummary(
					flyPathId,
					currentWindstreamMapId,
					ReadRequiredIntAttribute(reader, "id"),
					ReadOptionalIntAttribute(reader, "state", 0)));
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

			if (reader.Depth == 2 && reader.LocalName == "vortex_location" && elementPath.GetValueOrDefault(1) == "dimensional_vortex")
			{
				// Java parity: dataholders/VortexData.afterUnmarshal converts every VortexTemplate into a VortexLocation keyed by id.
				currentVortexLocation = new VortexLocationBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					reader.GetAttribute("defends_race") ?? string.Empty,
					reader.GetAttribute("offence_race") ?? string.Empty);
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

			if (reader.LocalName == "event" && elementPath.GetValueOrDefault(reader.Depth - 1) == "timed_events")
			{
				// Java parity: dataholders/EventData loads timed EventTemplate rows and validates date windows.
				currentEventTemplate = new EventTemplateBuilder(
					reader.GetAttribute("name") ?? string.Empty,
					ReadDateTimeAttribute(reader, "start"),
					ReadDateTimeAttribute(reader, "end"),
					reader.GetAttribute("theme") ?? string.Empty);
				currentEventTemplateDepth = reader.Depth;
				if (reader.IsEmptyElement)
				{
					eventTemplates.Add(currentEventTemplate.ToSummary());
					currentEventTemplate = null;
					currentEventTemplateDepth = -1;
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

			if (reader.Depth == 4
				&& reader.LocalName == "inventory_item"
				&& currentQuestDropBuilder != null
				&& elementPath.GetValueOrDefault(3) == "inventory_items")
			{
				// Java parity: questEngine/Aion.GameServer.QuestEngine.QuestEngine.init builds questUpdateItems from InventoryItem.item_id and ignores count.
				var itemId = ReadRequiredIntAttribute(reader, "item_id");
				if (questUpdateItemIdSet.Add(itemId))
					questUpdateItemIds.Add(itemId);
				continue;
			}

			if (IsInsideElement(elementPath, reader.Depth, "global_npc_exclusions"))
			{
				// Java parity: dataholders/GlobalNpcExclusionData JAXB whitespace-list elements.
				var localName = reader.LocalName;
				var value = await ReadElementTextAsync(reader, cancellationToken);
				switch (localName)
				{
					case "npc_ids":
						globalNpcExclusionNpcIds.UnionWith(ParseIntSet(value));
						break;
					case "npc_names":
						globalNpcExclusionNpcNames.UnionWith(ParseStringSet(value));
						break;
					case "npc_types":
						globalNpcExclusionNpcTypes.UnionWith(ParseStringSet(value));
						break;
					case "npc_tribes":
						globalNpcExclusionNpcTribes.UnionWith(ParseStringSet(value));
						break;
					case "npc_abyss_types":
						globalNpcExclusionNpcAbyssTypes.UnionWith(ParseStringSet(value));
						break;
				}
				continue;
			}

			if (reader.LocalName == "gd_rule" && IsInsideElement(elementPath, reader.Depth, "global_rules"))
			{
				// Java parity: dataholders/GlobalDropData loads every GlobalRule from global_drops/rules.
				currentGlobalDropRule = new GlobalDropRuleBuilder(
					reader.GetAttribute("rule_name") ?? string.Empty,
					ReadFloatAttribute(reader, "chance"),
					ReadBoolAttribute(reader, "dynamic_chance"),
					ReadOptionalIntAttribute(reader, "min_diff", -99),
					ReadOptionalIntAttribute(reader, "max_diff", 99),
					reader.GetAttribute("restriction_race") ?? string.Empty,
					ReadBoolAttribute(reader, "level_based_chance_reduction"),
					ReadOptionalIntAttribute(reader, "member_limit", 1),
					ReadOptionalIntAttribute(reader, "max_drop_rule", 1));
				currentGlobalDropRuleDepth = reader.Depth;
				currentGlobalDropRuleIsEventDrop = false;
				if (reader.IsEmptyElement)
				{
					globalDropRules.Add(currentGlobalDropRule.ToSummary());
					currentGlobalDropRule = null;
					currentGlobalDropRuleDepth = -1;
				}
				continue;
			}

			if (reader.LocalName == "gd_rule" && currentEventTemplate != null && IsInsideElement(elementPath, reader.Depth, "event_drops"))
			{
				// Java parity: model/templates/event/EventTemplate.eventDropRules stores timed event gd_rule entries.
				currentGlobalDropRule = new GlobalDropRuleBuilder(
					reader.GetAttribute("rule_name") ?? string.Empty,
					ReadFloatAttribute(reader, "chance"),
					ReadBoolAttribute(reader, "dynamic_chance"),
					ReadOptionalIntAttribute(reader, "min_diff", -99),
					ReadOptionalIntAttribute(reader, "max_diff", 99),
					reader.GetAttribute("restriction_race") ?? string.Empty,
					ReadBoolAttribute(reader, "level_based_chance_reduction"),
					ReadOptionalIntAttribute(reader, "member_limit", 1),
					ReadOptionalIntAttribute(reader, "max_drop_rule", 1));
				currentGlobalDropRuleDepth = reader.Depth;
				currentGlobalDropRuleIsEventDrop = true;
				if (reader.IsEmptyElement)
				{
					currentEventTemplate.AddDropRule(currentGlobalDropRule.ToSummary());
					currentGlobalDropRule = null;
					currentGlobalDropRuleDepth = -1;
					currentGlobalDropRuleIsEventDrop = false;
				}
				continue;
			}

			if (currentGlobalDropRule != null && reader.Depth > currentGlobalDropRuleDepth)
			{
				switch (reader.LocalName)
				{
					case "gd_item":
						var minCount = ReadOptionalIntAttribute(reader, "min_count", 1);
						var maxCount = ReadOptionalIntAttribute(reader, "max_count", minCount);
						if (maxCount == 0)
							maxCount = minCount;
						currentGlobalDropRule.AddItem(
							new GlobalDropItemSummary(
								ReadRequiredIntAttribute(reader, "id"),
								minCount,
								maxCount,
								ReadOptionalFloatAttribute(reader, "chance", 100f)));
						continue;
					case "gd_world":
						currentGlobalDropRule.WorldTypes.Add(reader.GetAttribute("wd_type") ?? string.Empty);
						continue;
					case "gd_race":
						currentGlobalDropRule.Races.Add(reader.GetAttribute("race") ?? string.Empty);
						continue;
					case "gd_rating":
						currentGlobalDropRule.Ratings.Add(reader.GetAttribute("rating") ?? string.Empty);
						continue;
					case "gd_map":
						currentGlobalDropRule.MapIds.Add(ReadRequiredIntAttribute(reader, "map_id"));
						continue;
					case "gd_tribe":
						currentGlobalDropRule.Tribes.Add(reader.GetAttribute("tribe") ?? string.Empty);
						continue;
					case "gd_npc":
						currentGlobalDropRule.NpcIds.Add(ReadRequiredIntAttribute(reader, "npc_id"));
						continue;
					case "gd_npc_name":
						currentGlobalDropRule.NpcNames.Add(
							new GlobalDropNpcNameSummary(
								reader.GetAttribute("function") ?? string.Empty,
								reader.GetAttribute("value") ?? string.Empty));
						continue;
					case "gd_npc_group":
						currentGlobalDropRule.NpcGroups.Add(reader.GetAttribute("group") ?? string.Empty);
						continue;
					case "gd_excluded_npcs":
						currentGlobalDropRule.ExcludedNpcIds.UnionWith(ReadIntListAttribute(reader, "npc_ids"));
						continue;
					case "gd_zone":
						currentGlobalDropRule.Zones.Add(reader.GetAttribute("zone") ?? string.Empty);
						continue;
				}
			}

			if (currentVortexLocation != null && reader.Depth == 3 && elementPath.GetValueOrDefault(2) == "vortex_location")
			{
				var point = ReadVortexPoint(reader);
				switch (reader.LocalName)
				{
					case "home_point":
						currentVortexLocation.HomePoint = point;
						break;
					case "resurrection_point":
						currentVortexLocation.ResurrectionPoint = point;
						break;
					case "start_point":
						currentVortexLocation.StartPoint = point;
						break;
				}

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
				&& reader.LocalName == "task"
				&& elementPath.GetValueOrDefault(1) == "challenge_tasks")
			{
				// Java parity: model/templates/challenge/ChallengeTaskTemplate fields used by ChallengeTaskService.canRaiseLegionLevel.
				currentChallengeTask = new ChallengeTaskBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					reader.GetAttribute("type") ?? string.Empty,
					reader.GetAttribute("race") ?? string.Empty,
					ReadRequiredIntAttribute(reader, "min_level"),
					ReadRequiredIntAttribute(reader, "max_level"),
					ReadOptionalBoolAttribute(reader, "legion_level_task", false),
					ReadOptionalBoolAttribute(reader, "repeat", false),
					int.TryParse(reader.GetAttribute("prev_task"), out var previousTaskId) ? previousTaskId : (int?)null);
				if (reader.IsEmptyElement)
				{
					challengeTasks.Add(currentChallengeTask.ToSummary());
					currentChallengeTask = null;
				}

				continue;
			}

			if (reader.Depth == 3
				&& reader.LocalName == "quest"
				&& currentChallengeTask != null
				&& elementPath.GetValueOrDefault(2) == "task")
			{
				currentChallengeTask.AddQuest(new ChallengeQuestSummary(
					ReadRequiredIntAttribute(reader, "id"),
					ReadRequiredIntAttribute(reader, "repeat_count"),
					ReadRequiredIntAttribute(reader, "score")));
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

			if (reader.Depth == 2
				&& reader.LocalName == "flavour"
				&& elementPath.GetValueOrDefault(1) == "pet_feed")
			{
				// Java parity: dataholders/PetFeedData indexes pet_feed.xml flavour rows by id after unmarshalling.
				currentPetFeedFlavour = new PetFeedFlavourBuilder(
					ReadRequiredIntAttribute(reader, "id"),
					ReadOptionalIntAttribute(reader, "full_count", defaultValue: 1),
					ReadOptionalIntAttribute(reader, "loved_limit", defaultValue: 0),
					ReadRequiredIntAttribute(reader, "cd"));
				currentPetFeedFlavourDepth = reader.Depth;
				if (reader.IsEmptyElement)
				{
					var flavour = currentPetFeedFlavour.ToSummary();
					petFeedFlavours[flavour.Id] = flavour;
					currentPetFeedFlavour = null;
					currentPetFeedFlavourDepth = -1;
				}

				continue;
			}

			if (currentPetFeedFlavour != null
				&& reader.Depth == currentPetFeedFlavourDepth + 1
				&& reader.LocalName == "food")
			{
				currentPetFeedRewardGroup = new PetFeedRewardGroupBuilder(
					ReadPetFoodTypeAttribute(reader.GetAttribute("group")),
					ReadBoolAttribute(reader, "loved"));
				currentPetFeedRewardGroupDepth = reader.Depth;
				if (reader.IsEmptyElement)
				{
					currentPetFeedFlavour.AddRewardGroup(currentPetFeedRewardGroup.ToSummary());
					currentPetFeedRewardGroup = null;
					currentPetFeedRewardGroupDepth = -1;
				}

				continue;
			}

			if (currentPetFeedRewardGroup != null
				&& reader.Depth == currentPetFeedRewardGroupDepth + 1
				&& reader.LocalName == "result")
			{
				currentPetFeedRewardGroup.AddReward(ReadRequiredIntAttribute(reader, "item"));
				continue;
			}

			if (reader.Depth == 2
				&& elementPath.GetValueOrDefault(1) == "item_groups"
				&& TryGetPetFoodTypeForItemGroupElement(reader.LocalName, out var petFoodType))
			{
				currentPetFoodItemGroup = petFoodType;
				currentPetFoodItemGroupDepth = reader.Depth;
				if (!petFoodGroupItems.ContainsKey(petFoodType))
					petFoodGroupItems[petFoodType] = new HashSet<int>();
				if (reader.IsEmptyElement)
				{
					currentPetFoodItemGroup = null;
					currentPetFoodItemGroupDepth = -1;
				}

				continue;
			}

			if (currentPetFoodItemGroup.HasValue
				&& reader.Depth == currentPetFoodItemGroupDepth + 1
				&& reader.LocalName == "item")
			{
				petFoodGroupItems[currentPetFoodItemGroup.Value].Add(ReadRequiredIntAttribute(reader, "id"));
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
					ReadIntAttribute(reader, "attack_speed"),
					ReadIntAttribute(reader, "state"),
					reader.GetAttribute("ai") ?? string.Empty,
					reader.GetAttribute("group_drop") ?? string.Empty,
					reader.GetAttribute("abyss_type") ?? "NONE");
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

			if (reader.Depth == 3 && reader.LocalName == "kisk_stats" && currentNpcTemplate != null)
			{
				// Java parity: model/templates/stats/KiskStatsTemplate.
				currentNpcTemplate.KiskStats = new KiskStatsSummary(
					ReadOptionalIntAttribute(reader, "usemask", 4),
					ReadOptionalIntAttribute(reader, "members", 6),
					ReadOptionalIntAttribute(reader, "resurrects", 18));
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
				currentNpcTemplate.HasTalkInfo = true;
				currentNpcTemplate.TalkDistance = ReadOptionalIntAttribute(reader, "distance", 2);
				currentNpcTemplate.FunctionDialogIds.AddRange(ReadIntListAttribute(reader, "func_dialogs"));
				currentNpcTemplate.SubDialogType = ReadNpcSubDialogType(reader.GetAttribute("subdialog_type"));
				currentNpcTemplate.SubDialogValue = ReadOptionalIntAttribute(reader, "subdialog_value", 0);
				currentNpcTemplate.CanTalkInvisible = ReadOptionalBoolAttribute(reader, "can_talk_invisible", true);
				currentNpcTemplate.IsDialogNpc = ReadBoolAttribute(reader, "is_dialog");
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
					ReadIntAttribute(reader, "cooldown"),
					reader.GetAttribute("activation") ?? string.Empty)
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

			if (reader.Depth == 4 && reader.LocalName == "signetburst" && currentSkillTemplate != null)
			{
				// Java parity: skillengine/effect/SignetBurstEffect exposes signet and signetlvl attributes.
				currentSkillTemplate.AddSignetBurst(
					new SkillSignetBurstEffectSummary(
						reader.GetAttribute("signet") ?? string.Empty,
						ReadIntAttribute(reader, "signetlvl")));
				continue;
			}

			if (reader.Depth == 4 && IsDropBoostStatEffectElement(reader.LocalName) && currentSkillTemplate != null)
			{
				currentSkillTemplate.StartBuffStatEffect(reader.LocalName);
				if (reader.IsEmptyElement)
					currentSkillTemplate.EndBuffStatEffect();
				continue;
			}

			if (reader.Depth == 5 && reader.LocalName == "change" && currentSkillTemplate != null)
			{
				var change = new SkillStatChange(
					reader.GetAttribute("stat") ?? string.Empty,
					reader.GetAttribute("func") ?? string.Empty,
					ReadIntAttribute(reader, "value"),
					ReadIntAttribute(reader, "delta"));
				currentSkillTemplate.AddCurrentMasteryChange(change);
				currentSkillTemplate.AddCurrentBuffStatChange(change);
				currentSkillTemplate.StartCurrentStatChangeConditions(change);
				continue;
			}

			if (reader.LocalName != "conditions"
				&& elementPath.GetValueOrDefault(reader.Depth - 1) == "conditions"
				&& elementPath.GetValueOrDefault(reader.Depth - 2) == "change"
				&& currentSkillTemplate != null)
			{
				currentSkillTemplate.AddCurrentStatChangeCondition(ReadSkillStatChangeCondition(reader));
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
		using var nearbyQuestTemplateStream = File.OpenRead(cacheFilePath);
		var nearbyQuestTemplates = new NearbyQuestTemplateTable(
			new NearbyQuestTemplateXmlExtractor()
				.Extract(nearbyQuestTemplateStream)
				.Select(template => workOrderRecipes.TryGetRecipeId(template.QuestId, out var recipeId)
					? template with { WorkOrderRecipeId = recipeId }
					: template)
				.ToArray());
		var questHandlers = QuestHandlerAvailabilityTable.Load(cacheFilePath, questHandlerDirectory, cancellationToken);
		var questNpcStarts = LoadQuestNpcStarts(cacheFilePath, questHandlerDirectory, cancellationToken);
		var questCompletionFollowUps = QuestCompletionFollowUpTable.Load(questHandlerDirectory, cancellationToken);
		var processedGlobalDropRules = ProcessGlobalDropRules(globalDropRules, npcTemplates);

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
			new VortexLocationTable(vortexLocations.AsReadOnly()),
			new NpcTemplateTable(npcTemplates.AsReadOnly()),
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
			new QuestUpdateItemTable(questUpdateItemIds.AsReadOnly()),
			new GlobalDropTable(processedGlobalDropRules),
			new EventDropTable(eventTemplates.AsReadOnly()),
			new GlobalNpcExclusionTable(
				globalNpcExclusionNpcIds,
				globalNpcExclusionNpcNames,
				globalNpcExclusionNpcTypes,
				globalNpcExclusionNpcTribes,
				globalNpcExclusionNpcAbyssTypes),
			new SkillTemplateTable(skillTemplates.AsReadOnly()),
			new NpcSkillTable(npcSkillLists.AsReadOnly()),
			new PetSkillTable(petSkills.AsReadOnly()),
			new PetTemplateTable(petTemplates.AsReadOnly()),
			new PetDopingTable(petDopings.AsReadOnly()),
			new PetFeedDataTable(new PetFeedEvaluationContext(
				petFeedFlavours,
				new PetFoodItemGroups(
					petFoodGroupItems.ToDictionary(
						pair => pair.Key,
						pair => (IReadOnlySet<int>)pair.Value)),
				itemTemplates.ToDictionary(template => template.TemplateId, template => template.Level))),
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
			new PortalPathTable(
				portalDialogPaths.AsReadOnly(),
				portalDialogTeleportIds,
				portalUsePaths.AsReadOnly(),
				portalScrollPaths.AsReadOnly()),
			new PortalLocTable(portalLocs.AsReadOnly()),
			new AutoGroupTable(autoGroups.AsReadOnly()),
			new PlayerInitialDataTable(
				creationItemsByClass.ToDictionary(
					pair => pair.Key,
					pair => new PlayerCreationData(pair.Key, pair.Value.AsReadOnly()),
					StringComparer.OrdinalIgnoreCase),
				spawnLocationsByRace),
			new SkillTreeTable(skillTree.AsReadOnly(), new SkillTemplateTable(skillTemplates.AsReadOnly())),
			new StorageExpansionTemplateTable(cubeExpansionTemplates.AsReadOnly()),
			new StorageExpansionTemplateTable(warehouseExpansionTemplates.AsReadOnly()),
			nearbyQuestTemplates,
			questHandlers,
			questNpcStarts,
			questCompletionFollowUps,
			new QuestBonusItemGroupTable(questBonusItemGroups.AsReadOnly()),
			new ChallengeTaskTable(challengeTasks.AsReadOnly()),
			new LegionDominionTable(legionDominions.AsReadOnly()),
			new AtreianPassportTable(atreianPassports.AsReadOnly()),
			new WindstreamTable(windstreamLocations.AsReadOnly()),
			validationTask);
	}

	private static QuestNpcStartTable LoadQuestNpcStarts(
		string cacheFilePath,
		string? questHandlerDirectory,
		CancellationToken cancellationToken)
	{
		var table = new QuestNpcStartTable();
		var questScriptDirectory = Path.GetDirectoryName(cacheFilePath);
		var result = new QuestNpcStartRegistrationSourceLoader()
			.Load(questScriptDirectory, questHandlerDirectory, cancellationToken);
		foreach (var source in result.Sources)
		{
			if (source.EventKind == QuestNpcRegistrationEventKind.OnTalkEvent)
				table.RegisterOnTalkEvent(source);
			else
				table.RegisterOnQuestStart(source);
		}

		return table;
	}

	private static IReadOnlyList<GlobalDropRuleSummary> ProcessGlobalDropRules(
		IReadOnlyList<GlobalDropRuleSummary> rules,
		IReadOnlyList<NpcTemplateSummary> npcTemplates)
	{
		// Java parity: dataholders/GlobalDropData.processRules expands gd_npc_names into gd_npc ids once NPC templates are loaded.
		var processedRules = new List<GlobalDropRuleSummary>(rules.Count);
		foreach (var rule in rules)
		{
			if (rule.NpcNames.Count == 0)
			{
				processedRules.Add(rule);
				continue;
			}

			var allowedNpcIds = rule.NpcIds.ToHashSet();
			foreach (var npcName in rule.NpcNames)
			{
				foreach (var npc in npcTemplates.Where(npc => MatchesGlobalDropNpcName(npcName, npc.Name)))
					allowedNpcIds.Add(npc.TemplateId);
			}

			processedRules.Add(
				allowedNpcIds.Count == 0
					? rule
					: rule with
					{
						NpcIds = allowedNpcIds,
						NpcNames = Array.Empty<GlobalDropNpcNameSummary>(),
					});
		}

		return processedRules.AsReadOnly();
	}

	private static bool TryGetTradeListTemplateKind(string localName, out TradeListTemplateKind kind)
	{
		switch (localName)
		{
			case "tradelist_template":
				kind = TradeListTemplateKind.TradeList;
				return true;
			case "trade_in_list_template":
				kind = TradeListTemplateKind.TradeInList;
				return true;
			case "purchase_template":
				kind = TradeListTemplateKind.PurchaseList;
				return true;
			default:
				kind = default;
				return false;
		}
	}

	private static void AddTradeListTemplate(
		TradeListTemplateSummary template,
		TradeListTemplateKind kind,
		ICollection<TradeListTemplateSummary> tradeLists,
		ICollection<TradeListTemplateSummary> tradeInLists,
		ICollection<TradeListTemplateSummary> purchaseLists)
	{
		switch (kind)
		{
			case TradeListTemplateKind.TradeList:
				tradeLists.Add(template);
				break;
			case TradeListTemplateKind.TradeInList:
				tradeInLists.Add(template);
				break;
			case TradeListTemplateKind.PurchaseList:
				purchaseLists.Add(template);
				break;
		}
	}

	private static void AddGoodsListSummary(
		GoodsListSummary summary,
		GoodsListKind kind,
		ICollection<GoodsListSummary> goodsLists,
		ICollection<GoodsListSummary> goodsInLists,
		ICollection<GoodsListSummary> goodsPurchaseLists)
	{
		switch (kind)
		{
			case GoodsListKind.List:
				goodsLists.Add(summary);
				break;
			case GoodsListKind.InList:
				goodsInLists.Add(summary);
				break;
			case GoodsListKind.PurchaseList:
				goodsPurchaseLists.Add(summary);
				break;
		}
	}

	private static void AddPortalPathSummary(
		PortalPathSummary path,
		ICollection<PortalPathSummary> portalUsePaths,
		ICollection<PortalPathSummary> portalDialogPaths,
		ICollection<PortalPathSummary> portalScrollPaths)
	{
		switch (path.Source)
		{
			case PortalPathSource.Use:
				portalUsePaths.Add(path);
				break;
			case PortalPathSource.Dialog:
				portalDialogPaths.Add(path);
				break;
			case PortalPathSource.Scroll:
				portalScrollPaths.Add(path);
				break;
		}
	}

	private static bool MatchesGlobalDropNpcName(GlobalDropNpcNameSummary ruleName, string npcName)
	{
		var value = ruleName.Value.ToLowerInvariant();
		return ruleName.Function.ToUpperInvariant() switch
		{
			"CONTAINS" => npcName.Contains(value, StringComparison.Ordinal),
			"END_WITH" => npcName.EndsWith(value, StringComparison.Ordinal),
			"START_WITH" => npcName.StartsWith(value, StringComparison.Ordinal),
			"EQUALS" => string.Equals(npcName, ruleName.Value, StringComparison.OrdinalIgnoreCase),
			_ => false,
		};
	}

	private enum TradeListTemplateKind
	{
		TradeList,
		TradeInList,
		PurchaseList,
	}

	private enum GoodsListKind
	{
		List,
		InList,
		PurchaseList,
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

	private static PetFunctionType ReadPetFunctionTypeAttribute(string? value)
	{
		// Java parity: model/templates/pet/PetFunctionType JAXB enum names.
		return value switch
		{
			"WAREHOUSE" => PetFunctionType.WAREHOUSE,
			"FOOD" => PetFunctionType.FOOD,
			"DOPING" => PetFunctionType.DOPING,
			"LOOT" => PetFunctionType.LOOT,
			"BUFF" => PetFunctionType.BUFF,
			"MERCHANT" => PetFunctionType.MERCHANT,
			"NONE" => PetFunctionType.NONE,
			"APPEARANCE" => PetFunctionType.APPEARANCE,
			"BAG" => PetFunctionType.BAG,
			"WING" => PetFunctionType.WING,
			_ => throw new FormatException($"Unexpected PetFunctionType value '{value}'."),
		};
	}

	private static PetFoodType ReadPetFoodTypeAttribute(string? value)
	{
		// Java parity: model/templates/pet/FoodType JAXB enum names.
		return value switch
		{
			"AETHER_CHERRY" => PetFoodType.AetherCherry,
			"AETHER_CRYSTAL_BISCUIT" => PetFoodType.AetherCrystalBiscuit,
			"AETHER_GEM_BISCUIT" => PetFoodType.AetherGemBiscuit,
			"AETHER_POWDER_BISCUIT" => PetFoodType.AetherPowderBiscuit,
			"ARMOR" => PetFoodType.Armor,
			"BALAUR_SCALES" => PetFoodType.BalaurScales,
			"BONES" => PetFoodType.Bones,
			"EXCLUDES" => PetFoodType.Excludes,
			"FLUIDS" => PetFoodType.Fluids,
			"HEALTHY_FOOD_ALL" => PetFoodType.HealthyFoodAll,
			"HEALTHY_FOOD_SPICY" => PetFoodType.HealthyFoodSpicy,
			"MISCELLANEOUS" => PetFoodType.Miscellaneous,
			"POPPY_SNACK" => PetFoodType.PoppySnack,
			"POPPY_SNACK_TASTY" => PetFoodType.PoppySnackTasty,
			"POPPY_SNACK_NUTRITIOUS" => PetFoodType.PoppySnackNutritious,
			"SOULS" => PetFoodType.Souls,
			"SHUGO_EVENT_COIN" => PetFoodType.ShugoEventCoin,
			"STINKY" => PetFoodType.Stinky,
			"THORNS" => PetFoodType.Thorns,
			_ => throw new FormatException($"Unexpected FoodType value '{value}'."),
		};
	}

	private static bool TryGetPetFoodTypeForItemGroupElement(string elementName, out PetFoodType petFoodType)
	{
		// Java parity: dataholders/ItemGroupsData.getPetFood maps FoodType values to these item_groups.xml elements.
		switch (elementName)
		{
			case "feed_crystal_biscuit":
				petFoodType = PetFoodType.AetherCrystalBiscuit;
				return true;
			case "feed_gem_biscuit":
				petFoodType = PetFoodType.AetherGemBiscuit;
				return true;
			case "feed_powder_biscuit":
				petFoodType = PetFoodType.AetherPowderBiscuit;
				return true;
			case "feed_aether_cherry":
				petFoodType = PetFoodType.AetherCherry;
				return true;
			case "feed_armor":
				petFoodType = PetFoodType.Armor;
				return true;
			case "feed_balaur_material":
				petFoodType = PetFoodType.BalaurScales;
				return true;
			case "feed_bone":
				petFoodType = PetFoodType.Bones;
				return true;
			case "feed_fluid":
				petFoodType = PetFoodType.Fluids;
				return true;
			case "feed_soul":
				petFoodType = PetFoodType.Souls;
				return true;
			case "feed_thorn":
				petFoodType = PetFoodType.Thorns;
				return true;
			case "feed_healthy_all":
				petFoodType = PetFoodType.HealthyFoodAll;
				return true;
			case "feed_healthy_spicy":
				petFoodType = PetFoodType.HealthyFoodSpicy;
				return true;
			case "poppy_snack":
				petFoodType = PetFoodType.PoppySnack;
				return true;
			case "tasty_poppy_snack":
				petFoodType = PetFoodType.PoppySnackTasty;
				return true;
			case "nutritious_poppy_snack":
				petFoodType = PetFoodType.PoppySnackNutritious;
				return true;
			case "feed_shugo_event_coin":
				petFoodType = PetFoodType.ShugoEventCoin;
				return true;
			case "stinking_junk":
				petFoodType = PetFoodType.Stinky;
				return true;
			case "feed_exclude":
				petFoodType = PetFoodType.Excludes;
				return true;
			default:
				petFoodType = default;
				return false;
		}
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

	private static long? ReadNullableLongAttribute(XmlReader reader, string attributeName)
	{
		return long.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : null;
	}

	private static bool ReadBoolAttribute(XmlReader reader, string attributeName)
	{
		return bool.TryParse(reader.GetAttribute(attributeName), out var parsed) && parsed;
	}

	private static bool ReadRequiredBoolAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName)
			?? throw new FormatException($"Element <{reader.LocalName}> is missing required attribute '{attributeName}'.");
		return bool.Parse(value);
	}

	private static bool ReadXmlBoolAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName)
			?? throw new FormatException($"Element <{reader.LocalName}> is missing required attribute '{attributeName}'.");
		return value switch
		{
			"1" => true,
			"0" => false,
			_ => bool.Parse(value),
		};
	}

	private static bool ReadOptionalBoolAttribute(XmlReader reader, string attributeName, bool defaultValue)
	{
		return bool.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : defaultValue;
	}

	private static VortexStateType ReadVortexStateTypeAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName);
		return value switch
		{
			"INVASION" => VortexStateType.INVASION,
			"PEACE" => VortexStateType.PEACE,
			_ => throw new FormatException($"Element <{reader.LocalName}> has unexpected VortexStateType '{value}'."),
		};
	}

	private static ItemActionUseTargetType ParseItemActionUseTargetType(string value)
	{
		// Java parity: model/templates/item/actions/UseTarget.fromValue.
		return value switch
		{
			"ACCESSORY" => ItemActionUseTargetType.Accessory,
			"ARMOR" => ItemActionUseTargetType.Armor,
			"EQUIPMENT" => ItemActionUseTargetType.Equipment,
			"WEAPON" => ItemActionUseTargetType.Weapon,
			"WING" => ItemActionUseTargetType.Wing,
			"OTHER" => ItemActionUseTargetType.Other,
			"ALL" => ItemActionUseTargetType.All,
			_ => throw new FormatException($"Unexpected UseTarget value '{value}'."),
		};
	}

	private static NpcSubDialogType? ReadNpcSubDialogType(string? value)
	{
		return value switch
		{
			null or "" => null,
			"FORT_CAPTURE" => NpcSubDialogType.FortCapture,
			"SKILL_ID" => NpcSubDialogType.SkillId,
			"ITEM_ID" => NpcSubDialogType.ItemId,
			"RETURN" => NpcSubDialogType.Return,
			"PCBANG" => NpcSubDialogType.PcBang,
			"PAID_USER" => NpcSubDialogType.PaidUser,
			"NEWBIE" => NpcSubDialogType.Newbie,
			"ABYSSRANK" => NpcSubDialogType.AbyssRank,
			"ABYSSRANKING" => NpcSubDialogType.AbyssRanking,
			"LEVEL" => NpcSubDialogType.Level,
			"LEVEL_LOW" => NpcSubDialogType.LevelLow,
			"LEVEL_HIGH" => NpcSubDialogType.LevelHigh,
			"LEGION_DOMINION_NPC" => NpcSubDialogType.LegionDominionNpc,
			"TARGET_LEGION_DOMINION" => NpcSubDialogType.TargetLegionDominion,
			"PACK_3" => NpcSubDialogType.Pack3,
			"PACK_4" => NpcSubDialogType.Pack4,
			"CASH" => NpcSubDialogType.Cash,
			_ => null,
		};
	}

	private static DateTime? ReadDateTimeAttribute(XmlReader reader, string attributeName)
	{
		return DateTime.TryParse(
			reader.GetAttribute(attributeName),
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out var parsed)
			? parsed
			: null;
	}

	private static DateTime ReadRequiredDateTimeAttribute(XmlReader reader, string attributeName)
	{
		return ReadDateTimeAttribute(reader, attributeName)
			?? throw new FormatException($"Element <{reader.LocalName}> is missing required DateTime attribute '{attributeName}'.");
	}

	private static bool IsStatModifierElement(string elementName)
	{
		return elementName is "add" or "sub" or "rate" or "set" or "abs";
	}

	private static bool IsDropBoostStatEffectElement(string elementName)
	{
		return elementName is "boostdroprate" or "drboost";
	}

	private static SkillStatChangeConditionSummary ReadSkillStatChangeCondition(XmlReader reader)
	{
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
		if (reader.HasAttributes)
		{
			while (reader.MoveToNextAttribute())
				attributes[reader.Name] = reader.Value;
			reader.MoveToElement();
		}

		return new SkillStatChangeConditionSummary(
			reader.LocalName,
			new ReadOnlyDictionary<string, string>(attributes));
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

	private static IReadOnlyList<int> ReadXmlIntListAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName);
		if (string.IsNullOrWhiteSpace(value))
			return Array.Empty<int>();

		return value
			.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(part => int.Parse(part, CultureInfo.InvariantCulture))
			.ToArray();
	}

	private static IReadOnlySet<int> ParseIntSet(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? new HashSet<int>()
			: value
				.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(part => int.Parse(part, CultureInfo.InvariantCulture))
				.ToHashSet();
	}

	private static IReadOnlySet<string> ParseStringSet(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			: value
				.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static bool IsInsideElement(IReadOnlyDictionary<int, string> elementPath, int depth, string elementName)
	{
		return elementPath.Any(pair => pair.Key < depth && pair.Value == elementName);
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
