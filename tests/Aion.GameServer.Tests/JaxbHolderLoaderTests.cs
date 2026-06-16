using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;

namespace Aion.GameServer.Tests;

/// <summary>
/// Pilot smoke test for the faithful per-holder XML load path (JaxbHolderLoader).
/// Loads the real game-server/data/static_data/bind_points/bind_points.xml into the faithful
/// BindPointData holder and asserts known entries, proving the JAXB-style holder can be populated
/// from its source XML via XmlSerializer + AfterUnmarshal.
/// </summary>
public sealed class JaxbHolderLoaderTests
{
    [Fact]
    public void LoadFromFile_PopulatesBindPointDataFromRealXml()
    {
        var path = ResolveStaticDataFile("bind_points", "bind_points.xml");

        var data = JaxbHolderLoader.LoadFromFile<BindPointData>(path);

        // AfterUnmarshal built the npcId->template index and nulled the raw list.
        Assert.True(data.Size() > 0);

        // Known entry from bind_points.xml: npcid="700013" name="Binding_Stone_akarios" price="47".
        var akarios = data.GetBindPointTemplate(700013);
        Assert.NotNull(akarios);
        Assert.Equal("Binding_Stone_akarios", akarios!.GetName());
        Assert.Equal(700013, akarios.GetNpcId());
        Assert.Equal(47, akarios.GetPrice());

        // Free bind stone (price="-1").
        var abyssLi = data.GetBindPointTemplate(250092);
        Assert.NotNull(abyssLi);
        Assert.Equal(-1, abyssLi!.GetPrice());

        Assert.Null(data.GetBindPointTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesCuringObjectsDataFromRealXml()
    {
        var path = ResolveStaticDataFile("curing_objects", "curing_objects.xml");

        var data = JaxbHolderLoader.LoadFromFile<CuringObjectsData>(path);

        // AfterUnmarshal copied every curing_object row into the list.
        Assert.True(data.Size() > 0);

        var objects = data.GetCuringObject();
        Assert.NotNull(objects);

        // First row: <curing_object map_id="710010000" x="1765.6873" y="2601.5105" z="231.19335" range="10.3"/>
        var first = objects[0];
        Assert.Equal(710010000, first.GetMapId());
        Assert.Equal(10.3f, first.GetRange(), 3);
    }

    [Fact]
    public void LoadFromFile_PopulatesChestDataFromRealXml()
    {
        var path = ResolveStaticDataFile("chests", "chest_templates.xml");

        var data = JaxbHolderLoader.LoadFromFile<ChestData>(path);

        // AfterUnmarshal built the npcId->template index and nulled the raw list.
        Assert.True(data.Size() > 0);

        // Known entry: <chest npc_id="700472"><key_item item_ids="185000036" count="1"/></chest>
        var chest = data.GetChestTemplate(700472);
        Assert.NotNull(chest);
        Assert.Equal(700472, chest!.GetNpcId());
        var keyItems = chest.GetKeyItems();
        Assert.NotNull(keyItems);
        Assert.Single(keyItems!);
        Assert.Equal(1, keyItems![0].GetCount());
        Assert.Contains(185000036, keyItems[0].GetItemIds());

        Assert.Null(data.GetChestTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesRoadDataFromRealXml()
    {
        var path = ResolveStaticDataFile("roads", "roads.xml");

        var data = JaxbHolderLoader.LoadFromFile<RoadData>(path);

        Assert.True(data.Size() > 0);

        var roads = data.GetRoadTemplates();
        Assert.NotNull(roads);

        // First row: <road radius="30.0" map="210030000" name="VERTERON_TO_ELTNEN"> with center/p1/p2/roadexit.
        var first = roads[0];
        Assert.Equal("VERTERON_TO_ELTNEN", first.GetName());
        Assert.Equal(210030000, first.GetMap());
        Assert.Equal(30.0f, first.GetRadius(), 3);
        Assert.NotNull(first.GetCenter());
        Assert.NotNull(first.GetP1());
        Assert.NotNull(first.GetP2());
        Assert.NotNull(first.GetRoadExit());
        // roadexit mapid="210020000" on first road.
        Assert.Equal(210020000, first.GetRoadExit().GetMap());
    }

    [Fact]
    public void LoadFromFile_PopulatesHotspotDataFromRealXml()
    {
        var path = ResolveStaticDataFile("hotspot_template.xml");

        var data = JaxbHolderLoader.LoadFromFile<HotspotData>(path);

        Assert.True(data.Size() > 0);

        // Known entry: <hotspot_location id="13" worldId="210010000" x="807.0" y="1242.0" z="119.0" race="ELYOS" price="44"/>
        var hotspot = data.GetHotspotTemplateById(13);
        Assert.NotNull(hotspot);
        Assert.Equal(13, hotspot!.GetId());
        Assert.Equal(210010000, hotspot.GetWorldId());
        Assert.Equal(Model.Race.ELYOS, hotspot.GetRace());
        Assert.Equal(44L, hotspot.GetPrice());
    }

    [Fact]
    public void LoadFromFile_PopulatesMapWeatherDataFromRealXml()
    {
        var path = ResolveStaticDataFile("weather_table.xml");

        var data = JaxbHolderLoader.LoadFromFile<MapWeatherData>(path);

        // AfterUnmarshal built the mapId->table index and nulled the raw list.
        Assert.True(data.Size() > 0);

        // First map row: <map id="210010000" zone_count="2" weather_count="7">.
        var table = data.GetWeather(210010000);
        Assert.NotNull(table);
        Assert.Equal(210010000, table!.GetMapId());
        Assert.Equal(2, table.GetZoneCount());
        Assert.Equal(7, table.GetWeatherCount());

        // First table entry: <table zone_id="1" rank="2" code="1" name="RAIN"/>.
        var entries = table.GetZoneData();
        Assert.NotNull(entries);
        var first = entries[0];
        Assert.Equal(1, first.GetZoneId());
        Assert.Equal(1, first.GetCode());
        Assert.Equal("RAIN", first.GetWeatherName());
        Assert.False(first.IsBefore());
        Assert.False(first.IsAfter());

        // <table zone_id="1" rank="1" code="2" name="RAIN" before="true"/>.
        Assert.True(entries[1].IsBefore());

        Assert.Null(data.GetWeather(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesKillBountyDataFromRealXml()
    {
        var path = ResolveStaticDataFile("bounties", "kill_bounties.xml");

        var data = JaxbHolderLoader.LoadFromFile<KillBountyData>(path);

        Assert.True(data.Size() > 0);

        var bounties = data.GetKillBounties();
        Assert.NotNull(bounties);

        // First row: <kill_bounty type="PER_X_KILLS" kill_count="1000" is_random_reward="true">
        //   <bounty item_id="168310018" count="1" />
        var first = bounties[0];
        Assert.Equal(Model.Templates.Bounty.BountyType.PER_X_KILLS, first.GetBountyType());
        Assert.Equal(1000, first.GetKillCount());
        Assert.True(first.IsRandomReward());
        // race omitted on this row -> defaults to PC_ALL.
        Assert.Equal(Model.Race.PC_ALL, first.GetRaceCondition());

        var firstBounties = first.GetBounties();
        Assert.NotNull(firstBounties);
        Assert.Single(firstBounties!);
        Assert.Equal(168310018, firstBounties![0].GetItemId());
        Assert.Equal(1, firstBounties[0].GetCount());
    }

    [Fact]
    public void LoadFromFile_PopulatesBaseDataFromRealXml()
    {
        var path = ResolveStaticDataFile("base", "base_locations.xml");

        var data = JaxbHolderLoader.LoadFromFile<BaseData>(path);

        Assert.True(data.Size() > 0);

        var templates = data.GetAllBaseTemplates();
        Assert.NotNull(templates);

        // First row: <base_location id="2120" type="CASUAL" world="210020000" />
        var first = templates[0];
        Assert.Equal(2120, first.GetId());
        Assert.Equal(210020000, first.GetWorldId());
        Assert.Equal(Model.Base.BaseType.CASUAL, first.GetType_());
        // default_occupier omitted -> defaults to BALAUR.
        Assert.Equal(Model.Base.BaseOccupier.BALAUR, first.GetDefaultOccupier());

        // A PANESTERRA_FACTION_CAMP row carries an explicit default_occupier enum.
        var ivy = templates.First(t => t.GetId() == 4211);
        Assert.Equal(Model.Base.BaseType.PANESTERRA_FACTION_CAMP, ivy.GetType_());
        Assert.Equal(Model.Base.BaseOccupier.IVY_TEMPLE, ivy.GetDefaultOccupier());
    }

    [Fact]
    public void LoadFromFile_PopulatesLegionDominionDataFromRealXml()
    {
        var path = ResolveStaticDataFile("legion_dominion_template.xml");

        var data = JaxbHolderLoader.LoadFromFile<LegionDominionData>(path);

        Assert.True(data.Size() > 0);

        var locations = data.GetLocationTemplates();
        Assert.NotNull(locations);

        // First row: <legion_dominion_location id="1" world_id="220080000"
        //   zone="LegionDominionArea_01" race="ASMODIANS" name_id="404623">
        var first = locations[0];
        Assert.Equal(1, first.GetId());
        Assert.Equal(220080000, first.GetWorldId());
        Assert.Equal("LegionDominionArea_01", first.GetZone());
        Assert.Equal(Model.Race.ASMODIANS, first.GetRace());
        Assert.Equal(404623, first.GetL10nId());

        var rewards = first.GetRewards();
        Assert.NotNull(rewards);
        Assert.True(rewards!.Count > 0);
        // First reward: <reward rank="1" item_id="188053896" count="1" />
        Assert.Equal(1, rewards[0].GetRank());
        Assert.Equal(188053896, rewards[0].GetItemId());
        Assert.Equal(1, rewards[0].GetCount());

        // <invasion_rift key_item_id="185000233" rift_id="2289" />
        var rift = first.GetInvasionRift();
        Assert.NotNull(rift);
        Assert.Equal(185000233, rift!.GetKeyItemId());
        Assert.Equal(2289, rift.GetRiftId());
    }

    [Fact]
    public void LoadFromFile_PopulatesGatherableDataFromRealXml()
    {
        var path = ResolveStaticDataFile("gatherables", "gatherable_templates.xml");

        var data = JaxbHolderLoader.LoadFromFile<GatherableData>(path);

        Assert.True(data.Size() > 0);

        // First row: <gatherable_template id="400001" name="Kukuru" nameId="701957" sourceType="VEGETABLE"
        //   harvestCount="3" skillLevel="20" harvestSkill="30002" successAdj="100" failureAdj="100" aerialAdj="100">
        //   <materials><material rate="10000000" nameid="702021" itemid="152000001" name="Kukuru"/></materials>
        var kukuru = data.GetGatherableTemplate(400001);
        Assert.NotNull(kukuru);
        Assert.Equal("Kukuru", kukuru!.GetName());
        Assert.Equal(701957, kukuru.GetL10nId());
        Assert.Equal("VEGETABLE", kukuru.GetSourceType());
        Assert.Equal(3, kukuru.GetHarvestCount());
        Assert.Equal(20, kukuru.GetSkillLevel());
        Assert.Equal(30002, kukuru.GetHarvestSkill());

        var materials = kukuru.GetMaterials();
        Assert.NotNull(materials);
        var mats = materials!.GetMaterial();
        Assert.Single(mats);
        Assert.Equal(152000001, mats[0].GetItemId());
        Assert.Equal(10000000, mats[0].GetRate());

        Assert.Null(data.GetGatherableTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesMultiReturnItemDataFromRealXml()
    {
        var path = ResolveStaticDataFile("items", "multi_return_item.xml");

        var data = JaxbHolderLoader.LoadFromFile<MultiReturnItemData>(path);

        Assert.True(data.Size() > 0);

        // <return_item id="1"><return_loc index="0" worldid="110010000" desc="Sanctum" alias="LC1_Return_Area_1"/>...
        var locs = data.GetReturnLocListById(1);
        Assert.NotNull(locs);
        Assert.True(locs!.Count > 0);
        var first = locs[0];
        Assert.Equal(110010000, first.GetWorldid());
        Assert.Equal("Sanctum", first.GetDesc());
        Assert.Equal("LC1_Return_Area_1", first.GetAlias());

        Assert.Null(data.GetReturnLocListById(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesFlyRingDataFromRealXml()
    {
        var path = ResolveStaticDataFile("fly_rings", "fly_rings.xml");

        var data = JaxbHolderLoader.LoadFromFile<FlyRingData>(path);

        Assert.True(data.Size() > 0);

        var rings = data.GetFlyRingTemplates();
        Assert.NotNull(rings);

        // First row: <fly_ring name="PRIMUM_PLAZA_400010000_1" map="400010000" radius="6.0"> + center/p1/p2.
        var first = rings[0];
        Assert.Equal("PRIMUM_PLAZA_400010000_1", first.GetName());
        Assert.Equal(400010000, first.GetMap());
        Assert.Equal(6.0f, first.GetRadius(), 3);
        Assert.NotNull(first.GetCenter());
        Assert.Equal(959.63165f, first.GetCenter().GetX(), 3);
        Assert.NotNull(first.GetP1());
        Assert.NotNull(first.GetP2());
    }

    [Fact]
    public void LoadFromFile_PopulatesWindstreamDataFromRealXml()
    {
        var path = ResolveStaticDataFile("windstreams", "windstreams.xml");

        var data = JaxbHolderLoader.LoadFromFile<WindstreamData>(path);

        Assert.True(data.Size() > 0);

        // <windstream mapid="900030000"><locations><location id="76" state="1" fly_path="ONE_WAY"/></locations></windstream>
        var stream = data.GetStreamTemplate(900030000);
        Assert.NotNull(stream);
        Assert.Equal(900030000, stream!.GetMapId());
        var locations = stream.GetLocations();
        Assert.NotNull(locations);
        var locs = locations!.GetLocation();
        Assert.True(locs.Count > 0);
        Assert.Equal(76, locs[0].GetId());
        Assert.Equal(1, locs[0].GetState());
        Assert.Equal(Model.Flypath.FlyPathType.ONE_WAY, locs[0].GetFlyPathType());

        Assert.Null(data.GetStreamTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesTeleLocationDataFromRealXml()
    {
        var path = ResolveStaticDataFile("teleport_location.xml");

        var data = JaxbHolderLoader.LoadFromFile<TeleLocationData>(path);

        Assert.True(data.Size() > 0);

        // <teleloc_template loc_id="2" mapid="110010000" name="Sanctum" name_id="400489" posX="1313.25" posY="1512.011" posZ="568.107"/>
        var sanctum = data.GetTelelocationTemplate(2);
        Assert.NotNull(sanctum);
        Assert.Equal(2, sanctum!.GetLocId());
        Assert.Equal(110010000, sanctum.GetMapId());
        Assert.Equal(400489, sanctum.GetL10nId());
        Assert.Equal(1313.25f, sanctum.GetX(), 3);

        // heading omitted on this row -> defaults to 0.
        Assert.Equal(0, sanctum.GetHeading());

        // <teleloc_template loc_id="3" ... heading="100"/>
        var poeta = data.GetTelelocationTemplate(3);
        Assert.NotNull(poeta);
        Assert.Equal(100, poeta!.GetHeading());

        Assert.Null(data.GetTelelocationTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesPetDopingDataFromRealXml()
    {
        var path = ResolveStaticDataFile("pets", "pet_doping.xml");

        var data = JaxbHolderLoader.LoadFromFile<PetDopingData>(path);

        Assert.True(data.Size() > 0);

        // <doping id="1" usedrink="true" usefood="true" usescroll="0"/>
        var dope1 = data.GetDopingTemplate(1);
        Assert.NotNull(dope1);
        Assert.Equal(1, dope1!.GetId());
        Assert.True(dope1.IsUseDrink());
        Assert.True(dope1.IsUseFood());
        Assert.Equal(0, dope1.GetScrollsUsed());

        // <doping id="2" usedrink="false" usefood="true" usescroll="6"/>
        var dope2 = data.GetDopingTemplate(2);
        Assert.NotNull(dope2);
        Assert.False(dope2!.IsUseDrink());
        Assert.True(dope2.IsUseFood());
        Assert.Equal(6, dope2.GetScrollsUsed());

        Assert.Null(data.GetDopingTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesFlyPathDataFromRealXml()
    {
        var path = ResolveStaticDataFile("flypath_template.xml");

        var data = JaxbHolderLoader.LoadFromFile<FlyPathData>(path);

        Assert.True(data.Size() > 0);

        // <flypath_location id="1" sx="85.15" sy="189.13" sz="231.34" sworld="310020000"
        //   ex="218.85" ey="250.49" ez="206.72" eworld="310020000" time="45"/>
        var path1 = data.GetPathTemplate(1);
        Assert.NotNull(path1);
        Assert.Equal(1, path1!.GetId());
        Assert.Equal(85.15f, path1.GetStartX(), 3);
        Assert.Equal(310020000, path1.GetStartWorldId());
        Assert.Equal(218.85f, path1.GetEndX(), 3);
        Assert.Equal(310020000, path1.GetEndWorldId());
        // time="45" seconds -> GetTimeInMs multiplies by 1000.
        Assert.Equal(45000, path1.GetTimeInMs());

        Assert.Null(data.GetPathTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesShieldDataFromRealXml()
    {
        var path = ResolveStaticDataFile("siege", "siege_shields.xml");

        var data = JaxbHolderLoader.LoadFromFile<ShieldData>(path);

        Assert.True(data.Size() > 0);

        var shields = data.GetShieldTemplates();
        Assert.NotNull(shields);

        // <shield id="1011" map="400010000" name="DIVINE_FORTRESS" radius="86.0">
        //   <center x="2137.505" y="1930.4448" z="2334.0"/></shield>
        var divine = shields.First(s => s.GetId() == 1011);
        Assert.Equal("DIVINE_FORTRESS", divine.GetName());
        Assert.Equal(400010000, divine.GetMap());
        Assert.Equal(86.0f, divine.GetRadius(), 3);
        Assert.NotNull(divine.GetCenter());
        Assert.Equal(2137.505f, divine.GetCenter().GetX(), 3);
        Assert.Equal(2334.0f, divine.GetCenter().GetZ(), 3);
    }

    [Fact]
    public void LoadFromFile_PopulatesPortalLocDataFromRealXml()
    {
        var path = ResolveStaticDataFile("portals", "portal_loc.xml");

        var data = JaxbHolderLoader.LoadFromFile<PortalLocData>(path);

        Assert.True(data.Size() > 0);

        // <portal_loc world_id="110010000" loc_id="1100100" x="1476.3" y="1595.5" z="572.9"/>
        var loc = data.GetPortalLoc(1100100);
        Assert.NotNull(loc);
        Assert.Equal(110010000, loc!.GetWorldId());
        Assert.Equal(1100100, loc.GetLocId());
        Assert.Equal(1476.3f, loc.GetX(), 3);
        Assert.Equal(572.9f, loc.GetZ(), 3);
        // h omitted on this row -> defaults to 0.
        Assert.Equal((sbyte)0, loc.GetH());

        // <portal_loc world_id="110010000" loc_id="1100101" ... h="53"/>
        var withH = data.GetPortalLoc(1100101);
        Assert.NotNull(withH);
        Assert.Equal((sbyte)53, withH!.GetH());

        Assert.Null(data.GetPortalLoc(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesSkillAliasLocationDataFromRealXml()
    {
        var path = ResolveStaticDataFile("skills", "alias_locations.xml");

        var data = JaxbHolderLoader.LoadFromFile<SkillAliasLocationData>(path);

        Assert.True(data.Size() > 0);

        // <alias_location name="IDTemple_Low_Furnace_01" world_id="300160000">
        //   <alias_pos x="529.252380" y="1297.261719" z="198"/> ...
        var loc = data.GetSkillAliasLocation("IDTemple_Low_Furnace_01");
        Assert.NotNull(loc);
        Assert.Equal("IDTemple_Low_Furnace_01", loc!.GetAliasName());
        Assert.Equal(300160000, loc.GetWorldId());

        var positions = loc.GetSkillAliasPositionList();
        Assert.NotNull(positions);
        Assert.True(positions!.Count > 0);
        Assert.Equal(529.252380f, positions[0].GetX(), 3);
        Assert.Equal(1297.261719f, positions[0].GetY(), 3);
        Assert.Equal(198f, positions[0].GetZ(), 3);

        Assert.Null(data.GetSkillAliasLocation("__nope__"));
    }

    [Fact]
    public void LoadFromFile_PopulatesInstanceBuffDataFromRealXml()
    {
        var path = ResolveStaticDataFile("instance_bonusattr", "instance_bonusattr.xml");

        var data = JaxbHolderLoader.LoadFromFile<InstanceBuffData>(path);

        Assert.True(data.Size() > 0);

        // <instance_bonusattr buff_id="1">
        //   <penalty_attr stat="PHYSICAL_ACCURACY" func="ADD" value="1100"/> ...
        //   <penalty_attr stat="SPEED" func="PERCENT" value="70"/></instance_bonusattr>
        var buff1 = data.GetInstanceBonusattr(1);
        Assert.NotNull(buff1);
        Assert.Equal(1, buff1!.GetBuffId());
        var attrs = buff1.GetPenaltyAttr();
        Assert.True(attrs.Count >= 5);
        Assert.Equal(Model.Stats.Container.StatEnum.PHYSICAL_ACCURACY, attrs[0].GetStat());
        Assert.Equal(SkillEngine.Change.Func.ADD, attrs[0].GetFunc());
        Assert.Equal(1100, attrs[0].GetValue());
        var speed = attrs.First(a => a.GetStat() == Model.Stats.Container.StatEnum.SPEED);
        Assert.Equal(SkillEngine.Change.Func.PERCENT, speed.GetFunc());
        Assert.Equal(70, speed.GetValue());

        Assert.Null(data.GetInstanceBonusattr(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesHouseNpcsDataFromRealXml()
    {
        var path = ResolveStaticDataFile("housing", "house_npcs.xml");

        var data = JaxbHolderLoader.LoadFromFile<HouseNpcsData>(path);

        Assert.True(data.Size() > 0);

        // <house address="20500"><spawn type="SIGN" .../><spawn type="MANAGER" .../>
        //   <spawn type="TELEPORT" .../></house>
        var spawns = data.GetSpawnsByAddress(20500);
        Assert.NotNull(spawns);
        Assert.Equal(3, spawns!.Count);
        var sign = spawns.First(s => s.GetType_() == Model.Templates.Spawns.SpawnType.SIGN);
        Assert.Equal(281.897f, sign.GetX(), 3);
        Assert.Equal(221.48187f, sign.GetZ(), 3);
        Assert.Equal((byte)101, sign.GetH());

        Assert.Null(data.GetSpawnsByAddress(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesCosmeticItemsDataFromRealXml()
    {
        var path = ResolveStaticDataFile("cosmetic_items", "cosmetic_items.xml");

        var data = JaxbHolderLoader.LoadFromFile<CosmeticItemsData>(path);

        Assert.True(data.Size() > 0);

        // <cosmetic_item type="hair_type" cosmetic_name="test_hair_type_li_m_01a" id="1"
        //   race="ELYOS" gender_permitted="MALE"/>
        var t = data.GetCosmeticItemsTemplate("test_hair_type_li_m_01a");
        Assert.NotNull(t);
        Assert.Equal("hair_type", t!.GetType_());
        Assert.Equal(1, t.GetId());
        Assert.Equal(Model.Race.ELYOS, t.GetRace());
        Assert.Equal("MALE", t.GetGenderPermitted());

        Assert.Null(data.GetCosmeticItemsTemplate("__nope__"));
    }

    [Fact]
    public void LoadFromFile_PopulatesAssembledNpcsDataFromRealXml()
    {
        var path = ResolveStaticDataFile("assembled_npcs", "assembled_npcs.xml");

        var data = JaxbHolderLoader.LoadFromFile<AssembledNpcsData>(path);

        Assert.True(data.Size() > 0);

        // <assembled_npc nr="1" routeId="3" liveTime="600000" mapId="210050000">
        //   <assembled_part npcId="258247" staticId="909"/> ...
        var npc = data.GetAssembledNpcTemplate(1);
        Assert.NotNull(npc);
        Assert.Equal(1, npc!.GetNr());
        Assert.Equal(3, npc.GetRouteId());
        Assert.Equal(600000, npc.GetLiveTime());
        Assert.Equal(210050000, npc.GetMapId());
        var parts = npc.GetAssembledNpcPartTemplates();
        Assert.True(parts.Count > 0);
        Assert.Equal(258247, parts[0].GetNpcId());
        Assert.Equal(909, parts[0].GetStaticId());

        Assert.Null(data.GetAssembledNpcTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesSignetDataTemplatesFromRealXml()
    {
        var path = ResolveStaticDataFile("skills", "signet_data_templates.xml");

        var data = JaxbHolderLoader.LoadFromFile<SignetDataTemplates>(path);

        Assert.True(data.Size() > 0);

        // <signet_data_template signet_skill="SIGNET1">
        //   <signet_data lvl="2" add_effect_prob="20" dmg_multi="0.5"/> ...
        var sd = data.GetSignetData(SkillEngine.Model.SignetEnum.SIGNET1, 2);
        Assert.NotNull(sd);
        Assert.Equal(2, sd!.GetLevel());
        Assert.Equal(20, sd.GetAddEffectProb());
        Assert.Equal(0.5f, sd.GetDamageMultiplier(), 3);

        Assert.Null(data.GetSignetData(SkillEngine.Model.SignetEnum.SIGNET1, 999));
    }

    [Fact]
    public void LoadFromFile_PopulatesItemPurificationDataFromRealXml()
    {
        var path = ResolveStaticDataFile("items", "item_purifications.xml");

        var data = JaxbHolderLoader.LoadFromFile<ItemPurificationData>(path);

        Assert.True(data.Size() > 0);

        // <item_purification base_item_id="100201319">
        //   <purification_result result_item_id="100201416" min_enchant_count="10"
        //     necessary_abyss_points="1374005">
        //     <req_material item_id="186000242" item_count="143" /> ...
        var tpl = data.GetItemPurificationTemplate(100201319);
        Assert.NotNull(tpl);
        Assert.Equal(100201319, tpl!.GetBaseItemId());
        var results = data.GetResultItemMap(100201319);
        Assert.NotNull(results);
        Assert.True(results!.ContainsKey(100201416));
        var r = results[100201416];
        Assert.Equal(10, r.GetMinEnchantCount());
        Assert.Equal(1374005, r.GetNecessaryAbyssPoints());
        var mats = r.GetRequiredMaterials();
        Assert.True(mats.Count >= 2);
        Assert.Equal(186000242, mats[0].GetItemId());
        Assert.Equal(143, mats[0].GetItemCount());

        Assert.Null(data.GetItemPurificationTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesPanelSkillsDataFromRealXml()
    {
        var path = ResolveStaticDataFile("polymorph_panels", "polymorph_panels.xml");

        var data = JaxbHolderLoader.LoadFromFile<PanelSkillsData>(path);

        Assert.True(data.Size() > 0);

        // <panel panel_id="4" panel_skills="4992001 4992257 4991745 4998145"/>
        // space-separated-list attribute via SkillsRaw string-proxy.
        var panel = data.GetSkillPanel(4);
        Assert.NotNull(panel);
        Assert.Equal(4, panel!.GetPanelId());
        // skill 4992001 -> skillId = 4992001>>8 = 19500, level = 4992001 & 0xFF = 1
        Assert.True(panel.IsSkillPresent(4992001 >> 8));
        Assert.True(panel.CanUseSkill(4992001 >> 8, 4992001 & 0xFF));

        Assert.Null(data.GetSkillPanel(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesRideDataFromRealXml_PresentNullableAttributes()
    {
        var path = ResolveStaticDataFile("ride", "ride.xml");

        var data = JaxbHolderLoader.LoadFromFile<RideData>(path);

        Assert.True(data.Size() > 0);

        // <ride_info id="2000000" type="0" move_speed="12.0" fly_speed="16.0"
        //   sprint_speed="15.0" start_fp="10" cost_fp="10"><bounds .../></ride_info>
        // nullable cost_fp / type present -> parsed via string-proxy.
        var ride = data.GetRideInfo(2000000);
        Assert.NotNull(ride);
        Assert.Equal(2000000, ride!.GetNpcId());
        Assert.Equal(10, ride.GetCostFp());
        Assert.Equal(0, ride.GetType_());
        Assert.Equal(10, ride.GetStartFp());
        Assert.Equal(12.0f, ride.GetMoveSpeed(), 3);
        Assert.Equal(16.0f, ride.GetFlySpeed(), 3);
        Assert.Equal(15.0f, ride.GetSprintSpeed(), 3);
        Assert.NotNull(ride.GetBounds());
        Assert.Equal(0.724f, ride.GetBounds().GetFront(), 3);
        Assert.Equal(0.5f, ride.GetBounds().GetAltitude(), 3);

        Assert.Null(data.GetRideInfo(-99999));
    }

    [Fact]
    public void RideInfo_StringProxy_AbsentNullableAttributes_YieldNull()
    {
        // Java parity: a nullable Integer @XmlAttribute that is absent unmarshals to null.
        // The real ride.xml has cost_fp/type on every row, so prove the absent-attribute branch of the
        // string-proxy directly against the faithful holder type with an inline <rides> document.
        const string xml =
            "<rides><ride_info id=\"999\" start_fp=\"5\" move_speed=\"1.0\" fly_speed=\"2.0\" sprint_speed=\"3.0\">" +
            "<bounds front=\"0.1\" side=\"0.2\" upper=\"0.3\" altitude=\"0.4\"/></ride_info></rides>";

        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(RideData));
        using var reader = new StringReader(xml);
        var data = (RideData)serializer.Deserialize(reader)!;
        InvokeAfterUnmarshal(data);

        var ride = data.GetRideInfo(999);
        Assert.NotNull(ride);
        // cost_fp and type attributes omitted -> string-proxy parses to null (1:1 with JAXB).
        Assert.Null(ride!.GetCostFp());
        Assert.Null(ride.GetType_());
        Assert.Equal(5, ride.GetStartFp());
    }

    private static void InvokeAfterUnmarshal(object holder)
    {
        var method = holder.GetType().GetMethod("AfterUnmarshal", new[] { typeof(object) });
        method?.Invoke(holder, new object?[] { null });
    }

    private static string ResolveStaticDataFile(params string[] relativeUnderStaticData)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "game-server", "data", "static_data" }
                    .Concat(relativeUnderStaticData).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate game-server/data/static_data/{string.Join('/', relativeUnderStaticData)} from {AppContext.BaseDirectory}");
    }
}
