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

    [Fact]
    public void LoadFromFile_PopulatesWorldRaidDataFromRealXml()
    {
        var path = ResolveStaticDataFile("world_raid", "world_raids.xml");

        var data = JaxbHolderLoader.LoadFromFile<WorldRaidData>(path);

        Assert.True(data.Size() > 0);

        // <world_raid_location location_id="1" map_id="210030000" x="1587.52" y="2078.81" z="155.85437" h="29">
        //   <world_raid_npcs><world_raid_npc npc_id="234558" death_msg_id="1402389" /></world_raid_npcs>
        //   <location_markers><spot x="1615.2394" .../>...</location_markers></world_raid_location>
        var loc = data.GetLocationsById(1);
        Assert.NotNull(loc);
        Assert.Equal(1, loc!.GetLocationId());
        Assert.Equal(210030000, loc.GetMapId());
        Assert.Equal(1587.52f, loc.GetX(), 3);
        Assert.Equal((byte)29, loc.GetH());

        var npcs = loc.GetNpcPool();
        Assert.NotNull(npcs);
        Assert.Single(npcs!);
        Assert.Equal(234558, npcs![0].GetNpcId());
        // death_msg_id present -> parsed via string-proxy.
        Assert.Equal(1402389, npcs[0].GetDeathMsgId());

        var markers = loc.GetLocationMarkers();
        Assert.NotNull(markers);
        Assert.Equal(2, markers!.Count);
        Assert.Equal(1615.2394f, markers[0].GetX(), 3);

        Assert.Null(data.GetLocationsById(-99999));
    }

    [Fact]
    public void WorldRaidNpc_StringProxy_AbsentDeathMsgId_KeepsZeroInitializer()
    {
        // Java parity: WorldRaidNpc.deathMsgId field initializer = 0; an absent attribute leaves it at 0.
        const string xml =
            "<world_raid_locations><world_raid_location location_id=\"7\" map_id=\"1\" x=\"0\" y=\"0\" z=\"0\" h=\"0\">" +
            "<world_raid_npcs><world_raid_npc npc_id=\"42\" /></world_raid_npcs>" +
            "<location_markers/></world_raid_location></world_raid_locations>";

        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(WorldRaidData));
        using var reader = new StringReader(xml);
        var data = (WorldRaidData)serializer.Deserialize(reader)!;
        InvokeAfterUnmarshal(data);

        var npc = data.GetLocationsById(7)!.GetNpcPool()![0];
        Assert.Equal(42, npc.GetNpcId());
        // death_msg_id omitted -> keeps the 0 field initializer (1:1 with JAXB).
        Assert.Equal(0, npc.GetDeathMsgId());
    }

    [Fact]
    public void LoadFromFile_PopulatesGoodsListDataFromRealXml()
    {
        var path = ResolveStaticDataFile("goodslists", "goodslists.xml");

        var data = JaxbHolderLoader.LoadFromFile<GoodsListData>(path);

        Assert.True(data.Size() > 0);

        // <list id="1"><item id="169500001"/>...</list>
        var list1 = data.GetGoodsListById(1);
        Assert.NotNull(list1);
        Assert.Equal(1, list1!.GetId());
        var ids = list1.GetItemIdList();
        Assert.NotNull(ids);
        Assert.Contains(169500001, ids!);

        // <list id="5001" ...><item buy_limit="0" sell_limit="100" id="152012001"/>...</list>
        // sell_limit/buy_limit present -> parsed via string-proxy; surfaces as LimitedItem.
        var list5001 = data.GetGoodsListById(5001);
        Assert.NotNull(list5001);
        var limited = list5001!.GetLimitedItems();
        Assert.True(limited.Count > 0);
        var li = limited.First(l => l.GetItemId() == 152012001);
        Assert.Equal(100, li.GetSellLimit());
        Assert.Equal(0, li.GetBuyLimit());

        // in_list and purchase_list buckets populated too.
        Assert.NotNull(data.GetGoodsInListById(1));
        Assert.NotNull(data.GetGoodsPurchaseListById(1));

        Assert.Null(data.GetGoodsListById(-99999));
    }

    [Fact]
    public void GoodsListItem_StringProxy_AbsentLimits_YieldNull()
    {
        // Java parity: GoodsList.Item sell_limit/buy_limit are nullable Integer; absent -> null
        // (and such an item is NOT surfaced as a LimitedItem).
        const string xml =
            "<goodslists><list id=\"1\"><item id=\"100\"/></list>" +
            "<in_list id=\"1\"/><purchase_list id=\"1\"/></goodslists>";

        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(GoodsListData));
        using var reader = new StringReader(xml);
        var data = (GoodsListData)serializer.Deserialize(reader)!;
        InvokeAfterUnmarshal(data);

        var list = data.GetGoodsListById(1);
        Assert.NotNull(list);
        Assert.Contains(100, list!.GetItemIdList());
        // No sell_limit/buy_limit -> no LimitedItem produced.
        Assert.Empty(list.GetLimitedItems());
    }

    [Fact]
    public void LoadFromFile_PopulatesNpcFactionsDataFromRealXml()
    {
        var path = ResolveStaticDataFile("npc_factions", "npc_factions.xml");

        var data = JaxbHolderLoader.LoadFromFile<NpcFactionsData>(path);

        Assert.True(data.Size() > 0);

        // <npc_faction id="2" name="Alabaster Order" npc_ids="799803 805145" name_id="1129000"
        //   category="DAILY" min_level="30" race="ELYOS"/>
        var faction = data.GetNpcFactionById(2);
        Assert.NotNull(faction);
        Assert.Equal(2, faction!.GetId());
        Assert.Equal("Alabaster Order", faction.GetName());
        Assert.Equal(1129000, faction.GetL10nId());
        Assert.Equal(Model.Templates.Factions.FactionCategory.DAILY, faction.GetCategory());
        // min_level present -> parsed via string-proxy.
        Assert.Equal(30, faction.GetMinLevel());
        Assert.Equal(Model.Race.ELYOS, faction.GetRace());
        // npc_ids space-separated -> proxy split.
        Assert.Contains(799803, faction.GetNpcIds());
        Assert.Contains(805145, faction.GetNpcIds());

        // Reverse index by npc id.
        Assert.Same(faction, data.GetNpcFactionByNpcId(799803));

        // <npc_faction id="8" ... max_level="39" .../>
        Assert.Equal(39, data.GetNpcFactionById(8)!.GetMaxLevel());

        Assert.Null(data.GetNpcFactionById(-99999));
    }

    [Fact]
    public void NpcFaction_StringProxy_AbsentMinLevel_YieldsNull()
    {
        // Java parity: NpcFactionTemplate.min_level is a nullable Integer; absent -> null (getMinLevel() would NPE).
        const string xml =
            "<npc_factions><npc_faction id=\"999\" name=\"x\" name_id=\"1\" category=\"DAILY\" race=\"ELYOS\"/></npc_factions>";

        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(NpcFactionsData));
        using var reader = new StringReader(xml);
        var data = (NpcFactionsData)serializer.Deserialize(reader)!;
        InvokeAfterUnmarshal(data);

        var faction = data.GetNpcFactionById(999);
        Assert.NotNull(faction);
        // min_level omitted -> string-proxy parses to null (1:1 with JAXB). max_level keeps its 99 default.
        Assert.Throws<System.InvalidOperationException>(() => faction!.GetMinLevel());
        Assert.Equal(99, faction!.GetMaxLevel());
    }

    [Fact]
    public void LoadFromFile_PopulatesTeleporterDataFromRealXml()
    {
        var path = ResolveStaticDataFile("npc_teleporter.xml");

        var data = JaxbHolderLoader.LoadFromFile<TeleporterData>(path);

        Assert.True(data.Size() > 0);

        // <teleporter_template npc_ids="203726" teleportId="1">
        //   <locations><telelocation loc_id="3" price="100" pricePvp="100" required_quest="1006" type="REGULAR"/>...
        var tpl = data.GetTeleporterTemplateByTeleportId(1);
        Assert.NotNull(tpl);
        Assert.Equal(1, tpl!.GetTeleportId());
        // npc_ids space-separated-list -> proxy.
        Assert.Contains(203726, tpl.GetNpcIds());
        Assert.True(tpl.ContainNpc(203726));

        var locData = tpl.GetTeleLocIdData();
        Assert.NotNull(locData);
        var loc3 = locData!.GetTeleportLocation(3);
        Assert.NotNull(loc3);
        Assert.Equal(3, loc3!.GetLocId());
        Assert.Equal(100, loc3.GetPrice());
        Assert.Equal(100, loc3.GetPricePvp());
        Assert.Equal(1006, loc3.GetRequiredQuest());
        Assert.Equal(Model.Templates.Teleport.TeleportType.REGULAR, loc3.GetType_());

        // Reverse lookup by npc id.
        Assert.Same(tpl, data.GetTeleporterTemplateByNpcId(203726));

        Assert.Null(data.GetTeleporterTemplateByTeleportId(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesHousePartsDataFromRealXml()
    {
        var path = ResolveStaticDataFile("housing", "house_parts.xml");

        var data = JaxbHolderLoader.LoadFromFile<HousePartsData>(path);

        Assert.True(data.Size() > 0);

        // <house_part id="3500000" name="Hexagonal Board Roof" quality="UNIQUE" type="ROOF" building_tags="CP_A"/>
        var part = data.GetPartById(3500000);
        Assert.NotNull(part);
        Assert.Equal(3500000, part!.GetId());
        Assert.Equal("Hexagonal Board Roof", part.GetName());
        Assert.Equal(Model.Templates.Items.ItemQuality.UNIQUE, part.GetQuality());
        Assert.Equal(Model.Templates.Housing.PartType.ROOF, part.GetType_());
        // building_tags space-separated Set<String> -> proxy.
        var tags = part.GetTags();
        Assert.NotNull(tags);
        Assert.Contains("CP_A", tags!);

        Assert.Null(data.GetPartById(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesItemRestrictionCleanupDataFromRealXml()
    {
        var path = ResolveStaticDataFile("items", "item_restriction_cleanups.xml");

        var data = JaxbHolderLoader.LoadFromFile<ItemRestrictionCleanupData>(path);

        Assert.True(data.Size() > 0);

        // <cleanup id="188053996" awh="0" lwh="0" /> -- Emperor Trillirunerk's Feather Box (only uncommented row).
        var tpl = data.GetList().First(t => t.GetId() == 188053996);
        Assert.Equal((sbyte)0, tpl.ResultAccountWH());
        Assert.Equal((sbyte)0, tpl.ResultLegionWH());
        // trade / sell / wh omitted -> keep the -1 field default.
        Assert.Equal((sbyte)(-1), tpl.ResultTrade());
        Assert.Equal((sbyte)(-1), tpl.ResultSell());
        Assert.Equal((sbyte)(-1), tpl.ResultWH());

        // awh=0/lwh=0 -> storability disabled.
        Assert.True(data.HasAccountOrLegionWhStorabilityDisabled(188053996));
        Assert.False(data.HasAccountOrLegionWhStorabilityDisabled(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesAssemblyItemsDataFromRealXml()
    {
        var path = ResolveStaticDataFile("items", "assembly_items.xml");

        var data = JaxbHolderLoader.LoadFromFile<AssemblyItemsData>(path);

        Assert.True(data.Size() > 0);

        // <item id="100201411" parts="188100135 188100136 188100137 188100138 188100139" />
        var item = data.GetAssemblyItem(100201411);
        Assert.NotNull(item);
        Assert.Equal(100201411, item!.GetId());
        // parts space-separated-list via PartsRaw string-proxy.
        var parts = item.GetParts();
        Assert.Equal(5, parts.Count);
        Assert.Equal(188100135, parts[0]);
        Assert.Contains(188100139, parts);

        Assert.Null(data.GetAssemblyItem(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesAtreianPassportDataFromRealXml()
    {
        var path = ResolveStaticDataFile("events", "login_events.xml");

        var data = JaxbHolderLoader.LoadFromFile<AtreianPassportData>(path);

        Assert.True(data.Size() > 0);

        // <login_event id="1" active="1" period_start="2014-03-01T00:00:00" period_end="2014-05-01T00:00:00"
        //   attend_type="DAILY" attend_num="1" reward_item="188052315" reward_item_num="1" reward_item_expire_time="1440"/>
        var ev1 = data.GetAtreianPassportId(1);
        Assert.NotNull(ev1);
        Assert.Equal(1, ev1!.GetId());
        // active="1" -> bool true.
        Assert.True(ev1.IsActive());
        Assert.Equal(Model.AttendType.DAILY, ev1.GetAttendType());
        Assert.Equal(1, ev1.GetAttendNum());
        Assert.Equal(188052315, ev1.GetRewardItemId());
        Assert.Equal(1, ev1.GetRewardItemCount());
        Assert.Equal(1440, ev1.GetRewardExpireMinutes());
        // period_start via LocalDateTimeAdapter string-proxy.
        Assert.Equal(new System.DateTime(2014, 3, 1, 0, 0, 0), ev1.GetPeriodStart());

        // <login_event id="2" active="0" ... attend_type="CUMULATIVE" .../>
        var ev2 = data.GetAtreianPassportId(2);
        Assert.NotNull(ev2);
        Assert.False(ev2!.IsActive());
        Assert.Equal(Model.AttendType.CUMULATIVE, ev2.GetAttendType());

        Assert.Null(data.GetAtreianPassportId(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesAbsoluteStatsDataFromRealXml_WithModifiersIntact()
    {
        var path = ResolveStaticDataFile("stats", "absolute_stats.xml");

        var data = JaxbHolderLoader.LoadFromFile<AbsoluteStatsData>(path);

        // AfterUnmarshal indexed every stats_set by id and nulled the raw list.
        Assert.True(data.Size() > 0);

        // <stats_set id="1"> carries a <modifiers> block of polymorphic <abs .../> stat functions.
        // CRITICAL: prove the StatFunction modifiers are NOT silently dropped.
        var modifiers = data.GetTemplate(1);
        Assert.NotNull(modifiers);
        var list = modifiers!.GetModifiers();
        Assert.NotNull(list);
        Assert.True(list!.Count > 1, "modifiers list empty -> StatFunctions were dropped on unmarshal");

        // The <abs> elements must deserialize to the StatAbsFunction subtype (polymorphic [XmlElement(typeof(...))]).
        Assert.All(list, m => Assert.IsType<Model.Stats.Calc.Functions.StatAbsFunction>(m));

        // First row: <abs name="POWER" value="1"/> -> getName()=POWER, getValue()=1, isBonus()=false.
        var first = list[0];
        Assert.Equal(Model.Stats.Container.StatEnum.POWER, first.GetName());
        Assert.Equal(1, first.GetValue());
        Assert.False(first.IsBonus());

        // A known bonus row: <abs name="MAXHP" value="103" bonus="true"/> -> isBonus()=true with value 103.
        var maxHpBonus = list.First(m => m.GetName() == Model.Stats.Container.StatEnum.MAXHP && m.IsBonus());
        Assert.Equal(103, maxHpBonus.GetValue());

        // A known non-bonus row: <abs name="MAXHP" value="101"/>.
        var maxHpBase = list.First(m => m.GetName() == Model.Stats.Container.StatEnum.MAXHP && !m.IsBonus());
        Assert.Equal(101, maxHpBase.GetValue());

        Assert.Null(data.GetTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesItemSetDataFromRealXml_WithBonusModifiersIntact()
    {
        var path = ResolveStaticDataFile("item_sets", "item_sets.xml");

        var data = JaxbHolderLoader.LoadFromFile<ItemSetData>(path);

        // AfterUnmarshal indexed every itemset by id (and by part item id) and nulled the raw list.
        Assert.True(data.Size() > 0);

        // <itemset id="1" name="Cloth Armor Set (Test)"> with 5 itemparts, two partbonuses and a fullbonus.
        var set = data.GetItemSetTemplate(1);
        Assert.NotNull(set);
        Assert.Equal(1, set!.GetId());
        Assert.Equal("Cloth Armor Set (Test)", set.GetName());

        // Reverse index by item id: <itempart itemid="110100919"/> -> resolves back to set 1.
        Assert.Same(set, data.GetItemSetTemplateByItemId(110100919));

        // CRITICAL: prove the partbonus modifier survives. <partbonus count="3"> carries
        // <add name="BOOST_MAGICAL_SKILL" value="100" bonus="true"/>.
        var parts = set.GetPartbonus();
        Assert.NotNull(parts);
        var part3 = parts!.First(p => p.GetCount() == 3);
        var part3Mods = part3.GetModifiers();
        Assert.NotNull(part3Mods);
        Assert.Single(part3Mods!);
        var boost = part3Mods![0];
        Assert.IsType<Model.Stats.Calc.Functions.StatAddFunction>(boost);
        Assert.Equal(Model.Stats.Container.StatEnum.BOOST_MAGICAL_SKILL, boost.GetName());
        Assert.Equal(100, boost.GetValue());
        Assert.True(boost.IsBonus());

        // CRITICAL: prove the fullbonus modifiers survive AND AfterUnmarshal set the item count.
        // <fullbonus> carries a <rate name="SPEED" value="10" bonus="true"/> among others.
        var full = set.GetFullbonus();
        Assert.NotNull(full);
        Assert.Equal(5, full!.GetCount()); // ItemSetTemplate.AfterUnmarshal set this to itempart.Count (5).
        var fullMods = full.GetModifiers();
        Assert.NotNull(fullMods);
        var speed = fullMods!.First(m => m.GetName() == Model.Stats.Container.StatEnum.SPEED);
        Assert.IsType<Model.Stats.Calc.Functions.StatRateFunction>(speed);
        Assert.Equal(10, speed.GetValue());
        Assert.True(speed.IsBonus());

        Assert.Null(data.GetItemSetTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesTitleDataFromRealXml_WithModifiersIntact()
    {
        var path = ResolveStaticDataFile("player_titles.xml");

        var data = JaxbHolderLoader.LoadFromFile<TitleData>(path);

        // AfterUnmarshal indexed every title by id and nulled the raw list.
        Assert.True(data.Size() > 0);

        // <title id="1" nameId="1100900" desc="Poeta's Protector" race="ELYOS"> with
        // <add name="MAXHP" value="20" bonus="true"/> + <add name="PHYSICAL_DEFENSE" value="5" bonus="true"/>.
        var title = data.GetTitleTemplate(1);
        Assert.NotNull(title);
        Assert.Equal(1, title!.GetTitleId());
        Assert.Equal(1100900, title.GetL10nId());
        Assert.Equal("Poeta's Protector", title.GetDesc());
        Assert.Equal(Model.Race.ELYOS, title.GetRace());

        // CRITICAL: prove the modifiers are not dropped.
        var mods = title.GetModifiers();
        Assert.NotNull(mods);
        Assert.True(mods!.Count >= 2, "title modifiers dropped on unmarshal");
        var maxHp = mods.First(m => m.GetName() == Model.Stats.Container.StatEnum.MAXHP);
        Assert.IsType<Model.Stats.Calc.Functions.StatAddFunction>(maxHp);
        Assert.Equal(20, maxHp.GetValue());
        Assert.True(maxHp.IsBonus());

        Assert.Null(data.GetTitleTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesConquerorAndProtectorDataFromRealXml_WithModifiersIntact()
    {
        var path = ResolveStaticDataFile("conqueror_protector_ranks", "conqueror_protector_ranks.xml");

        var data = JaxbHolderLoader.LoadFromFile<ConquerorAndProtectorData>(path);

        Assert.True(data.Size() > 0);

        // <rank type="CONQUEROR" rank_num="1"><add name="PVP_ATTACK_RATIO" value="10" bonus="true"/></rank>
        var conq1 = data.GetRank(Model.Templates.Cp.CPType.CONQUEROR, 1);
        Assert.NotNull(conq1);
        Assert.Equal(Model.Templates.Cp.CPType.CONQUEROR, conq1!.GetType_());
        Assert.Equal(1, conq1.GetRankNum());

        // CRITICAL: prove the stat modifier is not dropped.
        var conqMods = conq1.GetStatModifiers();
        Assert.NotNull(conqMods);
        Assert.Single(conqMods);
        var atk = conqMods[0];
        Assert.IsType<Model.Stats.Calc.Functions.StatAddFunction>(atk);
        Assert.Equal(Model.Stats.Container.StatEnum.PVP_ATTACK_RATIO, atk.GetName());
        Assert.Equal(10, atk.GetValue());
        Assert.True(atk.IsBonus());

        // <rank type="PROTECTOR" rank_num="1" visible_intruder_min_rank="3"> with PVP_DEFEND_RATIO 20.
        var prot1 = data.GetRank(Model.Templates.Cp.CPType.PROTECTOR, 1);
        Assert.NotNull(prot1);
        Assert.Equal(3, prot1!.GetVisibleIntruderMinRank());
        var def = prot1.GetStatModifiers()[0];
        Assert.Equal(Model.Stats.Container.StatEnum.PVP_DEFEND_RATIO, def.GetName());
        Assert.Equal(20, def.GetValue());

        Assert.Null(data.GetRank(Model.Templates.Cp.CPType.CONQUEROR, 9999));
    }

    [Fact]
    public void LoadFromFile_PopulatesVortexDataFromRealXml()
    {
        var path = ResolveStaticDataFile("vortex", "dimensional_vortex.xml");

        var data = JaxbHolderLoader.LoadFromFile<VortexData>(path);

        // AfterUnmarshal built the id->VortexLocation map.
        Assert.True(data.Size() > 0);

        // <vortex_location id="0" defends_race="ELYOS" offence_race="ASMODIANS">
        //   <home_point map="120080000" x="559.4" y="207.8" z="93.5" h="0"/>
        //   <start_point map="210060000" .../>
        var locations = data.GetVortexLocations();
        Assert.NotNull(locations);
        Assert.True(locations.ContainsKey(0));
        var loc0 = locations[0];
        Assert.Equal(0, loc0.GetId());
        Assert.Equal(Model.Race.ELYOS, loc0.GetDefendersRace());
        Assert.Equal(Model.Race.ASMODIANS, loc0.GetInvadersRace());
        // home_point map=120080000; start_point map=210060000 -> drives the home/invasion world ids.
        Assert.Equal(120080000, loc0.GetHomeWorldId());
        Assert.Equal(210060000, loc0.GetInvasionWorldId());

        // Lookup by invasion world id (start_point map) resolves the same location.
        Assert.Same(loc0, data.GetVortexLocation(210060000));

        Assert.Null(data.GetVortexLocation(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesNpcDataFromRealXml_WithKnownNpcFieldsIntact()
    {
        var path = ResolveStaticDataFile("npcs", "npc_templates.xml");

        // ~35MB standalone <npc_templates> root; AfterUnmarshal indexes every template by npc_id and nulls the list.
        var data = JaxbHolderLoader.LoadFromFile<NpcData>(path);

        Assert.True(data.Size() > 0);

        // Known named NPC (Sage Fasimedes):
        // <npc_template npc_id="203700" level="60" name="fasimedes" name_id="351200" title_id="350335"
        //   height="2" group_drop="LIGHT" rank="DISCIPLINED" rating="NORMAL" race="ELYOS" tribe="GUARD"
        //   type="ABYSS_GUARD" ai="simple_abyssguard" srange="7" sangle="300" arange="2" attack_speed="2000"
        //   hpgauge="3" state="6">
        //   <stats maxHp="23691"><speeds walk="1.5" run="6" .../></stats>
        //   <equipment><item>113600393</item>...6 items...</equipment>
        //   <bound_radius front="0.25" side="0.35" upper="2"/>
        //   <talk_info distance="5" is_dialog="true" can_talk_invisible="false"/>
        var npc = data.GetNpcTemplate(203700);
        Assert.NotNull(npc);

        // Scalar fields intact (NOT silently dropped).
        Assert.Equal(203700, npc!.GetTemplateId());
        Assert.Equal("fasimedes", npc.GetName());
        Assert.Equal(351200, npc.GetL10nId());
        Assert.Equal(350335, npc.GetTitleId());
        Assert.Equal((byte)60, npc.GetLevel());
        Assert.Equal(2f, npc.GetHeight(), 3);
        Assert.Equal(Model.Templates.Npc.GroupDropType.LIGHT, npc.GetGroupDrop());
        Assert.Equal(Model.Race.ELYOS, npc.GetRace());
        Assert.Equal(Model.TribeClass.GUARD, npc.GetTribe());
        Assert.Equal(Model.Templates.Npc.NpcTemplateType.ABYSS_GUARD, npc.GetNpcTemplateType());
        Assert.Equal("simple_abyssguard", npc.GetAiName());
        Assert.Equal(7, npc.GetAggroRange());
        Assert.Equal(300, npc.GetAggroAngle());
        Assert.Equal(2, npc.GetAttackRange());
        Assert.Equal(2000, npc.GetAttackSpeed());
        Assert.Equal(3, npc.GetHpGauge());
        Assert.Equal(6, npc.GetState());

        // Nested <stats> + <speeds> intact.
        var stats = npc.GetStatsTemplate();
        Assert.NotNull(stats);
        Assert.Equal(23691, stats!.GetMaxHp());
        Assert.Equal(1.5f, stats.GetWalkSpeed(), 3);
        Assert.Equal(6f, stats.GetRunSpeed(), 3);

        // Nested <bound_radius> intact.
        var br = npc.GetBoundRadius();
        Assert.NotNull(br);
        Assert.Equal(0.25f, br.GetFront(), 3);
        Assert.Equal(2f, br.GetUpper(), 3);

        // Nested <talk_info> intact.
        Assert.True(npc.IsDialogNpc());
        Assert.Equal(5, npc.GetTalkDistance());

        // CRITICAL: the <equipment> item-id list must NOT be silently dropped (IDREF resolution is deferred,
        // but the raw item ids are captured by the NpcEquipmentList proxy).
        var gear = npc.GetEquipment();
        Assert.NotNull(gear);
        Assert.Equal(
            new[] { 113600393, 110600423, 100000043, 112600380, 111600398, 114600388 },
            npc.equipmentList!.ItemIds);

        Assert.Null(data.GetNpcTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesItemDataFromRealXml_WithKnownItemFieldsAndModifierIntact()
    {
        var path = ResolveStaticDataFile("items", "item_templates.xml");

        // ~65MB standalone <item_templates> root; AfterUnmarshal indexes every template by item id and nulls the list.
        // (Slow: tens of seconds to deserialize the full catalog.)
        var data = JaxbHolderLoader.LoadFromFile<ItemData>(path);

        Assert.True(data.Size() > 0);

        // Known weapon (Fire Sword), id=100000125:
        // <item_template id="100000125" name="Fire Sword" level="23" item_group="SWORD" item_type="NORMAL"
        //   quality="COMMON" attack_type="PHYSICAL" max_enchant="10" m_slots="1">
        //   <modifiers><add name="PHYSICAL_ATTACK" value="7" bonus="true"/></modifiers>
        //   <weapon_stats .../></item_template>
        var item = data.GetItemTemplate(100000125);
        Assert.NotNull(item);

        // Scalar fields intact (NOT silently dropped).
        Assert.Equal(100000125, item!.GetTemplateId());
        Assert.Equal("Fire Sword", item.GetName());
        Assert.Equal(23, item.GetLevel());
        Assert.Equal(Model.Templates.Items.ItemQuality.COMMON, item.GetItemQuality());
        Assert.Equal(Model.Templates.Items.ItemType.NORMAL, item.GetItemType());
        Assert.Equal(Model.Templates.Items.Enums.ItemGroup.SWORD, item.GetItemGroup());
        Assert.Equal(Model.Templates.Items.ItemAttackType.PHYSICAL, item.GetAttackType());
        Assert.Equal(10, item.GetMaxEnchantLevel());
        Assert.Equal(1, item.GetManastoneSlots());
        Assert.True(item.GetItemSlot() != 0); // SWORD maps to a real equipment slot

        // Nested <weapon_stats> intact.
        var ws = item.GetWeaponStats();
        Assert.NotNull(ws);
        Assert.Equal(77, ws!.MaxDamage);
        Assert.Equal(63, ws.MinDamage);
        Assert.Equal(1400, ws.AttackSpeed);

        // CRITICAL: the polymorphic <modifiers>/<add> subtree must NOT be silently dropped.
        var modifiers = item.GetModifiers();
        Assert.NotNull(modifiers);
        Assert.Single(modifiers);
        var add = modifiers![0];
        Assert.IsType<Model.Stats.Calc.Functions.StatAddFunction>(add);
        Assert.Equal(Model.Stats.Container.StatEnum.PHYSICAL_ATTACK, add.GetName());
        Assert.Equal(7, add.GetValue());
        Assert.True(add.IsBonus());

        Assert.Null(data.GetItemTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesSkillDataFromRealXml_WithEffectsConditionsPropertiesIntact()
    {
        var path = ResolveStaticDataFile("skills", "skill_templates.xml");

        // ~12MB standalone <skill_data> root; AfterUnmarshal indexes every template by skill id (and group/stack),
        // firing each Effects.AfterUnmarshal (effectTypes set) children-first, then nulls the source list.
        var skills = JaxbHolderLoader.LoadFromFile<Dataholders.SkillData>(path);

        Assert.True(skills.Size() > 0);

        // Known skill (Transformation: White Tiger), skill_id="1":
        // <skill_template skill_id="1" name="Transformation: White Tiger" nameId="2285933" cooldownId="792"
        //   group="RA_WHITETIGER" stack="RA_LIGHT_WHITETIGER" lvl="1" skilltype="MAGICAL" skillsubtype="BUFF"
        //   tslot="BUFF" dispel_category="BUFF" req_dispel_level="2" activation="ACTIVE" cooldown="100"
        //   duration="0" cancel_rate="20" hostile_type="INDIRECT">
        //   <properties first_target="ME" first_target_range="1" target_relation="FRIEND" target_type="ONLYONE" />
        //   <startconditions><dp value="2000" /></startconditions>
        //   <effects>
        //     <shapechange model="202641" type="PC" duration2="120000" effectid="175" e="1" basiclvl="100" noresist="true" hoptype="SKILLLV" hopb="248" />
        //     <statup ... preeffect="1"><change stat="PHYSICAL_ATTACK" func="PERCENT" value="10" /></statup>
        //     ...
        //   </effects>
        //   <actions><dpuse value="2000" /></actions>
        //   <motion name="phburst" />
        var skill = skills.GetSkillTemplate(1);
        Assert.NotNull(skill);

        // Scalar fields intact (NOT silently dropped).
        Assert.Equal(1, skill!.GetSkillId());
        Assert.Equal("Transformation: White Tiger", skill.GetName());
        Assert.Equal(2285933, skill.GetL10nId());
        Assert.Equal("RA_WHITETIGER", skill.GetGroup());
        Assert.Equal("RA_LIGHT_WHITETIGER", skill.GetStack());
        Assert.Equal(1, skill.GetLvl());
        Assert.Equal(SkillEngine.Model.SkillType.MAGICAL, skill.GetTypeValue());
        Assert.Equal(SkillEngine.Model.SkillSubType.BUFF, skill.GetSubType());
        Assert.Equal(SkillEngine.Model.ActivationAttribute.ACTIVE, skill.GetActivationAttribute());
        Assert.Equal(100, skill.GetCooldown());
        Assert.Equal(20, skill.GetCancelRate());

        // Group/stack indexes built by AfterUnmarshal.
        Assert.Contains(skill, skills.GetSkillTemplatesByGroup("RA_WHITETIGER"));
        Assert.Contains(skill, skills.GetSkillTemplatesByStack("RA_LIGHT_WHITETIGER"));

        // <properties> intact (enum attributes via name-binding).
        var props = skill.GetProperties();
        Assert.NotNull(props);
        Assert.Equal(SkillEngine.Properties.FirstTargetAttribute.ME, props!.GetFirstTarget());
        Assert.Equal(1, props.GetFirstTargetRange());
        Assert.Equal(SkillEngine.Properties.TargetRelationAttribute.FRIEND, props.GetTargetRelation());
        Assert.Equal(SkillEngine.Properties.TargetRangeAttribute.ONLYONE, props.GetTargetType());

        // <startconditions> polymorphic <dp> -> DpCondition (NOT silently dropped).
        var start = skill.GetStartconditions();
        Assert.NotNull(start);
        var dp = Assert.IsType<SkillEngine.Condition.DpCondition>(Assert.Single(start!.GetConditions()));
        Assert.Equal(2000, dp.Value);

        // CRITICAL: <effects> polymorphic subtree must NOT be silently dropped — correct subtypes + nested data.
        var effects = skill.GetEffects();
        Assert.NotNull(effects);
        var effList = effects!.GetEffects();
        Assert.True(effList.Count >= 4);

        // First effect <shapechange> -> ShapeChangeEffect (a TransformEffect); model/type + nullable hoptype proxy intact.
        var shape = Assert.IsType<SkillEngine.Effects.ShapeChangeEffect>(effList[0]);
        Assert.Equal(202641, shape.GetTransformId());
        Assert.Equal(SkillEngine.Model.TransformType.PC, shape.GetTransformType());
        Assert.Equal(175, shape.GetEffectId());
        Assert.Equal(1, shape.GetPosition());
        Assert.Equal(SkillEngine.Model.HopType.SKILLLV, shape.Hoptype); // nullable-enum attribute via string proxy
        Assert.Equal(248, shape.HopB);

        // Effects.AfterUnmarshal built the effectTypes set (shapechange -> SHAPECHANGE) — prove it ran.
        Assert.True(effects.HasAnyEffectType(SkillEngine.Effects.EffectType.SHAPECHANGE));

        // Second effect <statup> -> StatupEffect carrying a nested <change> (PHYSICAL_ATTACK PERCENT +10).
        var statup = Assert.IsType<SkillEngine.Effects.StatupEffect>(effList[1]);
        var changes = statup.GetChange();
        Assert.NotNull(changes);
        var change = Assert.Single(changes!);
        Assert.Equal(Model.Stats.Container.StatEnum.PHYSICAL_ATTACK, change.GetStat()); // nullable-enum attr proxy
        Assert.Equal(SkillEngine.Change.Func.PERCENT, change.GetFunc());
        Assert.Equal(10, change.GetValue());

        // <actions> polymorphic <dpuse> -> DpUseAction (NOT silently dropped).
        var actions = skill.GetActions();
        Assert.NotNull(actions);
        Assert.IsType<SkillEngine.Action.DpUseAction>(Assert.Single(actions!.GetActions()));

        // <motion> intact.
        Assert.Equal("phburst", skill.GetMotion()!.GetName());

        Assert.Null(skills.GetSkillTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesQuestsDataFromRealXml_WithKnownQuestFieldsAndNestedIntact()
    {
        var path = ResolveStaticDataFile("quest_data", "quest_data.xml");

        // ~82k-line self-contained <quests> root; AfterUnmarshal indexes every template by id (and by npc faction)
        // and nulls the source list. Feeds DataManager.QUEST_DATA (the 1025 ported quest handlers).
        var data = JaxbHolderLoader.LoadFromFile<Dataholders.QuestsData>(path);

        Assert.True(data.Size() > 0);

        // Known quest (The Kerubim Threat), id=1001:
        // <quest id="1001" name="The Kerubim Threat" nameId="1102001" quest_zone="Poeta" minlevel_permitted="2"
        //   max_repeat_count="1" cannot_share="true" cannot_giveup="true" race_permitted="ELYOS" category="MISSION">
        //   <collect_items><collect_item item_id="182200001" count="3"/></collect_items>
        //   <rewards exp="2100">
        //     <selectable_reward_item item_id="114100806" count="1"/> ...3 selectable items... </rewards>
        //   <quest_drop npc_id="210671" item_id="182200001" drop_each_member="1" collecting_step="7"/>
        var quest = data.GetQuestById(1001);
        Assert.NotNull(quest);

        // Scalar fields intact (NOT silently dropped).
        Assert.Equal(1001, quest!.GetId());
        Assert.Equal("The Kerubim Threat", quest.GetName());
        Assert.Equal(1102001, quest.GetL10nId());
        Assert.Equal(2, quest.GetMinlevelPermitted());
        Assert.Equal(1, quest.GetMaxRepeatCount());
        Assert.True(quest.IsCannotShare());
        Assert.True(quest.IsCannotGiveup());
        Assert.Equal(Model.Race.ELYOS, quest.GetRacePermitted());
        Assert.Equal(Model.Templates.Quest.QuestCategory.MISSION, quest.GetCategory());
        Assert.True(quest.IsMission());

        // CRITICAL: nested <collect_items>/<collect_item> must NOT be silently dropped.
        var collect = quest.GetCollectItems();
        Assert.NotNull(collect);
        var collectItems = collect!.GetCollectItem();
        Assert.Single(collectItems);
        Assert.Equal(182200001, collectItems[0].GetItemId());
        Assert.Equal(3, collectItems[0].GetCount());

        // CRITICAL: nested <rewards> + its <selectable_reward_item> children must NOT be dropped.
        var rewards = quest.GetRewards();
        Assert.NotNull(rewards);
        var reward = Assert.Single(rewards);
        Assert.Equal(2100, reward.GetExp());
        var selectables = reward.GetSelectableRewardItem();
        Assert.Equal(3, selectables.Count);
        Assert.Equal(114100806, selectables[0].GetItemId());
        Assert.Equal(1, selectables[0].GetCount());
        Assert.Contains(selectables, s => s.GetItemId() == 114500778);

        // CRITICAL: nested <quest_drop> must NOT be dropped.
        var drops = quest.GetQuestDrop();
        Assert.Single(drops);
        Assert.Equal(210671, drops[0].GetNpcId());
        Assert.Equal(182200001, drops[0].GetItemId());
        Assert.True(drops[0].IsDropEachMemberGroup());
        Assert.Equal(7, drops[0].GetCollectingStep());

        // A simple reward-only quest (id=85): rewards gold="100" exp="100", restricted="true".
        var q85 = data.GetQuestById(85);
        Assert.NotNull(q85);
        Assert.True(q85!.IsRestricted());
        Assert.Equal(100L, Assert.Single(q85.GetRewards()).GetKinah());

        Assert.Null(data.GetQuestById(-99999));
    }

    [Fact]
    public void LoadFromFiles_PopulatesAIDataFromRealXml_WithSummonsAndBombsIntact()
    {
        // Java parity: the static_data import graph merges every <ai_templates> source file (ai/bombs.xml +
        // ai/spawn_helpers.xml) into one AIData before afterUnmarshal. Replicate the StaticData merge path here:
        // deserialize each raw, merge pending <ai> rows, then run AfterUnmarshal once. Feeds DataManager.AI_DATA
        // (the 462 ported AI handlers + SummonerAI).
        var bombsPath = ResolveStaticDataFile("ai", "bombs.xml");
        var summonsPath = ResolveStaticDataFile("ai", "spawn_helpers.xml");

        var data = JaxbHolderLoader.DeserializeFile<AIData>(bombsPath);
        data.MergePending(JaxbHolderLoader.DeserializeFile<AIData>(summonsPath));
        JaxbHolderLoader.RunAfterUnmarshal(data);

        Assert.True(data.Size() > 0);

        // Known bombs row (bombs.xml): <ai npcId="281327"><bombs><bomb skillId="16559"/></bombs></ai>
        var bombAi = data.GetAiTemplate(281327);
        Assert.NotNull(bombAi);
        Assert.Equal(281327, bombAi!.GetNpcId());
        var bombs = bombAi.GetBombs();
        Assert.NotNull(bombs);
        Assert.Equal(16559, bombs!.GetBombTemplate().GetSkillId());

        // <ai npcId="281545"><bombs><bomb skillId="18905" cd="4000"/></bombs></ai> -> cooldown intact.
        var cdAi = data.GetAiTemplate(281545);
        Assert.NotNull(cdAi);
        Assert.Equal(4000, cdAi!.GetBombs().GetBombTemplate().GetCd());

        // Known summons row (spawn_helpers.xml): Ragenon Souleater
        // <ai npcId="212145"><summons><percentage percent="50">
        //   <summonGroup npcId="280747" minCount="2" distance="3"/></percentage></summons></ai>
        var summonAi = data.GetAiTemplate(212145);
        Assert.NotNull(summonAi);
        Assert.Equal(212145, summonAi!.GetNpcId());
        var summons = summonAi.GetSummons();
        Assert.NotNull(summons);
        var percentages = summons!.GetPercentage();
        Assert.Single(percentages);
        Assert.Equal(50, percentages[0].GetPercent());
        var groups = percentages[0].GetSummons();
        Assert.Single(groups);
        Assert.Equal(280747, groups[0].GetNpcId());
        Assert.Equal(2, groups[0].GetMinCount());
        // SummonGroup.AfterUnmarshal ran children-first: maxCount defaulted from minCount (2).
        Assert.Equal(2, groups[0].GetMaxCount());
        Assert.Equal(3f, groups[0].GetDistance(), 3);

        Assert.Null(data.GetAiTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesAutoGroupDataFromRealXml()
    {
        var path = ResolveStaticDataFile("auto_group", "auto_group.xml");

        // Self-contained <auto_groups> root; AfterUnmarshal indexes every template by maskId (id) and builds
        // the recruitable npc->maskId map, then nulls the source list. Feeds DataManager.AUTO_GROUP.
        var data = JaxbHolderLoader.LoadFromFile<AutoGroupData>(path);

        Assert.True(data.Size() > 0);

        // Known row: <auto_group id="1" instanceId="300110000" name_id="401193" title_id="401197"
        //   min_lvl="46" max_lvl="50" register_new="true" register_quick="true" register_group="true"
        //   npc_ids="279055 279054 279041 279057 279039 279056"/>
        var ag1 = data.GetTemplateByInstanceMaskId(1);
        Assert.NotNull(ag1);
        Assert.Equal(1, ag1!.GetMaskId());
        Assert.Equal(300110000, ag1.GetInstanceMapId());
        Assert.Equal(401193, ag1.GetL10nId());
        Assert.Equal(401197, ag1.GetTitleId());
        Assert.Equal(46, ag1.GetMinLvl());
        Assert.Equal(50, ag1.GetMaxLvl());
        Assert.True(ag1.CanRegisterNewEntry());
        Assert.True(ag1.CanRegisterQuickEntry());
        Assert.True(ag1.HasRegisterGroup());
        // npc_ids XmlAttribute string-list (space-separated) round-trips intact (not dropped).
        Assert.Contains(279055, ag1.GetNpcIds());
        Assert.Equal(6, ag1.GetNpcIds().Count);

        Assert.Null(data.GetTemplateByInstanceMaskId(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesRecipeDataFromRealXml()
    {
        var path = ResolveStaticDataFile("recipe", "recipe_templates.xml");

        // Self-contained <recipe_templates> root; AfterUnmarshal indexes by id + collects autolearn recipes,
        // then nulls the source list. Feeds DataManager.RECIPE_DATA.
        var data = JaxbHolderLoader.LoadFromFile<RecipeData>(path);

        Assert.True(data.Size() > 0);

        // Known row: <recipe_template id="155000001" nameid="730278" skillid="40009" race="ELYOS"
        //   skillpoint="1" dp="200" autolearn="1" productid="152000401" quantity="3">
        //   <components_data><component quantity="1" itemid="152000901"/></components_data>
        var r = data.GetRecipeTemplateById(155000001);
        Assert.NotNull(r);
        Assert.Equal(155000001, r!.GetId());
        Assert.Equal(730278, r.GetL10nId());
        Assert.Equal(40009, r.GetSkillId());
        Assert.Equal(Model.Race.ELYOS, r.GetRace());
        Assert.Equal(1, r.GetSkillpoint());
        Assert.Equal(200, r.GetDp());
        Assert.Equal(1, r.GetAutoLearn());
        Assert.Equal(152000401, r.GetProductId());
        Assert.Equal(3, r.GetQuantity());

        // Nested <components_data>/<component> must NOT be dropped.
        var comps = r.GetComponents();
        Assert.Single(comps);
        var component = Assert.Single(comps[0].GetComponent());
        Assert.Equal(152000901, component.GetItemId());
        Assert.Equal(1, component.GetQuantity());

        // Nullable @XmlAttribute (max_production_count absent on this row) -> null via string proxy.
        Assert.Null(r.GetMaxProductionCount());

        Assert.Null(data.GetRecipeTemplateById(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesTradeListDataFromRealXml()
    {
        var path = ResolveStaticDataFile("npc_trade_list.xml");

        // Single <npc_trade_list> root with three list kinds (tradelist_template/trade_in_list_template/
        // purchase_template); AfterUnmarshal indexes each by npc_id then nulls the source lists.
        var data = JaxbHolderLoader.LoadFromFile<TradeListData>(path);

        Assert.True(data.Size() > 0);

        // Known row: <tradelist_template npc_id="203060" buy_price_rate="200">
        //   <tradelist id="129"/><tradelist id="130"/><tradelist id="131"/><tradelist id="450"/>
        var t = data.GetTradeListTemplate(203060);
        Assert.NotNull(t);
        Assert.Equal(203060, t!.GetNpcId());
        Assert.Equal(200, t.GetBuyPriceRate());
        // Nested <tradelist> goods-list refs must NOT be dropped.
        var tabs = t.GetTradeTablist();
        Assert.Equal(4, tabs.Count);
        Assert.Equal(129, tabs[0].GetId());
        Assert.Contains(tabs, x => x.GetId() == 450);
        // Default npc_type (absent) -> NORMAL; nullable save_count (absent) -> null via string proxy.
        Assert.Equal(Model.Templates.Tradelist.TradeNpcType.NORMAL, t.GetTradeNpcType());
        Assert.Null(t.IsSaveCount());

        Assert.Null(data.GetTradeListTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesPetDataFromRealXml()
    {
        var path = ResolveStaticDataFile("pets", "pets.xml");

        // Single <pets> root; AfterUnmarshal indexes by templateId then nulls the source list.
        var data = JaxbHolderLoader.LoadFromFile<PetData>(path);

        Assert.True(data.Size() > 0);

        // Known row: <pet id="900000" name="siberian wild tiger (purebred)" nameid="1600012"
        //   condition_reward="188051378"> ...petfunction... <petstats reaction="brave" run_speed="6.0" .../>
        var pet = data.GetPetTemplate(900000);
        Assert.NotNull(pet);
        Assert.Equal(900000, pet!.GetTemplateId());
        Assert.Equal("siberian wild tiger (purebred)", pet.GetName());
        Assert.Equal(1600012, pet.GetL10nId());
        Assert.Equal(188051378, pet.GetConditionReward());
        // Nested <petfunction> elements not dropped (BAG/DOPING/LOOT/WING present in XML).
        Assert.Contains(pet.GetPetFunctions(), f => f.GetPetFunctionType() == Model.Templates.Pet.PetFunctionType.LOOT);
        // Nested <petstats> not dropped.
        var stats = pet.GetPetStats();
        Assert.NotNull(stats);
        Assert.Equal("brave", stats!.GetReaction());
        Assert.Equal(6.0f, stats.GetRunSpeed(), 3);

        Assert.Null(data.GetPetTemplate(-99999));
    }

    [Fact]
    public void LoadFromFiles_PopulatesNpcSkillDataFromRealXml_Merged()
    {
        // Java imports the npc_skills/ dir with singleRootTag+recursiveImport: merge every <npc_skill_templates>
        // source (npc_skills.xml + guard/siege/rift + instances/* + open_worlds/*) then run AfterUnmarshal once.
        var dir = Path.GetDirectoryName(ResolveStaticDataFile("npc_skills", "npc_skills.xml"))!;

        NpcSkillData? data = null;
        foreach (var file in Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var part = JaxbHolderLoader.DeserializeFile<NpcSkillData>(file);
            if (data == null) data = part; else data.MergePending(part);
        }
        Assert.NotNull(data);
        JaxbHolderLoader.RunAfterUnmarshal(data!);

        Assert.True(data!.Size() > 0);

        // Known row (npc_skills.xml): <npc_skills npc_ids="201010"><npc_skill id="17424" lv="9" prob="25"/>...
        var list = data.GetNpcSkillList(201010);
        Assert.NotNull(list);
        Assert.Contains(201010, list!.GetNpcIds());
        var skills = list.GetNpcSkills();
        Assert.NotNull(skills);
        Assert.Contains(skills!, s => s.GetSkillId() == 17424 && s.GetSkillLevel() == 9 && s.GetProbability() == 25);

        Assert.Null(data.GetNpcSkillList(-99999));
    }

    [Fact]
    public void LoadFromFiles_PopulatesWalkerDataFromRealXml_Merged()
    {
        // Java imports the npc_walker/ dir with singleRootTag+recursiveImport: merge every <npc_walker> source
        // (per-instance route files) then run AfterUnmarshal once (which fires each WalkerTemplate.AfterUnmarshal
        // children-first: route-step indexing).
        var dir = Path.GetDirectoryName(ResolveStaticDataFile("npc_walker", "300100000_Steel Rake.xml"))!;

        WalkerData? data = null;
        foreach (var file in Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var part = JaxbHolderLoader.DeserializeFile<WalkerData>(file);
            if (data == null) data = part; else data.MergePending(part);
        }
        Assert.NotNull(data);
        JaxbHolderLoader.RunAfterUnmarshal(data!);

        Assert.True(data!.Size() > 0);

        // Known route (Steel Rake): route_id="0222C904738D487BE4D4F76C9DC76E63A8FD1EB9" pool="2" formation="SQUARE".
        var route = data.GetWalkerTemplate("0222C904738D487BE4D4F76C9DC76E63A8FD1EB9");
        Assert.NotNull(route);
        Assert.Equal(2, route!.GetPool());
        // Nested <routestep> elements not dropped; AfterUnmarshal indexed them and flagged the last step.
        var steps = route.GetRouteSteps();
        Assert.True(steps.Count > 1);
        Assert.Equal(0, steps[0].GetStepIndex());
        Assert.True(steps[steps.Count - 1].IsLastStep());
        // pool==2 forces SQUARE formation in WalkerTemplate.AfterUnmarshal.
        Assert.Equal(SpawnEngine.WalkerGroupType.SQUARE, route.GetType_());

        Assert.Null(data.GetWalkerTemplate("__nope__"));
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
