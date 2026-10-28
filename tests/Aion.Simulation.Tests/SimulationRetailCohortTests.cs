using System.Text.Json;
using System.Text.RegularExpressions;
using Aion.Bots.Dashboard;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Retail;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    /// <summary>Controlled incoming attacks, followed by ordinary packet-driven
    /// fights against ordinary SpawnEngine actors at authored spots. This isolated
    /// component fixture does not bypass or claim production map admission. Gear,
    /// learned skills, NPC HP and damage are never edited.</summary>
    [SkippableFact]
    public async Task ReviewedRetailCohortsUseActualStatsAndCombatInSim()
    {
        Skip.IfNot(fixture.IsAvailable,fixture.SkipReason);
        Skip.If(Environment.GetEnvironmentVariable("AION_RETAIL_COHORT_SIM")!="1","Requires a fresh private character clone and reviewed cohort manifest");
        bool production=RetailNpcCohortCatalog.Enabled;
        string output=Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")??throw new InvalidOperationException("External SIM evidence directory required");
        string manifest=Environment.GetEnvironmentVariable(production?RetailNpcCohortCatalog.ManifestEnvironment:"AION_RETAIL_COHORT_FIXTURE_MANIFEST")??throw new InvalidOperationException("External reviewed manifest required");
        string manifestHash=Environment.GetEnvironmentVariable(production?RetailNpcCohortCatalog.ManifestHashEnvironment:"AION_RETAIL_COHORT_FIXTURE_SHA256")??throw new InvalidOperationException("Separate reviewed digest required");
        using var manifestDoc=JsonDocument.Parse(File.ReadAllBytes(manifest));
        var sites=manifestDoc.RootElement.GetProperty("cohorts").EnumerateArray().SelectMany(c=>
            c.GetProperty("members").EnumerateArray().Select(m=>(Map:c.GetProperty("map_id").GetInt32(),Npc:m.GetProperty("npc_id").GetInt32())))
            .Select(id=>new{id.Map,id.Npc,Spot=Aion.GameServer.Dataholders.DataManager.SPAWNS_DATA.GetSpawnsForNpc(id.Map,id.Npc)
                .SelectMany(g=>g.GetSpawnTemplates()).First()}).ToArray();
        // Existing actors cached legacy stats at construction. Never relabel those
        // live objects: remove this fixture's selected IDs in every private-world
        // instance, then create fresh ordinary actors after profile installation.
        if(!production)
        foreach(var map in sites.GroupBy(s=>s.Map))
        foreach(var instance in fixture.World.GetWorldMap(map.Key))
        foreach(var npc in instance.GetNpcs().Where(n=>instance.GetInstanceId()==fixture.World.GetWorldMap(map.Key).GetMainWorldMapInstance().GetInstanceId()
            ||map.Any(s=>s.Npc==n.GetNpcId())).ToArray())npc.GetController().Delete();
        using var cohortScope=production?null:RetailNpcCohortCatalog.InstallForTests(File.ReadAllBytes(manifest),manifestHash,Path.GetDirectoryName(manifest)!);
        var admitted=RetailNpcCohortCatalog.GetActiveProfiles();
        RetailNpcCohortCombatReadiness.Validate(admitted);
        if(!production)SkyTempleNpcNavigationAssets.InitializeCohorts(RetailNpcCohortCatalog.GetPreparedMaps());
        // Record the real exit gate separately. Component verification is useful
        // while outdoor terrain compatibility is blocked; it cannot grant rollout.
        var mapGate=Record.Exception(SkyTempleNpcNavigationAssets.ValidateLoadedMaps);
        await File.WriteAllTextAsync(Path.Combine(output,"production-map-gate.json"),JsonSerializer.Serialize(new
            {passed=mapGate is null,error=mapGate?.ToString(),productionBootstrap=production,
                scope=production?"Normal startup admission and shipped population":"Component test scope only; no production publication is attempted"}));
        if(production)Assert.Null(mapGate);
        if(!production)
        foreach(var site in sites)
            Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(
                site.Map,site.Npc,site.Spot.GetX(),site.Spot.GetY(),site.Spot.GetZ(),site.Spot.GetHeading()),
                fixture.World.GetWorldMap(site.Map).GetMainWorldMapInstance().GetInstanceId());
        string run=Environment.GetEnvironmentVariable("AION_SIM_RUN_ID")??"retail-cohorts";
        using var policy=new SimulationLogPolicy(run,"RETAIL-COHORTS",fixture.Clock,allowlistPath,
            captureProvider:fixture.LogCapture,loggerFactory:fixture.LoggerFactory,evidence:evidence);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(25));var token=timeout.Token;
        string tracePath=Path.Combine(output,"combat.trace.jsonl");
        using var trace=BotActionTraceWriter.Open(tracePath,run,"b01","sim-player-1",
            virtualTime:()=>TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
        var(character,characterId,level,playerClass,line)=Environment.GetEnvironmentVariable("AION_RETAIL_COHORT_CHARACTER") switch
        {
            "gladiator"=>("Asimgladiator",320000,38,PlayerClass.GLADIATOR,NaturalClassLine.WarriorGladiator),
            "priest"=>("Pilot",104870,9,PlayerClass.PRIEST,NaturalClassLine.PriestCleric),
            _=>("Farmer",104728,16,PlayerClass.SORCERER,NaturalClassLine.MageSorcerer)
        };
        await using var session=new SimulationL0Session(fixture,policy,"b01",1,character,Race.ASMODIANS,trace,tracePath)
            {IdentityClassLine=line};
        var dashboard=new LiveBotDashboardState();
        await using var monitor=new LiveBotDashboardHost(run,["RETAIL-COHORTS"],dashboard,
            int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT")??"17880"));
        session.Dashboard=dashboard;
        using var runtimeLog=new ThreeKeysRuntimeLogProvider(Path.Combine(output,"cohort-events.jsonl"),()=>fixture.Clock.NowMillis);
        fixture.LoggerFactory.AddProvider(runtimeLog);
        Console.WriteLine("Retail cohort SIM monitor: "+monitor.Url);
        await session.LoginCharacterListAsync(token);session.SelectCharacter(characterId,character);
        await session.EnterWorldAsync(token);await session.SynchronizeAsync(token);
        var player=fixture.World.GetPlayer(session.CharacterId);
        Assert.Equal(level,(int)player.GetLevel());Assert.Equal(playerClass,player.GetPlayerClass());
        Assert.False(player.IsStaff());Assert.False(player.IsInvulnerable());
        await File.WriteAllTextAsync(Path.Combine(output,"character-effects-at-start.json"),JsonSerializer.Serialize(
            player.GetEffectController().GetAllEffects().Select(e=>new{id=e.GetSkillId(),name=e.GetSkillTemplate().GetName()})),token);
        Assert.DoesNotContain(player.GetEffectController().GetAllEffects(),e=>e.GetSkillId()==240||e.GetSkillTemplate().GetName()?.StartsWith("GM",StringComparison.OrdinalIgnoreCase)==true);
        await File.WriteAllTextAsync(Path.Combine(output,"character-at-start.json"),JsonSerializer.Serialize(new
        {
            session.CharacterId,level=player.GetLevel(),hp=player.GetLifeStats().GetCurrentHp(),maxHp=player.GetLifeStats().GetMaxHp(),
            pdef=player.GetGameStats().GetPDef().GetCurrent(),mdef=player.GetGameStats().GetMDef().GetCurrent(),
            equipment=session.Api.World.Inventory.Values.Where(i=>i.Details.EquippedSlot is {} slot&&slot!=0)
                .Select(i=>new{i.ObjectId,i.ItemId,i.Details.EquippedSlot}),skills=session.Api.World.Skills,
            setup="Fresh copy of an existing normal character; gear/learned skills/quests retained. Only online flags and copied GM Armor240/GM Hustle396 cleared in scratch DB; ordinary buffs retained. Controlled calculation target, not a level-appropriate balance claim."
        }),token);
        BotPosition At(VisibleObject actor)=>new(actor.GetX(),actor.GetY(),actor.GetZ(),actor.GetHeading());
        BotNavigationGeometry Geometry()=>BotNavigationGeometry.ForServerWorld(player.GetInstanceId(),Race.ASMODIANS);
        var runtime=new NaturalJourneyRuntime(RealStaticData.RepoRoot(),"SIM-RETAIL-COHORTS",fixture.Seed,
            fixture.DataManager.StaticData,()=>fixture.Clock.NowMillis,fixture.Epoch,Geometry,
            _=>Task.FromResult(false),policy.CheckClean,()=>policy.SnapshotProblems(),trace,dashboard);
        var journey=new NaturalIshalgenJourney(session,runtime,new NaturalJourneyOptions(ClassLine:line));
        var profiles=RetailNpcCohortCatalog.GetActiveProfiles().OrderBy(p=>p.Cohort!.MapId).ThenBy(p=>p.NpcId).ToArray();
        var census=new List<object>();
        foreach(var p in profiles)
        {
            int map=p.Cohort!.MapId;
            var spawns=fixture.World.GetWorldMap(map).GetMainWorldMapInstance().GetNpcs(p.NpcId).ToArray();
            Assert.NotEmpty(spawns);
            foreach(var n in spawns)
            {
                Assert.True(SkyTempleNpcProfileCatalog.TryGet(n,out var selected));Assert.Same(p,selected);
                Assert.Equal(p.MaxHp,n.GetLifeStats().GetMaxHp());Assert.Equal(p.Level,(int)n.GetLevel());
                var s=n.GetGameStats();
                Assert.Equal(p.PhysicalDefense,s.GetPDef().GetCurrent());Assert.Equal(p.MagicalDefense,s.GetMDef().GetCurrent());
                Assert.Equal(p.MagicalResist,s.GetMResist().GetCurrent());Assert.Equal(p.PhysicalAccuracy,s.GetMainHandPAccuracy().GetCurrent());
                Assert.Equal(p.Dodge,s.GetEvasion().GetCurrent());Assert.Equal(p.PhysicalCritical,s.GetMainHandPCritical().GetCurrent());
                Assert.Equal(p.MagicalCritical,s.GetMCritical().GetCurrent());Assert.Equal(p.HpRegen,s.GetHpRegenRate().GetCurrent());
                census.Add(new{map,npc=p.NpcId,obj=n.GetObjectId(),p.MaxHp,p.PhysicalDefense,p.MagicalDefense,p.MagicalResist,
                    p.PhysicalAccuracy,p.Dodge,p.PhysicalCritical,p.MagicalCritical,p.SourceRecordSha256,p.Generation,position=At(n)});
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output,"all-cohort-spawns.json"),JsonSerializer.Serialize(census),token);
        string? requested=Environment.GetEnvironmentVariable("AION_RETAIL_COHORT_SIM_NPCS");
        var only=requested?.Split(',').Select(int.Parse).ToHashSet();
        var outcomes=new List<object>();
        foreach(var p in profiles.Where(p=>only is null||only.Contains(p.NpcId)))
        {
            int map=p.Cohort!.MapId;
            Npc npc=fixture.World.GetWorldMap(map).GetMainWorldMapInstance().GetNpcs(p.NpcId)
                .First(n=>n.IsSpawned()&&!n.IsDead()&&n.GetLifeStats().GetCurrentHp()==p.MaxHp);
            if(player.GetWorldId()!=map)
            {
                // The ordinary combat helper captures its entry map and observes
                // genuine post-kill transitions. Complete director map setup first.
                var landing=Geometry().GroundAround(map,At(npc),[30f,40f,50f]).First();
                await TeleportForSetupAsync(session,player,map,landing.X,landing.Y,landing.Z,token);
                await session.SynchronizeAsync(token);
            }
            string[] logs=[],normals=[],losses=[];
            var result=await journey.RunObservedCombatAsync(async _=>
            {
                // Stay within actual attack reach. Outdoor path/version acceptance
                // is a separate failing gate, not inferred from in-range combat.
                await TeleportForSetupAsync(session,player,map,npc.GetX()+.5f,npc.GetY(),npc.GetZ(),token);
                await session.SynchronizeAsync(token);
                var assets=SkyTempleNpcNavigationAssets.For(npc);var scene=assets.CurrentScene(npc);
                var body=new SkyTempleNpcBody(p.Bounds.Front,p.Bounds.Side,p.Bounds.Upper,p.Bounds.HorizontalRadius,p.SourceRecordSha256,p.NpcId);
                var planner=new SkyTempleNpcRoutePlanner(assets,scene,body,"cohort-sim-"+p.NpcId);
                var stand=Geometry().GroundAround(map,At(npc),[.5f,1f,1.5f]).First(pos=>
                    planner.Visible(SkyTempleNpcNavigation.Position(npc),body.Upper,new(pos.X,pos.Y,pos.Z),player.GetObjectTemplate().GetBoundRadius().GetUpper()));
                await TeleportForSetupAsync(session,player,map,stand.X,stand.Y,stand.Z,token);await session.SynchronizeAsync(token);
                await EndTeleportProtection();
                Assert.Contains(npc.GetObjectId(),session.Api.World.Objects.Keys);
                int logStart=fixture.LogCapture.Entries.Count,packetStart=session.PacketHistory.Count;
                session.BeginStep("incoming-"+p.NpcId,"controlled-hate-setup-ordinary-AI-attacks");
                npc.GetAggroList().AddHate(player,1);
                for(int tick=0;tick<150&&!player.IsDead();tick++)
                {
                    var normal=fixture.LogCapture.Entries.Skip(logStart).Where(e=>e.Message.StartsWith("RETAIL_SKYTEMPLE_CALC ")&&e.Message.Contains("source="+npc.GetObjectId()+" ")).ToArray();
                    if(normal.Length>=2)break; // Optional action selection is not a skill-coverage gate.
                    if(player.GetLifeStats().GetCurrentHp()<player.GetLifeStats().GetMaxHp()*.35f)break;
                    await session.AdvanceAsync(TimeSpan.FromMilliseconds(200),token);await session.SynchronizeAsync(token);
                }
                Assert.False(player.IsDead(),"Normal copied character died during incoming-attack fixture: "+p.NpcId);
                logs=fixture.LogCapture.Entries.Skip(logStart).Select(e=>e.Message).ToArray();
                normals=logs.Where(m=>m.StartsWith("RETAIL_SKYTEMPLE_CALC ")&&m.Contains("source="+npc.GetObjectId()+" ")).ToArray();
                Assert.NotEmpty(normals);
                foreach(string message in normals)
                {
                    var fields=Fields(message);
                    Assert.Equal(SkyTempleCombatInputs.Element(p.Attacks[0].Type).ToString(),fields["element"]);
                    Assert.Equal($"{p.MinDamage}..{p.MaxDamage}",fields["range"]);
                    Assert.InRange(int.Parse(fields["sample"]),p.MinDamage,p.MaxDamage);
                }
                losses=logs.Where(m=>m.StartsWith("RETAIL_SKYTEMPLE_HP ")&&m.Contains("source="+npc.GetObjectId()+" ")&&m.Contains("target="+player.GetObjectId()+" ")).ToArray();
                Assert.NotEmpty(losses);
                foreach(string message in losses)
                {
                    var f=Fields(message);int before=int.Parse(f["before"]),after=int.Parse(f["after"]),requestedHp=int.Parse(f["requested"]);
                    Assert.Equal(Math.Min(before,Math.Max(0,requestedHp)),before-after);
                }
                Assert.Contains(session.PacketHistory.Skip(packetStart),packet=>packet.PacketType==typeof(SM_ATTACK));
                session.BeginStep("fight-"+p.NpcId,"ordinary-packets-unchanged-shipped-NPC");
                return npc.GetObjectId();
            },token);
            var outcome=new{map,npc=p.NpcId,obj=npc.GetObjectId(),p.MaxHp,p.MinDamage,p.MaxDamage,p.Attacks,result,normalCount=normals.Length,
                hpLosses=losses,logs,scope=production?"Normal bootstrap, shipped population, controlled in-range encounters and ordinary client packets; no full-map travel/rendering claim":
                    "Isolated private-map component fixture; fresh NPC at authored spot, in-range player placement/hate then ordinary client packets; no natural population, outdoor rollout, rendering or travel claim"};
            outcomes.Add(outcome);
            await File.WriteAllTextAsync(Path.Combine(output,$"npc-{p.NpcId}.json"),JsonSerializer.Serialize(outcome),token);
            Assert.True(result.Killed,"Ordinary packet-driven kill failed: "+p.NpcId);Assert.Equal(0,result.Deaths);Assert.Equal(0,npc.GetLifeStats().GetCurrentHp());
            policy.CheckClean();
            // Ordinary rest uses the character's existing abilities; no test heals.
            await journey.RunObservedRestAsync(token);
            Console.WriteLine($"Retail cohort {map}/{p.NpcId}: {normals.Length} normals; killed={result.Killed}; deaths={result.Deaths}");
        }
        Assert.NotEmpty(outcomes);
        var skillOutcomes=new List<object>();
        if(Environment.GetEnvironmentVariable("AION_RETAIL_COHORT_SKILL_FIXTURES")=="1")
        {
            Assert.False(production,"Explicit queued-skill fixtures require the isolated component mode");
            foreach(var p in profiles.Where(p=>p.Skills.Length>0))
            foreach(var selected in p.Skills)
            {
                var site=sites.Single(s=>s.Map==p.Cohort!.MapId&&s.Npc==p.NpcId);
                bool committed=false;var attempts=new List<object>();
                for(int trial=0;trial<10&&!committed;trial++)
                {
                    // One controlled caster, with real targetability. Teleport
                    // protection legitimately removes hostile spell targets; do
                    // not mistake an empty result list for a damage calculation.
                    foreach(var existing in fixture.World.GetWorldMap(site.Map).GetMainWorldMapInstance().GetNpcs().ToArray())existing.GetController().Delete();
                    await journey.RunObservedRestAsync(token);
                    await TeleportForSetupAsync(session,player,site.Map,site.Spot.GetX()+.5f,site.Spot.GetY(),site.Spot.GetZ(),token);
                    await session.SynchronizeAsync(token);
                    await EndTeleportProtection();
                    var caster=Assert.IsAssignableFrom<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(
                        Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(site.Map,site.Npc,
                            site.Spot.GetX(),site.Spot.GetY(),site.Spot.GetZ(),site.Spot.GetHeading()),1));
                    try
                    {
                        await session.SynchronizeAsync(token);
                        Assert.True(caster.CanTarget(player));
                        int start=fixture.LogCapture.Entries.Count;
                        session.BeginStep($"queued-source-skill-{p.NpcId}-{selected.Id48}-{trial}","controlled-selection-actual-source-binding-and-existing-cast-owner");
                        int resistThreshold=RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(
                            RetailNpcNormalStatusMath.Context.Create(SkyTempleCombatInputs.Scope(p.NpcId),RetailNpcNormalScope.MainBinarySha256,"controlled current-target spell fixture",false),
                            new(player.GetGameStats().GetMResist().GetCurrent(),caster.GetGameStats().GetMAccuracy().GetCurrent(),(uint)p.StatRatio,p.Level,player.GetLevel(),SkyTempleCombatInputs.Pose(player)));
                        caster.QueueSkill(selected.Id48,selected.Level);
                        caster.GetAggroList().AddHate(player,1);
                        string[] events=[];
                        for(int poll=0;poll<75;poll++)
                        {
                            await session.AdvanceAsync(TimeSpan.FromMilliseconds(200),token);await session.SynchronizeAsync(token);
                            events=fixture.LogCapture.Entries.Skip(start).Select(e=>e.Message)
                                .Where(m=>m.Contains("source="+caster.GetObjectId()+" ")&&m.Contains("skill="+selected.Id48+" ")).ToArray();
                            if(events.Any(m=>m.StartsWith("RETAIL_SKYTEMPLE_HP "))){committed=true;break;}
                        }
                        Assert.Contains(events,m=>m.StartsWith("RETAIL_SKYTEMPLE_SKILL_START "));
                        Assert.Contains(events,m=>m.StartsWith("RETAIL_SKYTEMPLE_SKILL_CAST_END "));
                        foreach(var message in events.Where(m=>m.StartsWith("RETAIL_SKYTEMPLE_HP ")))
                        {
                            var f=Fields(message);int before=int.Parse(f["before"]),after=int.Parse(f["after"]);
                            Assert.Equal(Math.Min(before,Math.Max(0,int.Parse(f["requested"]))),before-after);
                        }
                        attempts.Add(new{trial,committed,resistThreshold,events});
                        Assert.False(player.IsDead());policy.CheckClean();
                    }
                    finally{caster.GetController().Delete();await session.SynchronizeAsync(token);}
                }
                var record=new{npc=p.NpcId,skill=selected.Id48,level=selected.Level,committed,attempts,
                    scope="Explicit source-row queue selection; actual AI/cast/recovery/projectile/HP owners, not natural chooser coverage"};
                skillOutcomes.Add(record);
                await File.WriteAllTextAsync(Path.Combine(output,$"source-skill-{p.NpcId}-{selected.Id48}.json"),JsonSerializer.Serialize(record),token);
                Assert.True(committed,$"No actual HP consumer for source skill {p.NpcId}/{selected.Id48}/{selected.Level}");
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output,"acceptance.json"),JsonSerializer.Serialize(new
            {enabledMembers=profiles.Length,testedMembers=outcomes.Count,productionBootstrap=production,character,requestedIds=only?.Order().ToArray(),spawnCensus=census.Count,outcomes,skillOutcomes}),token);
        policy.AssertClean();
        async Task EndTeleportProtection()
        {
            if(!player.IsProtectionActive())return;
            var origin=session.CurrentPosition;
            var destination=new BotPosition(origin.X+.2f,origin.Y,origin.Z,origin.Heading);
            var plan=new BotMover(session.Api.World,session.Api.Timing).CreateGroundPlan([origin,destination],origin,
                session.Api.World.MovementSpeed??throw new InvalidDataException("Player speed unobserved"),TimeSpan.FromMilliseconds(200));
            await session.ExecuteMovementAsync(plan,token);await session.SynchronizeAsync(token);
            Assert.False(player.IsProtectionActive(),"Ordinary client movement must end teleport protection before the fixture");
        }
        static Dictionary<string,string> Fields(string message)=>Regex.Matches(message,@"(\w+)=([^ ]+)")
            .ToDictionary(m=>m.Groups[1].Value,m=>m.Groups[2].Value);
    }
}
