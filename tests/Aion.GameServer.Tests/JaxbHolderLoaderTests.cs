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
