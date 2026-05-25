using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class StaticDataLoadingTests
{
	[Fact]
	public void XmlMerger_MergesSingleRootDirectoryAndReusesCache()
	{
		using var temp = TempDirectory.Create();
		var dataDirectory = Directory.CreateDirectory(Path.Combine(temp.Path, "data", "static_data"));
		var itemsDirectory = Directory.CreateDirectory(Path.Combine(dataDirectory.FullName, "items"));
		File.WriteAllText(
			Path.Combine(dataDirectory.FullName, "static_data.xml"),
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<import file="items" singleRootTag="true" />
			</static_data>
			""");
		File.WriteAllText(Path.Combine(itemsDirectory.FullName, "a.xml"), """<items><item id="1" /></items>""");
		File.WriteAllText(Path.Combine(itemsDirectory.FullName, "b.xml"), """<items><item id="2" /></items>""");

		var cacheFile = Path.Combine(temp.Path, "cache", "static_data.xml");
		var merger = new XmlMerger(Path.Combine(dataDirectory.FullName, "static_data.xml"), cacheFile);

		var firstMerge = merger.Merge();
		var secondMerge = merger.Merge();
		var document = XDocument.Load(cacheFile);

		Assert.True(firstMerge.FileWasModified);
		Assert.False(secondMerge.FileWasModified);
		Assert.True(File.Exists(cacheFile + ".properties"));
		Assert.Equal(2, firstMerge.ImportedFiles.Count);
		Assert.Equal(2, document.Descendants("item").Count());
		Assert.Single(document.Root!.Elements("items"));
		Assert.Empty(document.Descendants("import"));
	}

	[Fact]
	public async Task XmlDataLoader_RunsAsyncValidationForModifiedCache()
	{
		using var temp = TempDirectory.Create();
		var dataDirectory = Directory.CreateDirectory(Path.Combine(temp.Path, "data", "static_data"));
		var itemsDirectory = Directory.CreateDirectory(Path.Combine(dataDirectory.FullName, "items"));
		File.WriteAllText(
			Path.Combine(dataDirectory.FullName, "static_data.xml"),
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<import file="items/items.xml" />
			</static_data>
			""");
		File.WriteAllText(Path.Combine(itemsDirectory.FullName, "items.xml"), """<items><item id="1" /></items>""");
		File.WriteAllText(
			Path.Combine(dataDirectory.FullName, "static_data.xsd"),
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
				<xs:element name="static_data">
					<xs:complexType>
						<xs:sequence>
							<xs:element name="items">
								<xs:complexType>
									<xs:sequence>
										<xs:element name="item" maxOccurs="unbounded">
											<xs:complexType>
												<xs:attribute name="id" type="xs:int" use="required" />
											</xs:complexType>
										</xs:element>
									</xs:sequence>
								</xs:complexType>
							</xs:element>
						</xs:sequence>
					</xs:complexType>
				</xs:element>
			</xs:schema>
			""");

		var staticData = await XmlDataLoader.LoadStaticDataAsync(
			new XmlDataLoaderOptions
			{
				MainXmlFilePath = Path.Combine(dataDirectory.FullName, "static_data.xml"),
				CacheXmlFilePath = Path.Combine(temp.Path, "cache", "static_data.xml"),
				SchemaFilePath = Path.Combine(dataDirectory.FullName, "static_data.xsd"),
				ValidateWhenCacheChanges = true,
			});

		Assert.NotNull(staticData.ValidationTask);
		await staticData.ValidationTask;
		Assert.Equal(1, staticData.GetElementCount("item"));
		Assert.Equal(1, staticData.ImportedFileCount);
	}

	[Fact]
	public async Task StaticData_LoadsStorageExpansionTemplatesByNpcId()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<cube_expander>
					<expansion_npc ids="798008 798037">
						<expand level="1" price="1000" />
					</expansion_npc>
					<expansion_npc ids="279022">
						<expand level="5" price="360000" />
					</expansion_npc>
				</cube_expander>
				<warehouse_expander>
					<expansion_npc ids="203199 203687">
						<expand level="1" price="1200" />
						<expand level="2" price="24000" />
					</expansion_npc>
				</warehouse_expander>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, []);

		Assert.Equal(3, staticData.CubeExpansionTemplates.Count);
		Assert.Equal(2, staticData.WarehouseExpansionTemplates.Count);
		var cubeTemplate = staticData.CubeExpansionTemplates.GetTemplateByNpcId(798037);
		Assert.NotNull(cubeTemplate);
		Assert.Equal(1, cubeTemplate.MinExpansionLevel);
		Assert.Equal(1, cubeTemplate.MaxExpansionLevel);
		Assert.Equal(1000, cubeTemplate.GetPrice(1));
		var abyssTemplate = staticData.CubeExpansionTemplates.GetTemplateByNpcId(279022);
		Assert.Equal(5, abyssTemplate?.MinExpansionLevel);
		Assert.Equal(360000, abyssTemplate?.GetPrice(5));
		var warehouseTemplate = staticData.WarehouseExpansionTemplates.GetTemplateByNpcId(203687);
		Assert.Equal(1, warehouseTemplate?.MinExpansionLevel);
		Assert.Equal(2, warehouseTemplate?.MaxExpansionLevel);
		Assert.Equal(24000, warehouseTemplate?.GetPrice(2));
		Assert.Null(staticData.WarehouseExpansionTemplates.GetTemplateByNpcId(1));
	}

	[Fact]
	public async Task StaticData_LoadsItemPurificationSummaries()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<item_purifications>
					<item_purification base_item_id="100201319">
						<purification_result result_item_id="100201416" min_enchant_count="10" necessary_abyss_points="1374005">
							<req_material item_id="186000242" item_count="143" />
							<req_material item_id="169405379" item_count="1" />
						</purification_result>
						<purification_result result_item_id="100201532" min_enchant_count="15" necessary_abyss_points="4122018" necessary_kinah="1000" />
					</item_purification>
				</item_purifications>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, []);

		Assert.Equal(1, staticData.ItemPurifications.Count);
		Assert.Equal(2, staticData.ItemPurifications.ResultCount);
		var template = staticData.ItemPurifications.GetItemPurificationTemplate(100201319);
		Assert.NotNull(template);
		Assert.Equal(2, template.Results.Count);
		var result = staticData.ItemPurifications.GetResultItem(100201319, 100201416);
		Assert.NotNull(result);
		Assert.Equal(10, result.MinEnchantCount);
		Assert.Equal(1_374_005, result.NecessaryAbyssPoints);
		Assert.Equal(0, result.NecessaryKinah);
		Assert.Equal(new ItemPurificationMaterialSummary(186000242, 143), result.RequiredMaterials[0]);
		var kinahResult = staticData.ItemPurifications.GetResultItem(100201319, 100201532);
		Assert.NotNull(kinahResult);
		Assert.Equal(1_000, kinahResult.NecessaryKinah);
		Assert.Empty(kinahResult.RequiredMaterials);
		Assert.Null(staticData.ItemPurifications.GetResultItem(100201319, 999));
	}

	[Fact]
	public async Task StaticData_LoadsQuestUpdateItemIdsFromQuestInventoryItems()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<quests>
					<quest id="1001">
						<inventory_items>
							<inventory_item item_id="182200001" count="3" />
							<inventory_item item_id="182200002" />
						</inventory_items>
					</quest>
					<quest id="1002">
						<inventory_items>
							<inventory_item item_id="182200001" count="99" />
							<inventory_item item_id="182200003" count="1" />
						</inventory_items>
					</quest>
					<quest id="1003" />
				</quests>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, []);

		Assert.Equal([182200001, 182200002, 182200003], staticData.QuestUpdateItems.ItemIds);
		Assert.Equal(3, staticData.QuestUpdateItems.Count);
		Assert.True(staticData.QuestUpdateItems.ContainsItemId(182200001));
		Assert.True(staticData.QuestUpdateItems.ContainsItemId(182200003));
		Assert.False(staticData.QuestUpdateItems.ContainsItemId(182299999));
	}

	[Fact]
	public async Task StaticData_LoadsPortalPathSummariesWithJavaRaceFallbacks()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<portal_templates2>
					<portal_use npc_id="700438">
						<portal_path loc_id="3000301" race="ELYOS" />
						<portal_path loc_id="3000302" race="ASMODIANS" />
					</portal_use>
					<portal_dialog npc_id="730000" teleport_dialog_id="1012">
						<portal_path dialog="10000" loc_id="3001001" race="ELYOS" min_level="25" min_rank="4" kinah="500" title_id="7" err_group="9001" err_level="9002" siege_id="101" />
						<portal_path dialog="10000" loc_id="3001002" race="ASMODIANS">
							<quest_req quest_id="1044" quest_step="3" />
							<item_req item_id="185000077" item_count="1" />
						</portal_path>
					</portal_dialog>
					<portal_scroll name="scroll_test">
						<portal_path loc_id="4000100" />
					</portal_scroll>
				</portal_templates2>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());

		Assert.Equal(3, staticData.PortalPaths.Count);
		Assert.Equal(5, staticData.PortalPaths.PathCount);
		Assert.True(staticData.PortalPaths.IsPortalNpc(700438));
		Assert.True(staticData.PortalPaths.IsPortalNpc(730000));
		Assert.False(staticData.PortalPaths.IsPortalNpc(700000));
		var usePath = staticData.PortalPaths.GetPortalUsePath(700438, "ASMODIANS");
		Assert.NotNull(usePath);
		Assert.Equal(3000302, usePath.LocId);
		Assert.Equal("ASMODIANS", usePath.Race);
		Assert.Equal(3000301, staticData.PortalPaths.GetPortalUsePath(700438, "ELYOS")?.LocId);
		Assert.Equal(3000302, staticData.PortalPaths.GetPortalUsePath(700438, "BALAUR")?.LocId);
		var dialogPath = staticData.PortalPaths.GetPortalDialogPath(730000, 10000, "ELYOS");
		Assert.NotNull(dialogPath);
		Assert.Equal(3001001, dialogPath.LocId);
		Assert.Equal(25, dialogPath.MinLevel);
		Assert.Equal(4, dialogPath.MinRank);
		Assert.Equal(500, dialogPath.Kinah);
		Assert.Equal(7, dialogPath.TitleId);
		Assert.Equal(9001, dialogPath.ErrGroup);
		Assert.Equal(9002, dialogPath.ErrLevel);
		Assert.Equal(101, dialogPath.SiegeId);
		var restrictedDialogPath = staticData.PortalPaths.GetPortalDialogPath(730000, 10000, "ASMODIANS");
		Assert.NotNull(restrictedDialogPath);
		var questRequirement = Assert.Single(restrictedDialogPath.QuestRequirements);
		Assert.Equal(1044, questRequirement.QuestId);
		Assert.Equal(3, questRequirement.QuestStep);
		var itemRequirement = Assert.Single(restrictedDialogPath.ItemRequirements);
		Assert.Equal(185000077, itemRequirement.ItemId);
		Assert.Equal(1, itemRequirement.ItemCount);
		Assert.Equal(1012, staticData.PortalPaths.GetTeleportDialogId(730000));
		Assert.Equal(1011, staticData.PortalPaths.GetTeleportDialogId(1));
		var scrollPath = staticData.PortalPaths.GetPortalScroll("scroll_test");
		Assert.NotNull(scrollPath);
		Assert.Equal(PortalPathSource.Scroll, scrollPath.Source);
		Assert.Equal("PC_ALL", scrollPath.Race);
		Assert.Equal(4000100, scrollPath.LocId);
	}

	[Fact]
	public async Task StaticData_LoadsPortalLocSummaries()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<portal_locs>
					<portal_loc world_id="110010000" loc_id="1100100" x="1476.3" y="1595.5" z="572.9" />
					<portal_loc world_id="110010000" loc_id="1100101" x="2006.8076" y="1478.2644" z="592.2286" h="53" />
				</portal_locs>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());

		Assert.Equal(2, staticData.PortalLocs.Count);
		var defaultHeading = staticData.PortalLocs.GetPortalLoc(1100100);
		Assert.NotNull(defaultHeading);
		Assert.Equal(110010000, defaultHeading.WorldId);
		Assert.Equal(1476.3f, defaultHeading.X);
		Assert.Equal(1595.5f, defaultHeading.Y);
		Assert.Equal(572.9f, defaultHeading.Z);
		Assert.Equal((byte)0, defaultHeading.Heading);
		var explicitHeading = staticData.PortalLocs.GetPortalLoc(1100101);
		Assert.NotNull(explicitHeading);
		Assert.Equal((byte)53, explicitHeading.Heading);
		Assert.Null(staticData.PortalLocs.GetPortalLoc(1));
	}

	[Fact]
	public async Task StaticData_LoadsRegularNpcSpawnSpotSummaries()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<npc_templates>
					<npc_template npc_id="203072" name="feira" name_id="351010" level="10" rank="DISCIPLINED" rating="NORMAL" race="ELYOS" tribe="GENERAL" type="GENERAL" state="6" ai="general" />
				</npc_templates>
				<spawns>
					<spawn_map map_id="210010000">
						<spawn npc_id="203000" respawn_time="295" difficult_id="1">
							<spot x="10.5" y="20.25" z="30.75" h="44" random_walk="7" walker_id="path-a" walker_index="3" anchor="anchor-a" state="2" ai="guard_ai" />
						</spawn>
						<spawn npc_id="150000015" handler="STATIC">
							<spot x="1" y="2" z="3" static_id="107" />
						</spawn>
						<spawn npc_id="203010" respawn_time="60">
							<temporary_spawn spawn_time="21.*.*" despawn_time="4.*.*" />
							<spot x="11" y="12" z="13" />
						</spawn>
						<spawn npc_id="203011" respawn_time="60">
							<spot x="14" y="15" z="16">
								<temporary_spawn spawn_time="5.*.*" despawn_time="20.*.*" />
							</spot>
						</spawn>
						<rift_spawn id="1" world="210010000">
							<spawn npc_id="203001" respawn_time="60">
								<spot x="4" y="5" z="6" h="7" anchor="rift-master" />
								<spot x="8" y="9" z="10" h="11" anchor="rift-slave" />
							</spawn>
							<spawn npc_id="203002" respawn_time="90" pool="2">
								<spot x="14" y="15" z="16" anchor="rift-pooled-a" />
								<spot x="17" y="18" z="19" anchor="rift-pooled-b" />
							</spawn>
						</rift_spawn>
					</spawn_map>
				</spawns>
				<town_spawns_data>
					<spawn_map map_id="700010000">
						<town_spawn town_id="1001">
							<town_level level="1">
								<spawn npc_id="831222" respawn_time="295">
									<spot x="7" y="8" z="9" />
								</spawn>
							</town_level>
						</town_spawn>
					</spawn_map>
				</town_spawns_data>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, []);

		Assert.Equal(6, staticData.NpcTemplates.GetNpcTemplate(203072)?.State);
		Assert.Equal("general", staticData.NpcTemplates.GetNpcTemplate(203072)?.AiName);
		Assert.Equal(4, staticData.NpcSpawns.Count);
		var spawn = Assert.Single(staticData.NpcSpawns.GetSpawnsForMap(210010000), spot => spot.NpcId == 203000);
		Assert.Equal(10.5f, spawn.X);
		Assert.Equal(20.25f, spawn.Y);
		Assert.Equal(30.75f, spawn.Z);
		Assert.Equal((byte)44, spawn.Heading);
		Assert.Equal(295, spawn.RespawnSeconds);
		Assert.Equal((byte)1, spawn.DifficultId);
		Assert.Equal(7, spawn.RandomWalkRange);
		Assert.Equal("path-a", spawn.WalkerId);
		Assert.Equal(3, spawn.WalkerIndex);
		Assert.Equal("anchor-a", spawn.Anchor);
		Assert.Equal(2, spawn.State);
		Assert.Equal("guard_ai", spawn.AiName);
		Assert.False(spawn.HasTemporarySchedule);
		var staticSpawn = Assert.Single(staticData.NpcSpawns.GetSpawnsForMap(210010000), spot => spot.NpcId == 150000015);
		Assert.Equal("STATIC", staticSpawn.Handler);
		Assert.Equal(107, staticSpawn.StaticId);
		Assert.False(staticSpawn.HasTemporarySchedule);
		var temporaryGroupSpawn = Assert.Single(staticData.NpcSpawns.GetSpawnsForMap(210010000), spot => spot.NpcId == 203010);
		Assert.True(temporaryGroupSpawn.HasTemporarySchedule);
		Assert.NotNull(temporaryGroupSpawn.GroupTemporarySchedule);
		Assert.True(temporaryGroupSpawn.GroupTemporarySchedule.IsInSpawnTime(21 * 60, DayOfWeek.Friday));
		Assert.False(temporaryGroupSpawn.GroupTemporarySchedule.IsInSpawnTime(20 * 60, DayOfWeek.Friday));
		var temporarySpotSpawn = Assert.Single(staticData.NpcSpawns.GetSpawnsForMap(210010000), spot => spot.NpcId == 203011);
		Assert.True(temporarySpotSpawn.HasTemporarySchedule);
		Assert.NotNull(temporarySpotSpawn.SpotTemporarySchedule);
		Assert.True(temporarySpotSpawn.SpotTemporarySchedule.IsInSpawnTime(5 * 60, DayOfWeek.Friday));
		Assert.False(temporarySpotSpawn.SpotTemporarySchedule.IsInSpawnTime(21 * 60, DayOfWeek.Friday));
		Assert.Equal(4, staticData.NpcRiftSpawns.Count);
		var riftSpawns = staticData.NpcRiftSpawns.GetSpawnsForRift(1);
		Assert.Equal(4, riftSpawns.Count);
		var masterRiftSpawn = Assert.Single(riftSpawns, spawn => spawn.Anchor == "rift-master");
		Assert.Equal(210010000, masterRiftSpawn.MapId);
		Assert.Equal(203001, masterRiftSpawn.NpcId);
		Assert.Equal(4, masterRiftSpawn.X);
		Assert.Equal(5, masterRiftSpawn.Y);
		Assert.Equal(6, masterRiftSpawn.Z);
		Assert.Equal((byte)7, masterRiftSpawn.Heading);
		Assert.True(staticData.NpcRiftSpawns.TryGetSpawnByAnchor("rift-master", out var anchoredMaster));
		Assert.Equal(masterRiftSpawn, anchoredMaster);
		Assert.True(staticData.NpcRiftSpawns.TryGetSpawnByAnchor("rift-slave", out var anchoredSlave));
		Assert.Equal(1, anchoredSlave?.SpotIndex);
		Assert.True(staticData.NpcRiftSpawns.TryGetSpawnByAnchor("rift-pooled-a", out var anchoredPooled));
		Assert.Equal(2, anchoredPooled?.PoolSize);
		Assert.False(staticData.NpcRiftSpawns.TryGetSpawnByAnchor("rift-pooled-b", out _));
		Assert.Empty(staticData.NpcSpawns.GetSpawnsForMap(700010000));
	}

	[Fact]
	public async Task StaticData_LoadsRiftLocations()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<rift_locations>
					<rift_location id="2120" world="210020000" />
					<rift_location id="2153" world="210050000" has_spawns="true" />
					<rift_location id="2189" world="210070000" auto_closeable="false" />
				</rift_locations>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, []);

		Assert.Equal(3, staticData.RiftLocations.Count);
		Assert.True(staticData.RiftLocations.Contains(2120));
		var defaultRift = staticData.RiftLocations.GetLocation(2120);
		Assert.NotNull(defaultRift);
		Assert.Equal(210020000, defaultRift.WorldId);
		Assert.False(defaultRift.HasSpawns);
		Assert.True(defaultRift.AutoCloseable);
		var guardedRift = staticData.RiftLocations.GetLocation(2153);
		Assert.NotNull(guardedRift);
		Assert.True(guardedRift.HasSpawns);
		Assert.True(guardedRift.AutoCloseable);
		var invasionRift = staticData.RiftLocations.GetLocation(2189);
		Assert.NotNull(invasionRift);
		Assert.False(invasionRift.AutoCloseable);
		var worldRift = Assert.Single(staticData.RiftLocations.GetLocationsForWorld(210020000));
		Assert.Equal(defaultRift, worldRift);
		Assert.Empty(staticData.RiftLocations.GetLocationsForWorld(220020000));
	}

	[Fact]
	public async Task StaticData_LoadsVortexLocations()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<dimensional_vortex>
					<vortex_location id="0" defends_race="ELYOS" offence_race="ASMODIANS">
						<home_point map="120080000" x="559.4" y="207.8" z="93.5" h="0" />
						<resurrection_point map="210060000" x="951.0" y="2433.0" z="107.0" h="0" />
						<start_point map="210060000" x="951.0" y="2433.0" z="107.0" h="0" />
					</vortex_location>
					<vortex_location id="1" defends_race="ASMODIANS" offence_race="ELYOS">
						<home_point map="110070000" x="452.6" y="237.1" z="127.0" h="0" />
						<resurrection_point map="220050000" x="2237.3" y="2801.5" z="73.3" h="0" />
						<start_point map="220050000" x="2242.0" y="2797.0" z="75.4" h="0" />
					</vortex_location>
				</dimensional_vortex>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, []);

		Assert.Equal(2, staticData.VortexLocations.Count);
		Assert.Equal(staticData.GetElementCount("vortex_location"), staticData.VortexLocations.Count);
		var theobomos = staticData.VortexLocations.GetLocation(0);
		Assert.NotNull(theobomos);
		Assert.Equal("ELYOS", theobomos.DefendersRace);
		Assert.Equal("ASMODIANS", theobomos.InvadersRace);
		Assert.Equal(new WorldPosition(120080000, 559.4f, 207.8f, 93.5f, 0), theobomos.HomePoint);
		Assert.Equal(new WorldPosition(210060000, 951.0f, 2433.0f, 107.0f, 0), theobomos.ResurrectionPoint);
		Assert.Equal(new WorldPosition(210060000, 951.0f, 2433.0f, 107.0f, 0), theobomos.StartPoint);
		Assert.Equal(120080000, theobomos.HomeWorldId);
		Assert.Equal(210060000, theobomos.InvasionWorldId);
		Assert.Equal(theobomos, staticData.VortexLocations.GetLocationByInvasionWorld(210060000));
		var brusthonin = staticData.VortexLocations.GetLocation(1);
		Assert.NotNull(brusthonin);
		Assert.Equal(new WorldPosition(220050000, 2242.0f, 2797.0f, 75.4f, 0), brusthonin.StartPoint);
		Assert.Null(staticData.VortexLocations.GetLocationByInvasionWorld(400010000));
	}

	[Fact]
	public async Task StaticData_LoadsWalkerTemplates()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<npc_walker>
					<walker_template route_id="route-a" pool="2" loop_type="WALK_BACK">
						<routestep x="1" y="2" z="3" rest_time="7" />
						<routestep x="4" y="5" z="6" />
						<routestep x="7" y="8" z="9" />
					</walker_template>
					<walker_template route_id="route-b" formation="SQUARE" rows="1,2">
						<routestep x="10" y="11" z="12" />
					</walker_template>
					<walker_template route_id="route-c" formation="SQUARE">
						<routestep x="13" y="14" z="15" />
					</walker_template>
				</npc_walker>
				<walker_versions>
					<walk_parent id="route-parent">
						<version id="route-a" />
						<version id="route-b" />
					</walk_parent>
				</walker_versions>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, []);

		Assert.Equal(3, staticData.WalkerTemplates.Count);
		var walkBack = staticData.WalkerTemplates.GetWalkerTemplate("route-a");
		Assert.NotNull(walkBack);
		Assert.Equal(2, walkBack.Pool);
		Assert.Equal("SQUARE", walkBack.Formation);
		Assert.Equal([2], walkBack.Rows);
		Assert.Equal("WALK_BACK", walkBack.LoopType);
		Assert.Equal(4, walkBack.RouteSteps.Count);
		Assert.Equal(new WalkerRouteStepSummary(4, 5, 6, 0, 3, true), walkBack.RouteSteps[^1]);
		Assert.False(walkBack.RouteSteps[0].IsLastStep);
		Assert.Equal(7, walkBack.RouteSteps[0].RestTime);

		var squareRows = staticData.WalkerTemplates.GetWalkerTemplate("route-b");
		Assert.NotNull(squareRows);
		Assert.Equal("SQUARE", squareRows.Formation);
		Assert.Equal([1, 2], squareRows.Rows);

		var missingRows = staticData.WalkerTemplates.GetWalkerTemplate("route-c");
		Assert.NotNull(missingRows);
		Assert.Equal("POINT", missingRows.Formation);
		Assert.Empty(missingRows.Rows);
		Assert.Equal(2, staticData.WalkerVersions.Count);
		Assert.True(staticData.WalkerVersions.IsRouteVersioned("route-a"));
		Assert.Equal("route-parent", staticData.WalkerVersions.GetRouteVersionId("route-b"));
		Assert.False(staticData.WalkerVersions.IsRouteVersioned("route-c"));
	}

	[Fact]
	public void WorldMapSummary_ParseFlagsMatchesJavaZoneAttributes()
	{
		var flags = WorldMapSummary.ParseFlags("BIND RECALL GLIDE FLY RIDE FLY_RIDE PVP DUEL_SAME_RACE DUEL_OTHER_RACE NO_RETURN_BATTLE");

		Assert.Equal((WorldZoneAttributes)1023, flags);
		Assert.True((flags & WorldZoneAttributes.Fly) != 0);
		Assert.True((flags & WorldZoneAttributes.NoReturnBattle) != 0);
	}

	[Fact]
	public void WorldMapSummary_HasOverriddenOptionMatchesJavaWorldMap()
	{
		var flyMap = new WorldMapSummary(
			400010000,
			IsInstance: false,
			TwinCount: 1,
			Flags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide);
		var glideOnlyMap = new WorldMapSummary(
			210010000,
			IsInstance: false,
			TwinCount: 5,
			Flags: WorldZoneAttributes.Glide);

		Assert.False(flyMap.HasOverriddenOption(WorldZoneAttributes.Fly, flyMap.Flags));
		Assert.True(flyMap.HasOverriddenOption(WorldZoneAttributes.Fly, flyMap.Flags & ~WorldZoneAttributes.Fly));
		Assert.False(flyMap.IsFlightAllowed(flyMap.Flags & ~WorldZoneAttributes.Fly));

		Assert.False(glideOnlyMap.HasOverriddenOption(WorldZoneAttributes.Fly, glideOnlyMap.Flags));
		Assert.True(glideOnlyMap.HasOverriddenOption(WorldZoneAttributes.Fly, glideOnlyMap.Flags | WorldZoneAttributes.Fly));
		Assert.True(glideOnlyMap.IsFlightAllowed(glideOnlyMap.Flags | WorldZoneAttributes.Fly));
		Assert.True(glideOnlyMap.CanGlide(glideOnlyMap.Flags));
	}

	[Fact]
	public void FlightZoneSummary_CanFlyCanGlideMatchesJavaZoneInstanceOptions()
	{
		var flyAndGlideMap = new WorldMapSummary(
			400010000,
			IsInstance: false,
			TwinCount: 1,
			Flags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide);
		var glideOnlyMap = new WorldMapSummary(
			210010000,
			IsInstance: false,
			TwinCount: 5,
			Flags: WorldZoneAttributes.Glide);
		var inheritZone = CreateFlightZone(flags: -1);
		var zeroFlagsZone = CreateFlightZone(flags: 0);
		var glideOnlyZone = CreateFlightZone(flags: (int)WorldZoneAttributes.Glide);
		var flyOnlyZone = CreateFlightZone(flags: (int)WorldZoneAttributes.Fly);

		Assert.True(inheritZone.CanFly(flyAndGlideMap, flyAndGlideMap.Flags));
		Assert.True(inheritZone.CanGlide(flyAndGlideMap, flyAndGlideMap.Flags));
		Assert.False(zeroFlagsZone.CanFly(glideOnlyMap, glideOnlyMap.Flags));
		Assert.True(zeroFlagsZone.CanGlide(glideOnlyMap, glideOnlyMap.Flags));

		Assert.False(glideOnlyZone.CanFly(flyAndGlideMap, flyAndGlideMap.Flags));
		Assert.True(glideOnlyZone.CanGlide(flyAndGlideMap, flyAndGlideMap.Flags));
		Assert.True(flyOnlyZone.CanFly(glideOnlyMap, glideOnlyMap.Flags));
		Assert.False(flyOnlyZone.CanGlide(glideOnlyMap, glideOnlyMap.Flags));

		var flyRemovedAtRuntime = flyAndGlideMap.Flags & ~WorldZoneAttributes.Fly;
		var flyAddedAtRuntime = glideOnlyMap.Flags | WorldZoneAttributes.Fly;
		Assert.False(flyOnlyZone.CanFly(flyAndGlideMap, flyRemovedAtRuntime));
		Assert.True(glideOnlyZone.CanFly(glideOnlyMap, flyAddedAtRuntime));
	}

	[Fact]
	public void WorldMapSummary_OptionReadersMatchJavaWorldMap()
	{
		var worldMap = new WorldMapSummary(
			400010000,
			IsInstance: false,
			TwinCount: 1,
			Flags: WorldMapSummary.ParseFlags("BIND RECALL GLIDE FLY RIDE FLY_RIDE PVP DUEL_SAME_RACE DUEL_OTHER_RACE NO_RETURN_BATTLE"));
		var flags = worldMap.Flags;

		Assert.True(worldMap.CanPutKisk(flags));
		Assert.True(worldMap.CanRecall(flags));
		Assert.True(worldMap.CanGlide(flags));
		Assert.True(worldMap.IsFlightAllowed(flags));
		Assert.True(worldMap.CanRide(flags));
		Assert.True(worldMap.CanFlyRide(flags));
		Assert.True(worldMap.IsPvpAllowed(flags));
		Assert.True(worldMap.IsSameRaceDuelsAllowed(flags));
		Assert.True(worldMap.IsOtherRaceDuelsAllowed(flags));
		Assert.False(worldMap.CanReturnToBattle(flags));

		flags = worldMap.RemoveWorldOption(flags, WorldZoneAttributes.Ride | WorldZoneAttributes.NoReturnBattle);
		Assert.False(worldMap.CanRide(flags));
		Assert.True(worldMap.CanReturnToBattle(flags));

		flags = worldMap.SetWorldOption(flags, WorldZoneAttributes.Ride | WorldZoneAttributes.NoReturnBattle);
		Assert.True(worldMap.CanRide(flags));
		Assert.False(worldMap.CanReturnToBattle(flags));
	}

	[Fact]
	public async Task DataManager_LoadsRealJavaStaticDataManifestCounts()
	{
		using var temp = TempDirectory.Create();
		var repoRoot = FindRepoRoot();

		var manager = await DataManager.LoadAsync(
			repoRoot,
			cacheDirectory: temp.Path,
			validateWhenCacheChanges: false);
		var staticData = manager.StaticData;

		Assert.True(File.Exists(Path.Combine(temp.Path, "static_data.xml")));
		Assert.True(File.Exists(Path.Combine(temp.Path, "static_data.xml.properties")));
		Assert.True(staticData.ImportedFileCount > 600);
		Assert.Equal(102009, staticData.GetElementCount("item_template"));
		Assert.Equal(63287, staticData.GetElementCount("npc_template"));
		Assert.Equal(13570, staticData.GetElementCount("skill_template"));
		Assert.True(staticData.GetElementCount("quest") > 8000);
		Assert.Equal(12494, staticData.GetElementCount("recipe_template"));
		Assert.Equal(staticData.GetElementCount("item_template"), staticData.ItemTemplates.Count);
		Assert.Equal(staticData.GetElementCount("cosmetic_item"), staticData.CosmeticItems.Count);
		Assert.Equal(staticData.GetElementCount("npc_template"), staticData.NpcTemplates.Count);
		Assert.Equal(staticData.GetElementCount("skill_template"), staticData.SkillTemplates.Count);
		Assert.Equal(300, staticData.TitleTemplates.Count);
		Assert.Equal(staticData.GetElementCount("recipe_template"), staticData.RecipeTemplates.Count);
		Assert.Equal(staticData.GetElementCount("ride_info"), staticData.RideInfos.Count);
		Assert.Equal(staticData.GetElementCount("item_purification"), staticData.ItemPurifications.Count);
		Assert.Equal(staticData.GetElementCount("purification_result"), staticData.ItemPurifications.ResultCount);
		var purification = staticData.ItemPurifications.GetResultItem(100201319, 100201416);
		Assert.NotNull(purification);
		Assert.Equal(10, purification.MinEnchantCount);
		Assert.Equal(1_374_005, purification.NecessaryAbyssPoints);
		Assert.Equal(new ItemPurificationMaterialSummary(186000242, 143), purification.RequiredMaterials[0]);
		Assert.Equal(staticData.GetElementCount("random_bonus"), staticData.ItemRandomBonuses.Count);
		Assert.Equal(staticData.GetElementCount("itemset"), staticData.ItemSets.Count);
		Assert.Equal(staticData.GetElementCount("enchant_list"), staticData.EnchantTemplates.Count);
		Assert.Equal(staticData.GetElementCount("tempering_list"), staticData.TemperingTemplates.Count);
		Assert.Equal(staticData.GetElementCount("walker_template"), staticData.WalkerTemplates.Count);
		Assert.NotNull(staticData.WalkerTemplates.GetWalkerTemplate("2B608BDFBB378B8479A1DB5321532BEC54C38823"));
		Assert.Equal(staticData.GetElementCount("version"), staticData.WalkerVersions.Count);
		Assert.Equal("1B5A84B85B8F8499B49A0840E90A25E686B00802", staticData.WalkerVersions.GetRouteVersionId("6E6042737F819C39511F6C5C4C85AD54B51C6D83"));
		Assert.Equal(staticData.GetElementCount("rift_location"), staticData.RiftLocations.Count);
		Assert.Equal(6, staticData.GetElementCount("expansion_npc"));
		Assert.Equal(3, staticData.CubeExpansionTemplates.Templates.Count);
		Assert.Equal(7, staticData.CubeExpansionTemplates.Count);
		var poetaCubeExpansion = staticData.CubeExpansionTemplates.GetTemplateByNpcId(798008);
		Assert.NotNull(poetaCubeExpansion);
		Assert.Equal(1, poetaCubeExpansion.MinExpansionLevel);
		Assert.Equal(1, poetaCubeExpansion.MaxExpansionLevel);
		Assert.Equal(1000, poetaCubeExpansion.GetPrice(1));
		var sanctumCubeExpansion = staticData.CubeExpansionTemplates.GetTemplateByNpcId(798011);
		Assert.NotNull(sanctumCubeExpansion);
		Assert.Equal(1, sanctumCubeExpansion.MinExpansionLevel);
		Assert.Equal(4, sanctumCubeExpansion.MaxExpansionLevel);
		Assert.Equal(180000, sanctumCubeExpansion.GetPrice(4));
		var abyssCubeExpansion = staticData.CubeExpansionTemplates.GetTemplateByNpcId(279022);
		Assert.NotNull(abyssCubeExpansion);
		Assert.Equal(5, abyssCubeExpansion.MinExpansionLevel);
		Assert.Equal(5, abyssCubeExpansion.MaxExpansionLevel);
		Assert.Equal(360000, abyssCubeExpansion.GetPrice(5));
		Assert.Equal(3, staticData.WarehouseExpansionTemplates.Templates.Count);
		Assert.Equal(254, staticData.WarehouseExpansionTemplates.Count);
		var commonWarehouseExpansion = staticData.WarehouseExpansionTemplates.GetTemplateByNpcId(203199);
		Assert.NotNull(commonWarehouseExpansion);
		Assert.Equal(1, commonWarehouseExpansion.MinExpansionLevel);
		Assert.Equal(5, commonWarehouseExpansion.MaxExpansionLevel);
		Assert.Equal(363000, commonWarehouseExpansion.GetPrice(5));
		var limitedWarehouseExpansion = staticData.WarehouseExpansionTemplates.GetTemplateByNpcId(203221);
		Assert.NotNull(limitedWarehouseExpansion);
		Assert.Equal(2, limitedWarehouseExpansion.MinExpansionLevel);
		Assert.Equal(3, limitedWarehouseExpansion.MaxExpansionLevel);
		Assert.Equal(24000, limitedWarehouseExpansion.GetPrice(2));
		var housingWarehouseExpansion = staticData.WarehouseExpansionTemplates.GetTemplateByNpcId(810015);
		Assert.NotNull(housingWarehouseExpansion);
		Assert.Equal(1, housingWarehouseExpansion.MinExpansionLevel);
		Assert.Equal(3, housingWarehouseExpansion.MaxExpansionLevel);
		Assert.Equal(72600, housingWarehouseExpansion.GetPrice(3));
		Assert.Equal(210050000, staticData.RiftLocations.GetLocation(2153)?.WorldId);
		Assert.True(staticData.RiftLocations.GetLocation(2153)?.HasSpawns);
		Assert.False(staticData.RiftLocations.GetLocation(2189)?.AutoCloseable);
		Assert.Contains(staticData.RiftLocations.GetLocationsForWorld(210070000), location => location.Id == 2176 && location.HasSpawns);
		Assert.True(staticData.NpcSpawns.TryGetRiftSpawnByAnchor("ELTNEN_AM", out var eltnenMasterSpawn));
		Assert.Equal(700137, eltnenMasterSpawn?.NpcId);
		Assert.True(staticData.NpcSpawns.TryGetRiftSpawnByAnchor("MORHEIM_AS", out var morheimSlaveSpawn));
		Assert.Equal(700138, morheimSlaveSpawn?.NpcId);
		Assert.Equal(staticData.GetElementCount("vortex_location"), staticData.VortexLocations.Count);
		Assert.Equal(2, staticData.VortexLocations.Count);
		Assert.Equal(new WorldPosition(210060000, 951.0f, 2433.0f, 107.0f, 0), staticData.VortexLocations.GetLocation(0)?.StartPoint);
		Assert.Equal(new WorldPosition(220050000, 2242.0f, 2797.0f, 75.4f, 0), staticData.VortexLocations.GetLocation(1)?.StartPoint);
		Assert.Equal(staticData.VortexLocations.GetLocation(0), staticData.VortexLocations.GetLocationByInvasionWorld(210060000));
		Assert.Equal(staticData.VortexLocations.GetLocation(1), staticData.VortexLocations.GetLocationByInvasionWorld(220050000));
		Assert.Equal(174, staticData.CustomNpcDrops.Count);
		Assert.Equal(2, staticData.CustomNpcDrops.GetNpcDrop(210582)?.Groups.Count);
		Assert.Equal(182400001, staticData.CustomNpcDrops.GetNpcDrop(212928)?.Groups[0].Drops[0].ItemId);
		Assert.Equal(staticData.GetElementCount("quest_drop"), staticData.QuestDrops.Count);
		Assert.Equal(6090, staticData.QuestDrops.Count);
		var kerubimDrop = Assert.Single(staticData.QuestDrops.GetQuestDrops(210671));
		Assert.Equal(1001, kerubimDrop.QuestId);
		Assert.Equal(182200001, kerubimDrop.ItemId);
		Assert.Equal(100, kerubimDrop.Chance);
		Assert.Equal(1, kerubimDrop.DropEachMember);
		Assert.Equal(7, kerubimDrop.CollectingStep);
		var kerubimCollectItem = Assert.Single(kerubimDrop.CollectItems);
		Assert.Equal(182200001, kerubimCollectItem.ItemId);
		Assert.Equal(3, kerubimCollectItem.Count);
		Assert.Equal(2196, staticData.GlobalDrops.Count);
		Assert.Equal(18557, staticData.GlobalDrops.Rules.Sum(rule => rule.Items.Count));
		var kinahRule = staticData.GlobalDrops.Rules.First(rule => rule.RuleName == "Kinah");
		Assert.Equal(50f, kinahRule.Chance);
		Assert.True(kinahRule.DynamicChance);
		Assert.Contains("ELYOS", kinahRule.Races);
		Assert.Contains("ASMODIANS", kinahRule.Races);
		var spiritRule = staticData.GlobalDrops.Rules.First(rule => rule.RuleName == "Morphable Spirit Essences");
		Assert.Empty(spiritRule.NpcNames);
		Assert.Contains(201007, spiritRule.NpcIds);
		var kinahItem = Assert.Single(kinahRule.Items);
		Assert.Equal(182400001, kinahItem.ItemId);
		Assert.Equal(5, kinahItem.MinCount);
		Assert.Equal(25, kinahItem.MaxCount);
		Assert.Equal(75, staticData.EventDrops.Count);
		Assert.Equal(119, staticData.EventDrops.Events.Sum(template => template.DropRules.Count));
		var brokenHearts = staticData.EventDrops.Events.First(template => template.Name == "Broken Hearts");
		Assert.Equal(new DateTime(2026, 2, 9, 0, 0, 0), brokenHearts.StartDate);
		Assert.Equal(new DateTime(2026, 2, 22, 23, 59, 59), brokenHearts.EndDate);
		Assert.Equal("VALENTINE", brokenHearts.Theme);
		Assert.Equal(4, brokenHearts.DropRules.Count);
		Assert.Contains(staticData.EventDrops.GetActiveDropRules(new DateTime(2026, 2, 10, 12, 0, 0)), rule => rule.RuleName == "Broken Hearts L");
		Assert.DoesNotContain(staticData.EventDrops.GetActiveDropRules(new DateTime(2026, 2, 10, 12, 0, 0), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Broken Hearts" }), rule => rule.RuleName == "Broken Hearts L");
		Assert.False(staticData.GlobalNpcExclusions.IsEmpty);
		Assert.Contains(219501, staticData.GlobalNpcExclusions.NpcIds);
		Assert.Contains("SUMMON_PET", staticData.GlobalNpcExclusions.NpcTemplateTypes);
		Assert.Contains("PET", staticData.GlobalNpcExclusions.NpcTribes);
		Assert.Contains("DOOR", staticData.GlobalNpcExclusions.NpcAbyssTypes);
		Assert.Equal(staticData.GetElementCount("instance_cooltime"), staticData.InstanceCooltimes.Count);
		Assert.Equal("SWORD", staticData.ItemTemplates.GetItemTemplate(100000001)?.ItemGroup);
		Assert.Equal([37, 44], staticData.ItemTemplates.GetItemTemplate(100000001)?.RequiredEquipSkills);
		Assert.Equal(3, staticData.ItemTemplates.GetItemTemplate(100000094)?.ValidEquipmentSlots);
		Assert.Equal(20, staticData.ItemTemplates.GetItemTemplate(169000005)?.WeaponBoost);
		Assert.Equal(188950002, staticData.ItemTemplates.GetItemTemplate(100000216)?.DispositionItemId);
		Assert.Equal(6, staticData.ItemTemplates.GetItemTemplate(100000216)?.DispositionItemCount);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169500916)?.IsClassSpecific("RANGER"));
		Assert.False(staticData.ItemTemplates.GetItemTemplate(169500916)?.IsClassSpecific("ASSASSIN"));
		Assert.Equal(25, staticData.ItemTemplates.GetItemTemplate(100001115)?.GetRequiredLevel("GLADIATOR"));
		Assert.Equal(39, staticData.ItemTemplates.GetItemTemplate(100001115)?.GetMaxLevelRestrict("GLADIATOR"));
		Assert.Equal("FEMALE", staticData.ItemTemplates.GetItemTemplate(110900040)?.GenderPermitted);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(110900040)?.IsItemDyePermitted);
		Assert.Equal(155000001, staticData.ItemTemplates.GetItemTemplate(152200001)?.CraftLearnRecipeId);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(152000065)?.ActivationCount);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100000895)?.ExpireTimeMinutes);
		Assert.Equal(21, staticData.ItemTemplates.GetItemTemplate(160000001)?.UseDelayId);
		Assert.Equal(5000, staticData.ItemTemplates.GetItemTemplate(160000001)?.UseDelayMillis);
		Assert.Equal(91, staticData.ItemTemplates.LearnableEmotionIds.Count);
		Assert.True(staticData.ItemTemplates.IsLearnableEmotion(64));
		Assert.True(staticData.ItemTemplates.IsLearnableEmotion(155));
		Assert.False(staticData.ItemTemplates.IsLearnableEmotion(140));
		Assert.Equal(64, staticData.ItemTemplates.GetItemTemplate(169600001)?.EmotionLearnId);
		Assert.Equal(0, staticData.ItemTemplates.GetItemTemplate(169600001)?.EmotionLearnMinutes);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169600001)?.HasEmotionLearnAction);
		Assert.Equal(64, staticData.ItemTemplates.GetItemTemplate(169600009)?.EmotionLearnId);
		Assert.Equal(5, staticData.ItemTemplates.GetItemTemplate(169600009)?.EmotionLearnMinutes);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169600009)?.HasEmotionLearnAction);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169945000)?.HasTitleAddAction);
		Assert.Equal(269, staticData.ItemTemplates.GetItemTemplate(169945000)?.TitleAddTitleId);
		Assert.False(staticData.ItemTemplates.GetItemTemplate(169945000)?.HasTitleAddMinutes);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169945001)?.HasTitleAddMinutes);
		Assert.Equal(10081, staticData.ItemTemplates.GetItemTemplate(169945001)?.TitleAddMinutes);
		Assert.Equal(new ItemSkillLearnActionInfo(1, 10, "RANGER"), staticData.ItemTemplates.GetItemTemplate(169500916)?.SkillLearnAction);
		Assert.Equal(new ItemExpandInventoryActionInfo(1, "CUBE"), staticData.ItemTemplates.GetItemTemplate(169630000)?.ExpandInventoryAction);
		Assert.Equal(new ItemExpandInventoryActionInfo(1, "WAREHOUSE"), staticData.ItemTemplates.GetItemTemplate(169640000)?.ExpandInventoryAction);
		Assert.Equal(staticData.GetElementCount("expextract"), staticData.ItemTemplates.Templates.Count(template => template.ExpExtractAction != null));
		Assert.Equal(new ItemExpExtractActionInfo(188052060, false, 33725505), staticData.ItemTemplates.GetItemTemplate(188920011)?.ExpExtractAction);
		Assert.Equal(new ItemExpExtractActionInfo(188052060, true, 100), staticData.ItemTemplates.GetItemTemplate(188920012)?.ExpExtractAction);
		Assert.Equal(staticData.GetElementCount("extract"), staticData.ItemTemplates.Templates.Count(template => template.HasExtractAction));
		Assert.True(staticData.ItemTemplates.GetItemTemplate(165000001)?.HasExtractAction);
		Assert.Equal(staticData.GetElementCount("apextract"), staticData.ItemTemplates.Templates.Count(template => template.ApExtractAction != null));
		Assert.Equal(new ItemApExtractActionInfo(0.2f, "WEAPON"), staticData.ItemTemplates.GetItemTemplate(165005000)?.ApExtractAction);
		Assert.Equal(new ItemApExtractActionInfo(0.5f, "ARMOR"), staticData.ItemTemplates.GetItemTemplate(165005001)?.ApExtractAction);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(100000363)?.CanApExtract);
		Assert.Equal(4900, staticData.ItemTemplates.GetItemTemplate(100000363)?.RequiredAbyssPoints);
		Assert.Equal(new ItemDyeActionInfo(null, 0, false), staticData.ItemTemplates.GetItemTemplate(169100000)?.DyeAction);
		Assert.Equal(new ItemDyeActionInfo(0xc22626, 0, false), staticData.ItemTemplates.GetItemTemplate(169120000)?.DyeAction);
		Assert.Equal(new ItemAnimationActionInfo(1, 2, 3, 4, null, 60), staticData.ItemTemplates.GetItemTemplate(188500000)?.AnimationAction);
		Assert.Equal("cash_hair_type_li_m_01a", staticData.ItemTemplates.GetItemTemplate(169800003)?.CosmeticActionName);
		Assert.Equal("test_preset_type_li_m_01a", staticData.ItemTemplates.GetItemTemplate(169890001)?.CosmeticActionName);
		Assert.Equal(new ItemRemodelActionInfo(1, 0), staticData.ItemTemplates.GetItemTemplate(122001250)?.RemodelAction);
		Assert.Equal(staticData.GetElementCount("houseobject"), staticData.ItemTemplates.Templates.Count(template => template.HasHouseObjectAction));
		Assert.Equal(3000001, staticData.ItemTemplates.GetItemTemplate(170000000)?.HouseObjectTemplateId);
		Assert.Equal(staticData.GetElementCount("housedeco"), staticData.ItemTemplates.Templates.Count(template => template.HasHouseDecorateAction));
		Assert.True(staticData.ItemTemplates.GetItemTemplate(170000023)?.HasHouseDecorateAction);
		Assert.Equal(0, staticData.ItemTemplates.GetItemTemplate(170000023)?.HouseDecorateTemplateId);
		Assert.Equal(3550000, staticData.ItemTemplates.GetItemTemplate(171000000)?.HouseDecorateTemplateId);
		Assert.Equal(2, staticData.ItemTemplates.GetItemTemplate(122001250)?.ExtraInventoryId);
		Assert.Equal(-1, staticData.ItemTemplates.GetItemTemplate(152000065)?.ExtraInventoryId);
		Assert.Equal(staticData.GetElementCount("decompose"), staticData.ItemTemplates.Templates.Count(template => template.HasDecomposeAction));
		Assert.True(staticData.ItemTemplates.GetItemTemplate(152000065)?.HasDecomposeAction);
		Assert.Equal(staticData.GetElementCount("composition"), staticData.ItemTemplates.Templates.Count(template => template.HasCompositionAction));
		Assert.True(staticData.ItemTemplates.GetItemTemplate(165010000)?.HasCompositionAction);
		Assert.Equal(staticData.GetElementCount("decomposable"), staticData.DecomposableItems.Count);
		Assert.True(staticData.DecomposableItems.NormalCount > staticData.DecomposableItems.SelectableCount);
		var pepentoRewards = staticData.DecomposableItems.GetInfoByItemId(152000065);
		Assert.NotNull(pepentoRewards);
		var pepentoGroup = Assert.Single(pepentoRewards);
		Assert.Equal(100f, pepentoGroup.Chance);
		Assert.Equal(0, pepentoGroup.MinLevel);
		Assert.Equal(99, pepentoGroup.MaxLevel);
		var pepentoItem = Assert.Single(pepentoGroup.Items);
		Assert.Equal(152000064, pepentoItem.ItemId);
		Assert.Equal(2, pepentoItem.MinCount);
		Assert.Equal(2, pepentoItem.MaxCount);
		Assert.Equal("PC_ALL", pepentoItem.Race);
		Assert.Empty(pepentoItem.PlayerClasses);
		var selectableRewards = staticData.DecomposableItems.GetSelectableItems(188051090);
		Assert.NotNull(selectableRewards);
		Assert.Contains(selectableRewards, item => item.ItemId == 125045164 && item.MinCount == 1 && item.MaxCount == 1);
		Assert.Contains(selectableRewards, item => item.ItemId == 188053609 && item.MinCount == 3 && item.MaxCount == 3);
		Assert.Null(staticData.DecomposableItems.GetInfoByItemId(188051090));
		var levelGatedRewards = staticData.DecomposableItems.GetInfoByItemId(188051162);
		Assert.NotNull(levelGatedRewards);
		Assert.Contains(
			levelGatedRewards,
			group => group is { Chance: 88f, MinLevel: 1, MaxLevel: 20 }
				&& group.Items.Any(item => item is { ItemId: 186000001, MinCount: 2, MaxCount: 3, Race: "ELYOS" }));
		var classRestrictedRewards = staticData.DecomposableItems.GetInfoByItemId(188051413);
		Assert.NotNull(classRestrictedRewards);
		Assert.Contains(
			classRestrictedRewards.SelectMany(group => group.Items),
			item => item.ItemId == 113600836 && item.HasClassRestrictions && item.PlayerClasses.SetEquals(["GLADIATOR", "TEMPLAR"]));
		var randomRewards = staticData.DecomposableItems.GetInfoByItemId(188050584);
		Assert.NotNull(randomRewards);
		Assert.Contains(
			randomRewards.SelectMany(group => group.RandomItems),
			item => item is { Type: "ENCHANTMENT", MinCount: 1, MaxCount: 3 });
		Assert.Equal(89, staticData.AssemblyItems.Count);
		Assert.Equal(staticData.GetElementCount("assemble"), staticData.ItemTemplates.Templates.Count(template => template.AssemblyItemId != 0));
		var assemblyItem = staticData.AssemblyItems.GetAssemblyItem(186000018);
		Assert.NotNull(assemblyItem);
		Assert.Equal([188100001, 188100002, 188100003, 188100004, 188100005], assemblyItem.Parts);
		Assert.Equal(186000018, staticData.ItemTemplates.GetItemTemplate(188100001)?.AssemblyItemId);
		Assert.Null(staticData.AssemblyItems.GetAssemblyItem(188100001));
		var hairCosmetic = staticData.CosmeticItems.GetCosmeticItemTemplate("cash_hair_type_li_m_01a");
		Assert.NotNull(hairCosmetic);
		Assert.Equal("hair_type", hairCosmetic.Type);
		Assert.Equal(0, hairCosmetic.Id);
		Assert.Equal("ELYOS", hairCosmetic.Race);
		Assert.Equal("MALE", hairCosmetic.GenderPermitted);
		var presetCosmetic = staticData.CosmeticItems.GetCosmeticItemTemplate("test_preset_type_li_m_01a");
		Assert.NotNull(presetCosmetic);
		Assert.Equal("preset_name", presetCosmetic.Type);
		Assert.Equal(1.0f, presetCosmetic.Preset?.Scale);
		Assert.Equal(1, presetCosmetic.Preset?.HairType);
		Assert.Equal(0, presetCosmetic.Preset?.FaceType);
		Assert.Equal(1515812, presetCosmetic.Preset?.HairColor);
		Assert.Equal(5402006, presetCosmetic.Preset?.EyeColor);
		Assert.Equal(13228789, presetCosmetic.Preset?.SkinColor);
		var sprintRide = staticData.RideInfos.GetRideInfo(2000000);
		Assert.NotNull(sprintRide);
		Assert.Equal(12.0f, sprintRide.MoveSpeed);
		Assert.Equal(16.0f, sprintRide.FlySpeed);
		Assert.Equal(15.0f, sprintRide.SprintSpeed);
		Assert.Equal(10, sprintRide.StartFp);
		Assert.Equal(10, sprintRide.CostFp);
		Assert.True(sprintRide.CanSprint());
		Assert.False(staticData.RideInfos.GetRideInfo(2000010)?.CanSprint());
		Assert.Equal(staticData.GetElementCount("ride"), staticData.ItemTemplates.Templates.Count(template => template.RideNpcId != 0));
		Assert.Equal(2000000, staticData.ItemTemplates.GetItemTemplate(190100000)?.RideNpcId);
		Assert.Equal(staticData.GetElementCount("toypetspawn"), staticData.ItemTemplates.Templates.Count(template => template.ToyPetSpawnNpcId != 0));
		Assert.Equal(700273, staticData.ItemTemplates.GetItemTemplate(184000011)?.ToyPetSpawnNpcId);
		Assert.Equal(0, staticData.ItemTemplates.GetItemTemplate(184000011)?.ToyPetSpawnTime);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100000714)?.EnchantType);
		Assert.Equal(15, staticData.ItemTemplates.GetItemTemplate(100100860)?.MaxEnchantLevel);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(100100860)?.CanExceedEnchant);
		Assert.False(staticData.ItemTemplates.GetItemTemplate(100000001)?.CanExceedEnchant);
		Assert.Equal("RANK1_SET2_PHYSICAL_WEAPON", staticData.ItemTemplates.GetItemTemplate(100000216)?.ExceedEnchantSkill);
		Assert.Equal(6, staticData.ItemTemplates.GetItemTemplate(100001384)?.ManastoneSlots);
		Assert.Equal(2, staticData.ItemTemplates.GetItemTemplate(100001384)?.SpecialManastoneSlots);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(100001276)?.CanTune);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100001276)?.MaxTuneCount);
		Assert.False(staticData.ItemTemplates.GetItemTemplate(100000001)?.CanTune);
		Assert.Equal(0, staticData.ItemTemplates.GetItemTemplate(100000001)?.MaxTuneCount);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100001105)?.ConditioningMaxLevel);
		var chargeTemplate = staticData.ItemTemplates.GetItemTemplate(100001105);
		Assert.NotNull(chargeTemplate);
		Assert.Equal(1, chargeTemplate.Improvement?.ChargeWay);
		Assert.Equal(1, chargeTemplate.Improvement?.Level);
		Assert.Equal(200, chargeTemplate.Improvement?.BurnAttack);
		Assert.Equal(100, chargeTemplate.Improvement?.BurnDefend);
		Assert.Equal(10000, chargeTemplate.Improvement?.Price1);
		Assert.Equal(0, chargeTemplate.Improvement?.Price2);
		Assert.Equal(4, chargeTemplate.RecommendRank);
		Assert.Equal(3, chargeTemplate.MinRank);
		Assert.Equal(18, chargeTemplate.MaxRank);
		var fireSword = staticData.ItemTemplates.GetItemTemplate(100000125);
		Assert.NotNull(fireSword);
		Assert.Equal("PHYSICAL", fireSword.AttackType);
		Assert.Equal(70, fireSword.WeaponStats?.MeanDamage);
		Assert.Equal(1400, fireSword.WeaponStats?.AttackSpeed);
		Assert.Equal(29, fireSword.IdianInfo?.BurnAttack);
		Assert.Equal(12, fireSword.IdianInfo?.BurnDefend);
		Assert.Contains(fireSword.StatModifiers, modifier => modifier is { Operation: "add", Name: "PHYSICAL_ATTACK", Value: 7, Bonus: true });
		var conditionedDagger = staticData.ItemTemplates.GetItemTemplate(100201371);
		Assert.NotNull(conditionedDagger);
		Assert.Contains(conditionedDagger.StatModifiers, modifier => modifier is { Operation: "rate", Name: "ATTACK_SPEED", Value: -4, Bonus: true, ChargeCondition: 1 });
		Assert.Equal("WEAPON_TEST", staticData.ItemTemplates.GetItemTemplate(100001673)?.EnchantName);
		Assert.Contains(staticData.EnchantTemplates.GetModifiers(fireSword, 2, 1), modifier => modifier is { Operation: "add", Name: "PHYSICAL_ATTACK", Value: 4, Bonus: false });
		var temperingTestEarring = staticData.ItemTemplates.GetItemTemplate(120001486);
		Assert.NotNull(temperingTestEarring);
		Assert.Equal("TEST_1", temperingTestEarring.TemperingName);
		Assert.Contains(staticData.TemperingTemplates.GetModifiers(temperingTestEarring, 2, 0), modifier => modifier is { Operation: "add", Name: "PHYSICAL_DEFENSE", Value: 10, Bonus: false });
		var physicalPlume = staticData.ItemTemplates.GetItemTemplate(187100011);
		Assert.NotNull(physicalPlume);
		Assert.Contains(staticData.TemperingTemplates.GetModifiers(physicalPlume, 3, 7), modifier => modifier is { Operation: "add", Name: "PHYSICAL_ATTACK", Value: 19, Bonus: true });
		Assert.Contains(staticData.TemperingTemplates.GetModifiers(physicalPlume, 3, 7), modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 450, Bonus: true });
		Assert.Equal(3, staticData.ItemTemplates.GetItemTemplate(166050001)?.PolishSetId);
		Assert.Contains(staticData.ItemRandomBonuses.GetModifiers("POLISH", 3, 1), modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 347, Bonus: true });
		Assert.Equal(1, staticData.ItemRandomBonuses.SelectRandomBonusNumber("POLISH", 3, () => 0));
		Assert.Equal(2, staticData.ItemTemplates.GetItemTemplate(168300003)?.ChargeActionMaxLevel);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(168300003)?.Improvement?.ChargeWay);
		var stigmaStone = staticData.ItemTemplates.GetItemTemplate(140001107);
		Assert.NotNull(stigmaStone);
		Assert.Equal(["FI_WHIRLDRAIN", "FI_WHIRLTORNADO"], stigmaStone.StigmaInfo?.GainSkillGroups);
		Assert.True(stigmaStone.StigmaInfo?.Chargeable);
		var testGodstone = staticData.ItemTemplates.GetItemTemplate(168000001);
		Assert.NotNull(testGodstone);
		Assert.Equal(8255, testGodstone.GodstoneInfo?.SkillId);
		Assert.Equal(1, testGodstone.GodstoneInfo?.SkillLevel);
		Assert.Equal(1000, testGodstone.GodstoneInfo?.Probability);
		var hpManastone = staticData.ItemTemplates.GetItemTemplate(167000226);
		Assert.NotNull(hpManastone);
		Assert.Contains(hpManastone.StatModifiers, modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 20, Bonus: true });
		Assert.Equal(1, hpManastone.EnchantAction?.Count);
		var assuredSupplement = staticData.ItemTemplates.GetItemTemplate(166150017);
		Assert.NotNull(assuredSupplement);
		Assert.Equal(100f, assuredSupplement.EnchantAction?.Chance);
		Assert.Equal(1, assuredSupplement.EnchantAction?.MinLevel);
		Assert.Equal(65, assuredSupplement.EnchantAction?.MaxLevel);
		Assert.True(assuredSupplement.EnchantAction?.ManastoneOnly);
		var randomBonusModifiers = staticData.ItemRandomBonuses.GetModifiers("INVENTORY", 1, 1);
		Assert.Contains(randomBonusModifiers, modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 100, Bonus: true });
		Assert.Contains(randomBonusModifiers, modifier => modifier is { Operation: "add", Name: "MAXMP", Value: -50, Bonus: true });
		var swordShieldSet = staticData.ItemSets.GetItemSetTemplate(2);
		Assert.NotNull(swordShieldSet);
		Assert.Same(swordShieldSet, staticData.ItemSets.GetItemSetTemplateByItemId(100000714));
		Assert.Contains(115000817, swordShieldSet.ItemIds);
		Assert.Contains(swordShieldSet.PartBonuses, bonus => bonus.Count == 2 && bonus.Modifiers.Any(modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 100, Bonus: true }));
		Assert.Contains(swordShieldSet.FullBonus!.Modifiers, modifier => modifier is { Operation: "add", Name: "MAXMP", Value: 100, Bonus: true });
		Assert.Equal("kamikaze worm", staticData.NpcTemplates.GetNpcTemplate(201000)?.Name);
		Assert.Equal("SPAKY", staticData.NpcTemplates.GetNpcTemplate(201002)?.GroupDrop);
		var smallKiskNpc = staticData.NpcTemplates.GetNpcTemplate(700273);
		Assert.NotNull(smallKiskNpc);
		Assert.Equal(new KiskStatsSummary(UseMask: 0, MaxMembers: 6, MaxResurrects: 18), smallKiskNpc.KiskStats);
		Assert.Equal(new KiskStatsSummary(UseMask: 3, MaxMembers: 1, MaxResurrects: 3), staticData.NpcTemplates.GetNpcTemplate(700403)?.KiskStats);
		var abyssBossNpc = staticData.NpcTemplates.GetNpcTemplate(218246);
		Assert.NotNull(abyssBossNpc);
		Assert.Equal("DRAGON", abyssBossNpc.GroupDrop);
		Assert.Equal("BOSS", abyssBossNpc.AbyssType);
		var postmanNpc = staticData.NpcTemplates.GetNpcTemplate(798100);
		Assert.NotNull(postmanNpc);
		Assert.Equal(2256, postmanNpc.MaxHp);
		Assert.Equal(4.23f, postmanNpc.RunSpeed);
		Assert.Equal(0.595f, postmanNpc.BoundRadius);
		Assert.Equal(0, postmanNpc.State);
		Assert.Equal("deliveryman", postmanNpc.AiName);
		Assert.False(postmanNpc.CanTalkInvisible);
		Assert.True(postmanNpc.CanInteract);
		Assert.False(postmanNpc.IsDialogNpc);
		Assert.Equal(6, staticData.NpcTemplates.GetNpcTemplate(203072)?.State);
		var brokerNpc = staticData.NpcTemplates.GetNpcTemplate(799211);
		Assert.NotNull(brokerNpc);
		Assert.Equal(5, brokerNpc.TalkDistance);
		Assert.Equal([33], brokerNpc.FunctionDialogIds);
		Assert.Equal("general", brokerNpc.AiName);
		Assert.False(brokerNpc.CanTalkInvisible);
		Assert.True(brokerNpc.CanInteract);
		Assert.True(brokerNpc.IsDialogNpc);
		Assert.True(brokerNpc.SupportsDialogAction(33));
		Assert.False(brokerNpc.SupportsDialogAction(2));
		Assert.True(staticData.NpcSpawns.Count > 60000, $"NpcSpawns.Count={staticData.NpcSpawns.Count}");
		var brokerSpawn = Assert.Single(staticData.NpcSpawns.GetSpawnsForMap(220070000), spawn => spawn.NpcId == 799211);
		Assert.Equal(1887.75f, brokerSpawn.X);
		Assert.Equal(2878.98f, brokerSpawn.Y);
		Assert.Equal(532.835f, brokerSpawn.Z);
		Assert.Equal((byte)70, brokerSpawn.Heading);
		Assert.Equal(295, brokerSpawn.RespawnSeconds);
		Assert.Equal(8, staticData.SkillTemplates.GetSkillTemplatesByGroup("RA_WHITETIGER").Count);
		Assert.True(staticData.PetSkills.Count > 0);
		Assert.True(staticData.PetSkills.IsPetOrderSkill(3835));
		Assert.Equal(22107, staticData.PetSkills.GetPetOrderSkill(3835, 833288));
		Assert.True(staticData.PetSkills.PetHasSkill(833288, 22107));
		var clothMastery = staticData.SkillTemplates.GetSkillTemplate(40);
		Assert.NotNull(clothMastery);
		Assert.Equal("PASSIVE", clothMastery.Activation);
		Assert.True(clothMastery.IsPassive);
		var armorMastery = Assert.Single(clothMastery.ArmorMastery);
		Assert.Equal("CLOTHES", armorMastery.ArmorType);
		Assert.Equal(1, armorMastery.Value);
		var armorChange = Assert.Single(armorMastery.Changes);
		Assert.Equal("PHYSICAL_DEFENSE", armorChange.Stat);
		Assert.Equal("PERCENT", armorChange.Func);
		Assert.Equal(10, armorChange.Value);
		var swordTraining = staticData.SkillTemplates.GetSkillTemplate(37);
		Assert.NotNull(swordTraining);
		var weaponMastery = Assert.Single(swordTraining.WeaponMastery);
		Assert.Equal("SWORD", weaponMastery.WeaponGroup);
		var weaponChange = Assert.Single(weaponMastery.Changes);
		Assert.Equal("PHYSICAL_ATTACK", weaponChange.Stat);
		Assert.Equal("PERCENT", weaponChange.Func);
		Assert.Equal(16, weaponChange.Value);
		var shieldTraining = staticData.SkillTemplates.GetSkillTemplate(50);
		Assert.NotNull(shieldTraining);
		var shieldMastery = Assert.Single(shieldTraining.ShieldMastery);
		var shieldChange = Assert.Single(shieldMastery.Changes);
		Assert.Equal("BLOCK", shieldChange.Stat);
		Assert.Equal("PERCENT", shieldChange.Func);
		Assert.Equal(5, shieldChange.Value);
		var dualWieldTraining = staticData.SkillTemplates.GetSkillTemplate(55);
		Assert.NotNull(dualWieldTraining);
		var weaponDual = Assert.Single(dualWieldTraining.WeaponDual);
		Assert.Equal(70, weaponDual.Value);
		Assert.Equal(0, weaponDual.Delta);
		Assert.Equal(40, weaponDual.SkillEfficiency);
		Assert.Equal(400, weaponDual.MaxDamageChance);
		Assert.Equal(0, weaponDual.MaxDamageDelta);
		var exhaustingWave = staticData.SkillTemplates.GetSkillTemplate(539);
		Assert.NotNull(exhaustingWave);
		Assert.False(exhaustingWave.IsPassive);
		Assert.Equal("ADVANCED", exhaustingWave.StigmaType);
		Assert.True(exhaustingWave.IsStigmaSkill);
		Assert.Contains(staticData.SkillTree.GetTemplatesForSkill(539, "GLADIATOR", "ELYOS"), skill => skill.Stigma == 2);
		var poetaProtector = staticData.TitleTemplates.GetTitleTemplate(1);
		Assert.NotNull(poetaProtector);
		Assert.Equal("ELYOS", poetaProtector.Race);
		Assert.Contains(poetaProtector.Modifiers, modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 20, Bonus: true });
		Assert.Contains(poetaProtector.Modifiers, modifier => modifier is { Operation: "add", Name: "PHYSICAL_DEFENSE", Value: 5, Bonus: true });
		Assert.Equal(152000401, staticData.RecipeTemplates.GetRecipeTemplateById(155000001)?.ProductId);
		Assert.True(staticData.HousingTemplates.AddressCount > 1000);
		Assert.Equal(9, staticData.HousingTemplates.BuildingCount);
		Assert.Equal(326001, staticData.HousingTemplates.GetAddress(6001)?.LandId);
		Assert.Equal(810018, staticData.HousingTemplates.GetAddress(6001)?.ManagerNpcId);
		Assert.Equal(0, staticData.HousingTemplates.GetAddress(6001)?.TownId);
		Assert.Equal(210040000, staticData.HousingTemplates.GetAddress(6001)?.MapId);
		Assert.Equal(2668.545166f, staticData.HousingTemplates.GetAddress(6001)?.X);
		Assert.Equal(645.303955f, staticData.HousingTemplates.GetAddress(6001)?.Y);
		Assert.Equal(355.70212f, staticData.HousingTemplates.GetAddress(6001)?.Z);
		Assert.Equal(351000, staticData.HousingTemplates.GetAddress(6001)?.DefaultBuildingId);
		Assert.Equal("PERSONAL_FIELD", staticData.HousingTemplates.GetAddress(6001)?.DefaultBuildingType);
		Assert.Equal(1001, staticData.HousingTemplates.GetAddress(10001)?.TownId);
		var studioAddress = staticData.HousingTemplates.GetAddress(2001);
		Assert.NotNull(studioAddress);
		Assert.Equal(720010000, studioAddress.MapId);
		Assert.Equal(355000, studioAddress.DefaultBuildingId);
		Assert.Equal("PERSONAL_INS", studioAddress.DefaultBuildingType);
		Assert.Equal(700010000, studioAddress.ExitMapId);
		Assert.Equal(2573.0f, studioAddress.ExitX);
		Assert.Equal(1961.0f, studioAddress.ExitY);
		Assert.Equal(185.0f, studioAddress.ExitZ);
		Assert.Equal(40, staticData.HousingTemplates.GetAddress(6001)?.MinLevel);
		Assert.Equal(4_000_000, staticData.HousingTemplates.GetAddress(6001)?.MaintenanceFee);
		Assert.Equal(4, staticData.HousingTemplates.GetHouseTypeId(350000));
		Assert.Equal(1, staticData.HousingTemplates.GetHouseTypeId(353000));
		Assert.Equal(276, staticData.HousingTemplates.PartCount);
		Assert.Equal("CP_C", staticData.HousingTemplates.GetBuilding(353000)?.PartsMatch);
		Assert.True(staticData.HousingTemplates.IsPartValidForBuilding(3520000, 353000));
		Assert.False(staticData.HousingTemplates.IsPartValidForBuilding(3500000, 353000));
		var houseDefaultDecor = staticData.HousingTemplates.GetDefaultDecorIds(353000);
		Assert.Equal(19, houseDefaultDecor.Count);
		Assert.Equal(3520000, houseDefaultDecor[0]);
		Assert.Equal(3521000, houseDefaultDecor[1]);
		Assert.Equal(3522001, houseDefaultDecor[2]);
		Assert.Equal(3523000, houseDefaultDecor[3]);
		Assert.Equal(3526000, houseDefaultDecor[4]);
		Assert.Equal(3527000, houseDefaultDecor[5]);
		Assert.All(houseDefaultDecor.Skip(6).Take(6), partId => Assert.Equal(3524000, partId));
		Assert.All(houseDefaultDecor.Skip(12).Take(6), partId => Assert.Equal(3525000, partId));
		Assert.Equal(0, houseDefaultDecor[18]);
		Assert.Equal(
			[3520000, 3521000, 3522001, 3523000, 3526000, 3527000, 3524000, 3525000],
			staticData.HousingTemplates.GetDefaultPartIds(353000));
		Assert.Equal(1511, staticData.HousingObjectTemplates.Count);
		var chairObject = staticData.HousingObjectTemplates.GetTemplate(3000004);
		Assert.NotNull(chairObject);
		Assert.Equal((byte)5, chairObject.TypeId);
		Assert.Equal("chair", chairObject.Kind);
		Assert.Equal("INTERIOR", chairObject.Area);
		Assert.Equal("FLOOR", chairObject.Location);
		Assert.Equal("CHAIR", chairObject.Category);
		Assert.Equal(1, chairObject.UseDays);
		Assert.True(chairObject.CanDye);
		var storageObject = staticData.HousingObjectTemplates.GetTemplate(3000007);
		Assert.NotNull(storageObject);
		Assert.Equal((byte)2, storageObject.TypeId);
		Assert.Equal(1, storageObject.WarehouseId);
		Assert.Equal("STORAGE", storageObject.Limit);
		Assert.Equal(360007, storageObject.NameId);
		Assert.Equal(5.0f, storageObject.TalkingDistance);
		var npcObject = staticData.HousingObjectTemplates.GetTemplate(3001000);
		Assert.NotNull(npcObject);
		Assert.Equal((byte)7, npcObject.TypeId);
		Assert.Equal(810013, npcObject.NpcId);
		Assert.Equal(30, npcObject.UseDays);
		var useObject = staticData.HousingObjectTemplates.GetTemplate(3190001);
		Assert.NotNull(useObject);
		Assert.Equal((byte)1, useObject.TypeId);
		Assert.True(useObject.OwnerOnly);
		Assert.Equal(3000, useObject.DelayMilliseconds);
		Assert.Equal(2.0f, useObject.TalkingDistance);
		Assert.Equal(186000166, useObject.RequiredItemId);
		Assert.Equal(2, useObject.UseActionCheckType);
		Assert.Equal(1, useObject.UseActionRemoveCount);
		Assert.Equal(188051519, useObject.UseActionRewardId);
		var finalRewardUseObject = staticData.HousingObjectTemplates.GetTemplate(3190013);
		Assert.NotNull(finalRewardUseObject);
		Assert.Equal(188051562, finalRewardUseObject.UseActionRewardId);
		Assert.Equal(188051555, finalRewardUseObject.UseActionFinalRewardId);
		Assert.Equal(5, staticData.InstanceCooltimes.GetInstanceCooltimeByWorldId(300030000)?.MaxCount);
		Assert.Equal(6, staticData.InstanceCooltimes.GetInstanceCooltimeByWorldId(300030000)?.MaxMemberLight);
		Assert.Equal(6, staticData.InstanceCooltimes.GetMaxMemberCount(300030000, "ASMODIANS"));
		Assert.Equal(25, staticData.InstanceCooltimes.GetEnterMinLevel(300030000, "ELYOS"));
		Assert.Equal(25, staticData.InstanceCooltimes.GetEnterMinLevel(300030000, "ASMODIANS"));
		Assert.Equal(0, staticData.InstanceCooltimes.GetEnterMaxLevel(300030000, "ELYOS"));
		Assert.True(staticData.InstanceCooltimes.CanEnterMentor(300030000));
		Assert.Equal("DAILY", staticData.InstanceCooltimes.GetInstanceCooltimeByWorldId(300030000)?.CoolTimeType);
		Assert.Equal(900, staticData.InstanceCooltimes.GetInstanceCooltimeByWorldId(300030000)?.EntCoolTime);
		Assert.Equal(staticData.GetElementCount("portal_path"), staticData.PortalPaths.PathCount);
		Assert.True(staticData.PortalPaths.IsPortalNpc(700438));
		Assert.Equal(3000301, staticData.PortalPaths.GetPortalUsePath(700438, "ELYOS")?.LocId);
		Assert.Equal(3000302, staticData.PortalPaths.GetPortalUsePath(700438, "ASMODIANS")?.LocId);
		Assert.Equal(3000302, staticData.PortalPaths.GetPortalUsePath(700438, "BALAUR")?.LocId);
		Assert.Equal(1352, staticData.PortalPaths.GetTeleportDialogId(832998));
		Assert.Equal(1011, staticData.PortalPaths.GetTeleportDialogId(832997));
		Assert.Equal(3006300, staticData.PortalPaths.GetPortalDialogPath(832998, 10000, "ELYOS")?.LocId);
		Assert.Equal(1100100, staticData.PortalPaths.GetPortalScroll("LC1_RETURN_AREA_1")?.LocId);
		var groggetSafePath = staticData.PortalPaths.GetPortalUsePath(730199, "ELYOS");
		Assert.NotNull(groggetSafePath);
		var groggetItemRequirement = Assert.Single(groggetSafePath.ItemRequirements);
		Assert.Equal(185000077, groggetItemRequirement.ItemId);
		Assert.Equal(1, groggetItemRequirement.ItemCount);
		var heironDrakePath = staticData.PortalPaths.GetPortalUsePath(730033, "ELYOS");
		Assert.NotNull(heironDrakePath);
		var heironQuestRequirement = Assert.Single(heironDrakePath.QuestRequirements);
		Assert.Equal(1636, heironQuestRequirement.QuestId);
		Assert.Equal(3, heironQuestRequirement.QuestStep);
		Assert.Equal(staticData.GetElementCount("portal_loc"), staticData.PortalLocs.Count);
		var sanctumPortalLoc = staticData.PortalLocs.GetPortalLoc(1100100);
		Assert.NotNull(sanctumPortalLoc);
		Assert.Equal(110010000, sanctumPortalLoc.WorldId);
		Assert.Equal(1476.3f, sanctumPortalLoc.X);
		Assert.Equal(1595.5f, sanctumPortalLoc.Y);
		Assert.Equal(572.9f, sanctumPortalLoc.Z);
		Assert.Equal((byte)0, sanctumPortalLoc.Heading);
		var secretLibraryExit = staticData.PortalLocs.GetPortalLoc(1100101);
		Assert.NotNull(secretLibraryExit);
		Assert.Equal((byte)53, secretLibraryExit.Heading);
		Assert.Contains(staticData.RecipeTemplates.GetAutolearnRecipes("ELYOS", 40009, 1), recipe => recipe.RecipeId == 155000001);
		var craftPlayer = new Player
		{
			Name = "Kahrun",
			Race = "ELYOS",
			Skills = [new PlayerSkill { SkillId = 40009, SkillLevel = 1 }],
		};
		Assert.True(CraftLearnService.ValidateNewRecipe(craftPlayer, 155000001, staticData).Succeeded);
		craftPlayer.Recipes = [155000001];
		Assert.Equal(CraftLearnFailure.AlreadyKnown, CraftLearnService.ValidateNewRecipe(craftPlayer, 155000001, staticData).Failure);
		Assert.Equal(6, staticData.PlayerInitialData.Count);
		Assert.Equal(210010000, staticData.PlayerInitialData.GetSpawnLocation("ELYOS")?.MapId);
		Assert.Equal(220010000, staticData.PlayerInitialData.GetSpawnLocation("ASMODIANS")?.MapId);
		Assert.Contains(staticData.PlayerInitialData.GetPlayerCreationData("WARRIOR")!.Items, item => item.ItemId == 100000094 && item.Count == 1);
		Assert.Contains(staticData.SkillTree.GetAutoLearnSkills("WARRIOR", "ELYOS", 1, 1), skill => skill.SkillId == 37 && skill.SkillLevel > 0);
		Assert.Equal(staticData.GetElementCount("map"), staticData.WorldMaps.Count);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(manager);
		Assert.Equal(staticData.WorldMaps.Select(map => map.MapId).Distinct().Count(), runtimeContext.WorldMapStates.Count);
		Assert.True(runtimeContext.WorldMapStates.TryGetMap(300020000, out var runtimeFlyingMap));
		Assert.NotNull(runtimeFlyingMap);
		Assert.True(runtimeFlyingMap.IsFlightAllowed);
		Assert.True(staticData.PlayerExperienceTable.MaxLevel > 60, $"MaxLevel={staticData.PlayerExperienceTable.MaxLevel}");
		Assert.Equal(0, staticData.PlayerExperienceTable.GetStartExpForLevel(1));
		Assert.Equal(11, staticData.PlayerExperienceTable.GetLevelForExp(182252));
		Assert.Contains(new WorldMapSummary(
			210010000,
			IsInstance: false,
			TwinCount: 5,
			DropType: "ELYSEA",
			Flags: WorldZoneAttributes.Bind | WorldZoneAttributes.Recall | WorldZoneAttributes.Glide | WorldZoneAttributes.PvpEnabled | WorldZoneAttributes.DuelSameRaceEnabled),
			staticData.WorldMaps);
		Assert.Contains(new WorldMapSummary(
			300030000,
			IsInstance: true,
			TwinCount: 0,
			DropType: "ABYSS_INSTANCE",
			Flags: WorldZoneAttributes.Glide | WorldZoneAttributes.PvpEnabled | WorldZoneAttributes.DuelSameRaceEnabled | WorldZoneAttributes.NoReturnBattle),
			staticData.WorldMaps);
		var flyingMap = Assert.Single(staticData.WorldMaps, map => map.MapId == 300020000);
		Assert.True(flyingMap.AllowsFlight);
		Assert.True(flyingMap.AllowsGlide);
		Assert.Equal(56, staticData.FlightZones.Count);
		var eltnenFlyZone = Assert.Single(staticData.FlightZones.GetZonesByMapId(210020000), zone => zone.Name == "FLYINGZONESHAPE1_4_210020000");
		Assert.Equal(FlightZoneType.Fly, eltnenFlyZone.ZoneType);
		Assert.Equal(-1, eltnenFlyZone.Flags);
		Assert.Equal(87.14319f, eltnenFlyZone.Bottom);
		Assert.Equal(317.1432f, eltnenFlyZone.Top);
		Assert.Equal(9, eltnenFlyZone.Points.Count);
		Assert.True(eltnenFlyZone.Contains(300f, 2700f, 100f));
		var belusNoFlyZone = Assert.Single(staticData.FlightZones.GetZonesByMapId(400020000), zone => zone.Name == "GAB1_01_FLYING_ZONE01_400020000");
		Assert.Equal(FlightZoneType.NoFly, belusNoFlyZone.ZoneType);
		Assert.Equal(48, belusNoFlyZone.Flags);
		Assert.False(belusNoFlyZone.Contains(1030f, 1000f, 1800f));
		Assert.Equal(152, staticData.CreaturePvpZones.Count);
		Assert.Equal(132, staticData.CreaturePvpZones.Zones.Count(zone => zone.ZoneType == CreaturePvpZoneType.Pvp));
		Assert.Equal(20, staticData.CreaturePvpZones.Zones.Count(zone => zone.ZoneType == CreaturePvpZoneType.Siege));
		var heironPvpZone = Assert.Single(staticData.CreaturePvpZones.GetZonesByMapId(210040000), zone => zone.Name == "PVP_87_210040000");
		Assert.Equal(CreaturePvpZoneType.Pvp, heironPvpZone.ZoneType);
		Assert.Equal("PVP_87_210040000", heironPvpZone.ZoneId);
		Assert.Equal(0, heironPvpZone.Flags);
		Assert.Equal(15, heironPvpZone.Points.Count);
		var upperAbyssFortressZone = Assert.Single(staticData.CreaturePvpZones.GetZonesByMapId(210050000), zone => zone.Name == "ABYSS_CASTLE_AREA_2011_210050000");
		Assert.Equal(CreaturePvpZoneType.Siege, upperAbyssFortressZone.ZoneType);
		Assert.Equal(55, upperAbyssFortressZone.Flags);
		Assert.Contains("item_templates", staticData.TopLevelElements);
		Assert.DoesNotContain("import", staticData.TopLevelElements);
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
	}

	private static FlightZoneSummary CreateFlightZone(int flags)
	{
		return new FlightZoneSummary(
			210010000,
			"test_flight_zone",
			FlightZoneType.Fly,
			flags,
			Bottom: 0,
			Top: 100,
			Points: [new ZonePoint2D(0, 0), new ZonePoint2D(10, 0), new ZonePoint2D(10, 10)]);
	}

	private sealed class TempDirectory : IDisposable
	{
		private TempDirectory(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public static TempDirectory Create()
		{
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-static-data-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return new TempDirectory(path);
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(Path, recursive: true);
			}
			catch
			{
			}
		}
	}
}
