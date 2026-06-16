using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Services.ToyPet;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Dataholders;

public sealed class DataManager
{
	private DataManager(StaticData staticData)
	{
		StaticData = staticData;
	}

	public StaticData StaticData { get; }

	// Java parity: DataManager exposes its loaded tables as static fields (DataManager.ITEM_DATA, ...).
	// The faithful C# loader is instance-based (DI), so a singleton bridge binds the DI instance at startup
	// (RegisterInstance) and the static *_DATA accessors delegate to its StaticData tables.
	private static DataManager? _instance;

	public static void RegisterInstance(DataManager instance) => _instance = instance;

	private static StaticData SD => (_instance ?? throw new InvalidOperationException(
		"DataManager singleton bridge not initialized; call DataManager.RegisterInstance(...) at startup.")).StaticData;

	public static ItemData ITEM_DATA => SD.ItemDataDh;
	public static SkillData SKILL_DATA => SD.SkillDataDh;
	public static NpcData NPC_DATA => SD.NpcDataDh;
	public static AIData AI_DATA => SD.AiDataDh;
	public static ChestData CHEST_DATA => SD.ChestDataDh;
	public static BindPointData BIND_POINT_DATA => SD.BindPointDataDh;
	public static SpawnsData SPAWNS_DATA { get; } = new();
	public static InstanceCooltimeData INSTANCE_COOLTIME_DATA => SD.InstanceCooltimeDataDh;
	public static PlayerExperienceTable PLAYER_EXPERIENCE_TABLE => SD.PlayerExperienceTable;
	public static TradeListData TRADE_LIST_DATA => SD.TradeListDataDh;
	public static NpcFactionsData NPC_FACTIONS_DATA => SD.NpcFactionsDataDh;
	public static RecipeData RECIPE_DATA => SD.RecipeDataDh;
	public static VortexData VORTEX_DATA => SD.VortexDataDh;
	public static RiftData RIFT_DATA => SD.RiftDataDh;
	public static PetData PET_DATA => SD.PetDataDh;
	public static PetSkillTable PET_SKILL_DATA => SD.PetSkills;
	public static TitleData TITLE_DATA => SD.TitleDataDh;
	public static AtreianPassportData ATREIAN_PASSPORT_DATA => SD.AtreianPassportDataDh;
	public static WalkerData WALKER_DATA => SD.WalkerDataDh;
	public static DecomposableItemsData DECOMPOSABLE_ITEMS_DATA => SD.DecomposableItemsDataDh;
	public static ItemPurificationData ITEM_PURIFICATION_DATA => SD.ItemPurificationDataDh;
	public static GoodsListData GOODSLIST_DATA => SD.GoodsListDataDh;
	public static PlayerInitialData PLAYER_INITIAL_DATA { get; } = new();
	public static ItemRandomBonusData ITEM_RANDOM_BONUSES => SD.ItemRandomBonusDataDh;
	public static AutoGroupData AUTO_GROUP => SD.AutoGroupDataDh;
	public static WalkerVersionTable WALKER_VERSIONS_DATA => SD.WalkerVersions;
	public static SkillTreeData SKILL_TREE_DATA => SD.SkillTreeDataDh;
	public static PortalLocData PORTAL_LOC_DATA => SD.PortalLocDataDh;
	public static PetFeedData PET_FEED_DATA => SD.PetFeedDataDh;
	public static NpcSkillData NPC_SKILL_DATA => SD.NpcSkillDataDh;
	public static CosmeticItemsData COSMETIC_ITEMS_DATA => SD.CosmeticItemsDataDh;
	public static ChallengeData CHALLENGE_DATA => SD.ChallengeDataDh;
	public static WindstreamData WINDSTREAM_DATA => SD.WindstreamDataDh;
	public static WarehouseExpandData WAREHOUSEEXPANDER_DATA => SD.WarehouseExpandDataDh;
	public static CubeExpandData CUBEEXPANDER_DATA => SD.CubeExpandDataDh;
	public static TemperingData TEMPERING_DATA => SD.TemperingDataDh;
	public static StaticDoorData STATICDOOR_DATA => SD.StaticDoorDataDh;
	public static RideData RIDE_DATA => SD.RideDataDh;
	public static PetDopingData PET_DOPING_DATA => SD.PetDopingDataDh;
	public static LegionDominionData LEGION_DOMINION_DATA => SD.LegionDominionDataDh;
	public static ItemSetData ITEM_SET_DATA => SD.ItemSetDataDh;
	public static InstanceExitData INSTANCE_EXIT_DATA => SD.InstanceExitDataDh;
	public static Portal2Data PORTAL2_DATA => SD.Portal2DataDh;
	public static GlobalNpcExclusionData GLOBAL_EXCLUSION_DATA => SD.GlobalNpcExclusionDataDh;
	public static GlobalDropData GLOBAL_DROP_DATA => SD.GlobalDropDataDh;
	public static CustomDrop CUSTOM_NPC_DROP => SD.CustomNpcDropDh;
	public static AssemblyItemsData ASSEMBLY_ITEM_DATA => SD.AssemblyItemsDataDh;
	public static HousingObjectData HOUSING_OBJECT_DATA { get; } = new();
	public static EnchantTable ENCHANT_DATA => SD.EnchantTemplates;
	public static ItemTemplateTable ITEM_TEMPLATES => SD.ItemTemplates;
	public static ItemRandomBonusTable ITEM_RANDOM_BONUS_TABLE => SD.ItemRandomBonuses;
	public static ItemSetTable ITEM_SET_TABLE => SD.ItemSets;
	public static EnchantTable ENCHANT_TABLE => SD.EnchantTemplates;
	public static TemperingTable TEMPERING_TABLE => SD.TemperingTemplates;
	public static TitleTemplateTable TITLE_TEMPLATE_TABLE => SD.TitleTemplates;
	public static HouseData HOUSE_DATA => SD.HouseDataDh;
	public static QuestsData QUEST_DATA => SD.Quests;
	public static TribeRelationsData TRIBE_RELATIONS_DATA => SD.TribeRelations;
	public static WorldMapsData WORLD_MAPS_DATA => SD.WorldMaps2;
	public static NpcShoutData NPC_SHOUT_DATA => SD.NpcShouts;
	public static UpgradeArcadeData UPGRADE_ARCADE_DATA => SD.UpgradeArcade;
	public static SiegeLocationData SIEGE_LOCATION_DATA => SD.SiegeLocations;
	public static ItemGroupsData ITEM_GROUPS_DATA => SD.ItemGroups;
	public static Aion.GameServer.Model.Templates.Mail.Mails SYSTEM_MAIL_TEMPLATES => SD.SystemMailTemplates;
	public static HouseBuildingData HOUSE_BUILDING_DATA => SD.HouseBuildings;
	public static GuideHtmlData GUIDE_HTML_DATA => SD.GuideHtml;
	public static MaterialData MATERIAL_DATA => SD.Materials;
	public static ZoneData ZONE_DATA => SD.ZoneInfo;
	public static XMLQuests XML_QUESTS => SD.XmlQuests;
	public static TownSpawnsData TOWN_SPAWNS_DATA => SD.TownSpawns;
	public static SkillChargeData SKILL_CHARGE_DATA => SD.SkillCharges;
	public static MotionData MOTION_DATA => SD.Motions;
	public static MapWeatherData MAP_WEATHER_DATA => SD.MapWeathers;
	public static EventData EVENT_DATA => SD.Events;
	public static PanelSkillsData PANEL_SKILL_DATA => SD.PanelSkillsDataDh;
	public static ItemRestrictionCleanupData ITEM_CLEAN_UP => SD.ItemRestrictionCleanupDataDh;
	public static ConquerorAndProtectorData CONQUEROR_AND_PROTECTOR_DATA => SD.ConquerorAndProtectorDataDh;
	public static AbsoluteStatsData ABSOLUTE_STATS_DATA => SD.AbsoluteStatsDataDh;
	public static WorldRaidData WORLD_RAID_DATA => SD.WorldRaidDataDh;
	public static TeleporterData TELEPORTER_DATA => SD.TeleporterDataDh;
	public static TeleLocationData TELELOCATION_DATA => SD.TeleLocationDataDh;
	public static SkillAliasLocationData SKILL_ALIAS_LOCATION_DATA => SD.SkillAliasLocationDataDh;
	public static SignetDataTemplates SIGNET_DATA_TEMPLATES => SD.SignetDataTemplatesDh;
	public static ShieldData SHIELD_DATA => SD.ShieldDataDh;
	public static RoadData ROAD_DATA => SD.RoadDataDh;
	public static MultiReturnItemData MULTIRETURN_DATA => SD.MultiReturnItemDataDh;
	public static KillBountyData KILL_BOUNTY_DATA => SD.KillBountyDataDh;
	public static InstanceBuffData INSTANCE_BUFF_DATA => SD.InstanceBuffDataDh;
	public static HousePartsData HOUSE_PARTS_DATA => SD.HousePartsDataDh;
	public static HouseNpcsData HOUSE_NPCS_DATA => SD.HouseNpcsDataDh;
	public static HotspotData HOTSPOT_DATA => SD.HotspotDataDh;
	public static GatherableData GATHERABLE_DATA => SD.GatherableDataDh;
	public static FlyRingData FLY_RING_DATA => SD.FlyRingDataDh;
	public static FlyPathData FLY_PATH => SD.FlyPathDataDh;
	public static CuringObjectsData CURING_OBJECTS_DATA => SD.CuringObjectsDataDh;
	public static BaseData BASE_DATA => SD.BaseDataDh;
	public static AssembledNpcsData ASSEMBLED_NPC_DATA => SD.AssembledNpcsDataDh;

	public static Task<DataManager> LoadAsync(
		string repoRoot,
		string? cacheDirectory = null,
		bool validateWhenCacheChanges = true,
		ILogger? logger = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dataholders/DataManager static_data.xml bootstrap entry point.
		var staticDataDirectory = Path.Combine(repoRoot, "game-server", "data", "static_data");
		var options = new XmlDataLoaderOptions
		{
			MainXmlFilePath = Path.Combine(staticDataDirectory, "static_data.xml"),
			SchemaFilePath = Path.Combine(staticDataDirectory, "static_data.xsd"),
			CacheXmlFilePath = Path.Combine(cacheDirectory ?? Path.Combine(repoRoot, "game-server", "cache"), "static_data.xml"),
			QuestHandlerDirectory = Path.Combine(repoRoot, "game-server", "data", "handlers", "quest"),
			ValidateWhenCacheChanges = validateWhenCacheChanges,
		};

		return LoadAsync(options, logger, cancellationToken);
	}

	public static async Task<DataManager> LoadAsync(XmlDataLoaderOptions options, ILogger? logger = null, CancellationToken cancellationToken = default)
	{
		// Java parity: DataManager delegates static XML load/cache construction.
		var staticData = await XmlDataLoader.LoadStaticDataAsync(options, logger, cancellationToken);

		// Populate the proven faithful per-feature leaf holders (model B) from their source XML so the
		// DataManager.*_DATA accessors that delegate to them return real data at runtime. Guarded internally
		// (file-exists + try/catch) so a missing/malformed feature file never aborts boot. The large/
		// cross-referenced holders (Item/Npc/Skill/Quest/AI) remain deferred.
		var staticDataDirectory = Path.GetDirectoryName(options.MainXmlFilePath);
		if (!string.IsNullOrEmpty(staticDataDirectory))
			staticData.LoadLeafHoldersFromFiles(staticDataDirectory, logger);

		return new DataManager(staticData);
	}
}
