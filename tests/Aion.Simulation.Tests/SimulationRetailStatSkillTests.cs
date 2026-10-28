using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Movement;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Retail;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    /// <summary>Explicit selection of source-bound primary skills through existing
    /// cast/impact/HP owners. No native selector or natural balance claim.</summary>
    [SkippableFact]
    public async Task RetailStatisticsPrimarySkillsCommitSourceDamageInSim()
    {
        Skip.IfNot(fixture.IsAvailable,fixture.SkipReason);
        Skip.If(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SKILL_SIM")!="1","Requires a fresh scratch character and statistics manifest");
        Assert.True(RetailNpcStatCatalog.Enabled);
        string output=Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")??throw new InvalidOperationException("External SIM evidence required");
        string run=Environment.GetEnvironmentVariable("AION_SIM_RUN_ID")??"retail-stat-skills";
        using var policy=new SimulationLogPolicy(run,"RETAIL-STAT-SKILLS",fixture.Clock,allowlistPath,
            new SimulationLogPolicyOptions {FailOnWarnings=false},fixture.LogCapture,fixture.LoggerFactory,evidence:evidence);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(40));var token=timeout.Token;
        await using var session=new SimulationL0Session(fixture,policy,"b01",1,"Asimgladiator",Race.ASMODIANS)
            {IdentityClassLine=NaturalClassLine.WarriorGladiator};
        var dashboard=new LiveBotDashboardState();
        await using var monitor=new LiveBotDashboardHost(run,["RETAIL-STAT-SKILLS"],dashboard,
            int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT")??"17880"));
        session.Dashboard=dashboard;Console.WriteLine("Retail skill SIM monitor: "+monitor.Url);
        using var log=new ThreeKeysRuntimeLogProvider(Path.Combine(output,"skill-events.jsonl"),()=>fixture.Clock.NowMillis);
        fixture.LoggerFactory.AddProvider(log);
        await session.LoginCharacterListAsync(token);session.SelectCharacter(320000,"Asimgladiator");
        await session.EnterWorldAsync(token);await session.SynchronizeAsync(token);
        var player=fixture.World.GetPlayer(session.CharacterId);
        Assert.False(player.IsStaff());Assert.False(player.IsInvulnerable());Assert.Equal(38,(int)player.GetLevel());
        string? equipmentFixture=InstallExclusiveEquipmentFixture(player,output);
        object DefenseSnapshot()
        {
            if(RetailNpcStatCatalog.StatusSources is null)return new {available=false};
            var state=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatPhysicalStatuses.For(player);
            if(!state.CanRead(out string reason))return new {available=true,known=false,reason};
            return new {available=true,known=true,
                dodge=state.Read(Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatStatusSources.StatusKind.Dodge),
                block=state.Read(Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatStatusSources.StatusKind.Block),
                parry=state.Read(Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatStatusSources.StatusKind.Parry)};
        }
        string selection=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SKILL_SELECTION")??"source";
        Assert.Contains(selection,new[]{"source","existing-host","scripted-helper"});
        bool useExistingHost=selection=="existing-host";
        bool useScriptedHelper=selection=="scripted-helper";
        var maps=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_MAPS")?.Split(',').Select(int.Parse).ToHashSet()??[220010000,220030000];
        // Map0 memberships are reusable unassigned templates, never a World object.
        // A controlled numeric fixture may explicitly choose an existing supported
        // world; retain the original sourceMap in the inventory. No spawn data changes.
        int? unassignedMap=int.TryParse(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_UNASSIGNED_FIXTURE_MAP"),out int fixtureMap)?fixtureMap:null;
        if(unassignedMap.HasValue)Assert.Contains(unassignedMap.Value,RetailNpcStatCatalog.SupportedMaps);
        var cases=new List<(int Map,SkyTempleNpcProfile Profile,int Skill,int Level,RetailNpcStatSkillCombat.Source Source,int Entry)>();
        var inventory=new List<object>();
        foreach(var m in RetailNpcStatCatalog.Members.Where(m=>maps.Contains(m.MapId)))
        {
            if(m.MapId==0&&!unassignedMap.HasValue)
            {
                inventory.Add(new {sourceMap=0,npc=m.Profile.NpcId,admitted=false,reason="unassigned-template-requires-explicit-supported-fixture-world"});
                continue;
            }
            int mapId=m.MapId==0?unassignedMap!.Value:m.MapId;
            var groups=m.Profile.Source.Source.GetRawRootChildren("skills");
            if(groups.Length!=1)continue;
            var entries=Aion.GameServer.Dataholders.RetailNpc.RetailNpcSourceProfile.RawChildren(groups[0])
                .Where(x=>x[0].GetString()=="data").ToArray();
            foreach(var b in m.Profile.Source.Source.SkillBindings.EnumerateArray())
            {
                string? bindingStatus=b.GetProperty("status").GetString();
                if(bindingStatus!="dual_join_agrees_static_only")
                {
                    inventory.Add(new {map=mapId,sourceMap=m.MapId,npc=m.Profile.NpcId,name=b.GetProperty("name").GetString(),bindingStatus,
                        admitted=false,reason="missing-independent-source-and-48-identity"});
                    continue;
                }
                int id=b.GetProperty("supported48_id").GetInt32(),index=b.GetProperty("source_entry_index").GetInt32();
                int level=SkyTempleNpcProfile.LeafInt(entries[index],"skill_level");
                bool bound=RetailNpcStatCatalog.TryGetSkill(m.Profile,id,level,out var source);
                inventory.Add(new {map=mapId,sourceMap=m.MapId,npc=m.Profile.NpcId,skill=id,level,sourceEntryIndex=index,bindingStatus,
                    admitted=bound&&source.Supported,reason=bound?source.UnsupportedReason:"missing-exact-source-binding",
                    family=bound?source.Family.ToString():null,sourceSha256=bound?source.Sha256:null});
                if(bound&&source.Supported)cases.Add((mapId,m.Profile,id,level,source,index));
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output,"skill-inventory.json"),JsonSerializer.Serialize(new
        {
            maps=maps.Order(),inventory,
            scope="Current C# primary recipe admission and static binding inventory. Unsupported primary family does not establish a legacy buff/heal mismatch or a native runtime failure."
        }),token);
        Assert.NotEmpty(cases);
        var only=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_NPCS")?.Split(',').Select(int.Parse).ToHashSet();
        var skills=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_SKILLS")?.Split(',').Select(int.Parse).ToHashSet();
        string direction=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_DIRECTION")??"default";
        Assert.Contains(direction,new[]{"default","back","front","zero"});
        cases=cases.Where(c=>(only is null||only.Contains(c.Profile.NpcId))&&(skills is null||skills.Contains(c.Skill))).ToList();Assert.NotEmpty(cases);
        string[] targetStates=(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_TARGET_STATES")??"standing").Split(',');
        Assert.NotEmpty(targetStates);Assert.Equal(targetStates.Length,targetStates.Distinct().Count());
        foreach(string state in targetStates)Assert.Contains(state,new[]{"standing","stumble","ended","poison","bleed"});
        string[] defenseStates=(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_DEFENSE_STATES")??"none").Split(',');
        Assert.NotEmpty(defenseStates);Assert.Equal(defenseStates.Length,defenseStates.Distinct().Count());
        foreach(string state in defenseStates)Assert.Contains(state,new[]{"none","all","block-many","dodge-ended-older"});
        string blindSetting=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_SOURCE_BLIND")??"0";
        Assert.Contains(blindSetting,new[]{"0","1"});bool sourceBlindFixture=blindSetting=="1";
        if(sourceBlindFixture||defenseStates.Any(s=>s!="none"))Assert.NotNull(RetailNpcStatCatalog.StatusSources);
        var stateCases=cases.SelectMany(c=>targetStates.SelectMany(state=>defenseStates.Select(defense=>
            (c.Map,c.Profile,c.Skill,c.Level,c.Source,c.Entry,State:state,DefenseState:defense)))).ToList();
        var results=new List<object>();var blocked=new List<string>();int deaths=0,excludedFriendly=0,excludedSelf=0,excludedAbsentHost=0,excludedSourceVariant=0;
        await using var progress=new StreamWriter(Path.Combine(output,"skill-outcomes.jsonl"));
        foreach(var group in stateCases.GroupBy(c=>c.Map))
        {
            var instance=fixture.World.GetWorldMap(group.Key).GetMainWorldMapInstance();
            foreach(var npc in instance.GetNpcs().ToArray())npc.GetController().Delete();
            var map=fixture.World.GetWorldMap(group.Key);
            var spot=group.SelectMany(c=>DataManager.SPAWNS_DATA.GetSpawnsForNpc(c.Map,c.Profile.NpcId)).SelectMany(s=>s.GetSpawnTemplates()).FirstOrDefault()
                ??DataManager.SPAWNS_DATA.GetSpawnsByWorldId(group.Key).SelectMany(s=>s.GetSpawnTemplates()).FirstOrDefault();
            bool syntheticAnchor=spot is null;
            spot??=Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(group.Key,group.First().Profile.NpcId,
                Math.Max(64,map.GetWorldSize()/2f),Math.Max(64,map.GetWorldSize()/2f),Math.Max(map.GetWaterLevel(),map.GetDeathLevel())+100,0);
            bool positioned=false;
            foreach(var c in group)
            {
                var trials=new List<object>();bool committed=false,counterpartyEligible=true;
                IReadOnlyList<RetailNpcStatSkillLevels.Decision> levelDecisions=[];
                int? hostLevelUsed=null;
                // A level38 ordinary target can legitimately resist90% of low
                // NPC spells. Probe a bounded positive path without changing its
                // stats or treating this sample as a hit-frequency/balance test.
                for(int trial=0;trial<256&&!committed;trial++)
                {
                    if(player.IsDead())
                    {
                        await session.AdvanceAsync(TimeSpan.FromSeconds(3),token);
                        await session.SendPacketAsync(session.Api.Revive(),token);await session.SynchronizeAsync(token);
                        Assert.False(player.IsDead());positioned=false;
                    }
                    if(!positioned||player.GetWorldId()!=c.Map)
                    {
                        await TeleportForSetupAsync(session,player,c.Map,spot.GetX()+.5f,spot.GetY(),spot.GetZ(),token);
                        await session.SynchronizeAsync(token);
                        if(player.IsProtectionActive())
                        {
                            player.GetController().StopProtectionActiveTask();
                            await session.SynchronizeAsync(token);Assert.False(player.IsProtectionActive());
                        }
                        positioned=true;
                    }
                    var spawn=Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(c.Map,c.Profile.NpcId,
                        player.GetX()+(direction=="front"?.5f:direction=="zero"?0f:-.5f),
                        player.GetY()+(direction=="zero"?-.5f:0f),player.GetZ(),spot.GetHeading(),null,"retail_statistics_fixture");
                    var caster=Assert.IsAssignableFrom<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn,instance.GetInstanceId()));
                    Aion.GameServer.SkillEngine.Model.Effect? fixtureStumble=null;
                    var stumbleTemplate=new Aion.GameServer.SkillEngine.Effects.StumbleEffect();
                    Aion.GameServer.SkillEngine.Model.Effect? fixturePeriodic=null;
                    Aion.GameServer.SkillEngine.Effects.AbstractOverTimeEffect? periodicTemplate=null;
                    Aion.GameServer.SkillEngine.Effects.AbnormalState? periodicState=null;
                    var statusFixtures=new List<Aion.GameServer.SkillEngine.Model.Effect>();
                    try
                    {
                        caster.UnsetState(CreatureState.RESTING);
                        int start=fixture.LogCapture.Entries.Count,before=player.GetLifeStats().GetCurrentHp();
                        caster.SetTarget(player);
                        int castLevel=c.Level;
                        if(useExistingHost)
                        {
                            var list=caster.GetSkillList();
                            var existing=Enumerable.Range(0,list.Size()).Select(list.GetSkillOnPosition)
                                .Where(e=>e.GetSkillId()==c.Skill).ToArray();
                            levelDecisions=RetailNpcStatSkillLevels.GetDecisions(caster).Where(d=>d.SkillId==c.Skill).ToArray();
                            if(existing.Length==0)
                            {
                                counterpartyEligible=false;excludedAbsentHost++;
                                trials.Add(new {trial,started=false,limitation="source-skill-absent-existing-host-list"});break;
                            }
                            var chosen=existing.FirstOrDefault(e=>e.GetSkillLevel()==c.Level);
                            if(chosen is null)
                            {
                                if(levelDecisions.Any(d=>d.Status=="ambiguous-source-level")
                                    &&cases.Any(other=>other.Map==c.Map&&other.Profile.NpcId==c.Profile.NpcId&&other.Skill==c.Skill
                                        &&existing.Any(e=>e.GetSkillLevel()==other.Level)))
                                {
                                    counterpartyEligible=false;excludedSourceVariant++;
                                    trials.Add(new {trial,started=false,limitation="source-level-variant-absent-existing-host-list",
                                        actualLevels=existing.Select(e=>e.GetSkillLevel()).ToArray(),levelDecisions});break;
                                }
                                trials.Add(new {trial,started=false,limitation="existing-host-level-unresolved",
                                    actualLevels=existing.Select(e=>e.GetSkillLevel()).ToArray(),levelDecisions});break;
                            }
                            castLevel=chosen.GetSkillLevel();hostLevelUsed=castLevel;
                            // Read the real entry's numeric level. AI selection,
                            // end-cast spawn callbacks and cooldown metadata are
                            // separate owners, outside this direct-cast fixture.
                        }
                        if(useScriptedHelper)
                        {
                            if(RetailNpcStatSkillLevels.GetDirectLevel(caster,c.Skill) is null)
                            {
                                counterpartyEligible=false;excludedSourceVariant++;
                                trials.Add(new {trial,started=false,limitation="unverified-or-ambiguous-scripted-source-level"});break;
                            }
                            castLevel=Aion.GameServer.Ai.NpcSkillCasting.LevelOf(caster,c.Skill);
                            Assert.Equal(c.Level,castLevel);
                        }
                        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(caster,c.Skill,castLevel,player);
                        Assert.NotNull(skill);
                        var properties=skill.GetSkillTemplate().GetProperties();
                        if(c.Source.SelfOnlyPrimary&&properties?.GetFirstTarget()==Aion.GameServer.SkillEngine.Properties.FirstTargetAttribute.ME
                            &&properties.GetTargetType()==Aion.GameServer.SkillEngine.Properties.TargetRangeAttribute.ONLYONE)
                        {
                            // Both source and host select the caster only. A
                            // Player damage fixture cannot prove NPC self-damage.
                            // Keep that distinct context visible, not256 false
                            // retries or an invented enemy-target substitution.
                            counterpartyEligible=false;excludedSelf++;
                            trials.Add(new {trial,started=false,limitation="source-and-host-self-only-primary"});
                            break;
                        }
                        if(skill.GetSkillTemplate().GetProperties()?.GetTargetRelation()==Aion.GameServer.SkillEngine.Properties.TargetRelationAttribute.ENEMY
                            &&!caster.IsEnemy(player))
                        {
                            // A same-faction guard cannot prove enemy-cast damage
                            // against this Asmodian. Record the inapplicable
                            // counterparty; do not change factions or gear.
                            counterpartyEligible=false;excludedFriendly++;
                            trials.Add(new {trial,started=false,limitation="friendly-to-asmodian-target"});
                            break;
                        }
                        // Numeric fixtures supply explicit prerequisite states,
                        // just as they supply a close temporary caster. This is
                        // not a flight/travel test or a gameplay bypass.
                        var conditions=skill.GetSkillTemplate().GetStartconditions()?.GetConditions();
                        bool targetFlight=conditions?.OfType<Aion.GameServer.SkillEngine.Condition.TargetFlyingCondition>()
                            .Any(x=>x.Restriction==Aion.GameServer.SkillEngine.Model.FlyingRestriction.FLY)==true;
                        bool casterFlight=conditions?.OfType<Aion.GameServer.SkillEngine.Condition.SelfFlyingCondition>()
                            .Any(x=>x.Restriction==Aion.GameServer.SkillEngine.Model.FlyingRestriction.FLY)==true
                            ||conditions?.OfType<Aion.GameServer.SkillEngine.Condition.OnFlyCondition>().Any()==true;
                        bool wasFlying=player.IsInState(CreatureState.FLYING);
                        bool hadFlyFlag=player.IsInFlyState(FlyState.FLYING);
                        if(targetFlight)
                        {
                            player.SetState(CreatureState.FLYING);player.SetFlyState(FlyState.FLYING);
                            Assert.True(player.IsFlying());
                        }
                        if(casterFlight)caster.SetState(CreatureState.FLYING);
                        // Run the real successful Stumble Start/End hooks.
                        // Admission/resistance of the crowd-control skill is a
                        // separate compatibility owner of this numeric fixture.
                        if(c.State=="stumble"||c.State=="ended"&&c.Source.StumbleOnly)
                        {
                            Assert.True(c.Source.StumbleOnly);
                            fixtureStumble=new(caster,player,new Aion.GameServer.SkillEngine.Model.SkillTemplate {skillId=910003},1);
                            fixtureStumble.SetTargetLoc(player.GetX(),player.GetY(),player.GetZ());
                            stumbleTemplate.StartEffect(fixtureStumble);
                            Assert.True(player.GetEffectController().RetailNpcStumbleActive);
                            if(c.State=="ended"){stumbleTemplate.EndEffect(fixtureStumble);fixtureStumble=null;}
                        }
                        else if(c.State is "poison" or "bleed" or "ended")
                        {
                            Assert.NotEqual(0,c.Source.AdditionalStatusMask);
                            periodicState=c.State=="poison"||c.State=="ended"&&c.Source.AdditionalStatusMask==1
                                ?Aion.GameServer.SkillEngine.Effects.AbnormalState.POISON:Aion.GameServer.SkillEngine.Effects.AbnormalState.BLEED;
                            periodicTemplate=periodicState==Aion.GameServer.SkillEngine.Effects.AbnormalState.POISON
                                ?new Aion.GameServer.SkillEngine.Effects.PoisonEffect():new Aion.GameServer.SkillEngine.Effects.BleedEffect();
                            fixturePeriodic=new(caster,player,new Aion.GameServer.SkillEngine.Model.SkillTemplate {skillId=910004},1);
                            // Real existing state Start/End hooks, with no tick
                            // scheduled by this explicit prerequisite fixture.
                            periodicTemplate.StartEffect(fixturePeriodic,periodicState);
                            if(c.State=="ended"){periodicTemplate.EndEffect(fixturePeriodic,periodicState);fixturePeriodic=null;}
                        }
                        Assert.Equal(c.State=="stumble",player.GetEffectController().RetailNpcStumbleActive);
                        Assert.Equal(c.State=="poison"?1:c.State=="bleed"?2:0,player.GetEffectController().RetailNpcPoisonBleedMask);
                        Aion.GameServer.SkillEngine.Model.Effect StartStatus(int id,Creature receiver,Type kind)
                        {
                            var sourceSkill=DataManager.SKILL_DATA.GetSkillTemplate(id);Assert.NotNull(sourceSkill);
                            var sourceTemplate=Assert.Single(sourceSkill.GetEffects()!.GetEffects(),kind.IsInstanceOfType);
                            var supplied=new Aion.GameServer.SkillEngine.Model.Effect(player,receiver,sourceSkill,1);
                            supplied.SetTargetLoc(receiver.GetX(),receiver.GetY(),receiver.GetZ());
                            supplied.AddSuccessEffect(sourceTemplate);sourceTemplate.StartEffect(supplied);statusFixtures.Add(supplied);
                            return supplied;
                        }
                        if(c.DefenseState=="all")
                        {
                            StartStatus(1923,player,typeof(Aion.GameServer.SkillEngine.Effects.AlwaysDodgeEffect));
                            StartStatus(8710,player,typeof(Aion.GameServer.SkillEngine.Effects.AlwaysBlockEffect));
                            StartStatus(1759,player,typeof(Aion.GameServer.SkillEngine.Effects.AlwaysParryEffect));
                        }
                        if(c.DefenseState=="block-many")StartStatus(2974,player,typeof(Aion.GameServer.SkillEngine.Effects.AlwaysBlockEffect));
                        if(c.DefenseState=="dodge-ended-older")
                        {
                            var older=StartStatus(1923,player,typeof(Aion.GameServer.SkillEngine.Effects.AlwaysDodgeEffect));
                            StartStatus(3195,player,typeof(Aion.GameServer.SkillEngine.Effects.AlwaysDodgeEffect));older.EndEffect();
                        }
                        if(sourceBlindFixture)StartStatus(4135,caster,typeof(Aion.GameServer.SkillEngine.Effects.BlindEffect));
                        object defensesBefore=DefenseSnapshot();
                        var blindBefore=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatPhysicalStatuses.For(caster).Blind;
                        // The public scripted helper intentionally returns void.
                        // Keep its start status unknown and require real damage/HP
                        // evidence; never label the request itself a started cast.
                        bool? started;
                        if(useScriptedHelper)
                        {
                            Aion.GameServer.Ai.NpcSkillCasting.UseOnNow(caster,player,c.Skill);
                            started=null;
                        }
                        else started=skill.UseSkill();
                        var events=Array.Empty<string>();
                        for(int poll=0;started!=false&&poll<150;poll++)
                        {
                            await session.AdvanceAsync(TimeSpan.FromMilliseconds(100),token);await session.SynchronizeAsync(token);
                            events=fixture.LogCapture.Entries.Skip(start).Select(e=>e.Message).ToArray();
                            if(events.Any(e=>e.StartsWith("RETAIL_SKYTEMPLE_HP ")&&e.Contains("source="+caster.GetObjectId()+" ")&&e.Contains("skill="+c.Skill+" ")))break;
                            // Cast end and projectile impact have distinct owners.
                            // Keep the caster alive through the impact window.
                        }
                        var primary=events.Where(e=>e.StartsWith("RETAIL_STAT_SKILL ")&&e.Contains("npc="+c.Profile.NpcId+" ")&&e.Contains("skill="+c.Skill+" ")).ToArray();
                        var inputs=events.Where(e=>e.StartsWith("RETAIL_STAT_PRIMARY_INPUT ")).Select(e=>e["RETAIL_STAT_PRIMARY_INPUT ".Length..]).ToArray();
                        foreach(string input in inputs)
                        {
                            using var payload=JsonDocument.Parse(input);var t=payload.RootElement;
                            Assert.Equal(caster.GetObjectId(),t.GetProperty("source").GetInt32());
                            if(c.Source.StumbleOnly)
                            {
                                Assert.True(t.GetProperty("stumbleOnly").GetBoolean());
                                Assert.Equal(c.State=="stumble",t.GetProperty("stumbled").GetBoolean());
                                if(c.State=="stumble")Assert.InRange(t.GetProperty("additionalDraw").GetInt32(),1,100);
                                else Assert.Equal(JsonValueKind.Null,t.GetProperty("additionalDraw").ValueKind);
                            }
                        }
                        var losses=events.Where(e=>e.StartsWith("RETAIL_SKYTEMPLE_HP ")&&e.Contains("source="+caster.GetObjectId()+" ")&&e.Contains("skill="+c.Skill+" ")).ToArray();
                        foreach(var e in losses)
                        {
                            var f=e.Split(' ',StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(x=>x.Split('=',2)).Where(x=>x.Length==2).ToDictionary(x=>x[0],x=>x[1]);
                            Assert.Equal(Math.Min(int.Parse(f["before"]),Math.Max(0,int.Parse(f["requested"]))),int.Parse(f["before"])-int.Parse(f["after"]));
                        }
                        committed=primary.Length>0&&losses.Length>0;
                        if(player.IsDead())deaths++;
                        trials.Add(new {trial,started,castLevel,before,after=player.GetLifeStats().GetCurrentHp(),primary,losses,
                            defenseState=c.DefenseState,sourceBlindFixture,defensesBefore,defensesAfter=DefenseSnapshot(),
                            sourceBlindBefore=new {active=blindBefore.Active,chance=blindBefore.Chance},
                            prechecks=events.Where(e=>e.StartsWith("RETAIL_STAT_PHYSICAL_PRECHECK ")).ToArray(),
                            inputs,targetState=c.State,sourceObjectId=caster.GetObjectId(),targetObjectId=player.GetObjectId(),world=caster.GetWorldId(),
                            enemyCounterparty=caster.IsEnemy(player),
                            fixtureTargetFlying=targetFlight,fixtureCasterFlying=casterFlight,
                            startConditions=conditions?.Select(x=>x.GetType().Name).ToArray(),
                            casterPosition=new {x=caster.GetX(),y=caster.GetY(),z=caster.GetZ()},
                            targetPosition=new {x=player.GetX(),y=player.GetY(),z=player.GetZ()},
                            targetHeading=player.GetHeading(),direction,
                            diagnostics=events.Where(e=>e.StartsWith("RETAIL_STAT_SKILL_COMPAT ")).ToArray()});
                        if(targetFlight&&!wasFlying&&!player.IsDead())player.UnsetState(CreatureState.FLYING);
                        if(targetFlight&&!hadFlyFlag)player.UnsetFlyState(FlyState.FLYING);
                        if(started==false||!committed&&events.Any(e=>e.StartsWith("RETAIL_STAT_SKILL_COMPAT ")
                            &&e.Contains("npc="+c.Profile.NpcId+" ")&&e.Contains("skill="+c.Skill+" ")
                            &&!e.Contains("reason=non-player-target;")))break;
                    }
                    finally
                    {
                        if(fixtureStumble is not null)stumbleTemplate.EndEffect(fixtureStumble);
                        if(fixturePeriodic is not null)periodicTemplate!.EndEffect(fixturePeriodic,periodicState);
                        foreach(var supplied in statusFixtures)supplied.EndEffect();
                        // The real requested skill may add linked subeffects
                        // with their own skill IDs. End this caster's effects
                        // through their normal owner before the next isolated
                        // numeric case; do not disable that server behavior.
                        foreach(var applied in player.GetEffectController().GetAbnormalEffects()
                            .Where(e=>ReferenceEquals(e.GetEffector(),caster)).ToArray())applied.EndEffect();
                        Assert.False(player.GetEffectController().RetailNpcStumbleActive);
                        Assert.Equal(0,player.GetEffectController().RetailNpcPoisonBleedMask);
                        caster.GetController().Delete();
                    }
                }
                var result=new {map=c.Map,npc=c.Profile.NpcId,skill=c.Skill,level=c.Level,sourceEntryIndex=c.Entry,
                    targetState=c.State,defenseState=c.DefenseState,sourceBlindFixture,
                    sourceProfileSha256=c.Profile.SourceRecordSha256,sourceSkillSha256=c.Source.Sha256,syntheticAnchor,
                    family=c.Source.Family.ToString(),selection,hostLevelUsed,levelDecisions,counterpartyEligible,committed,trials};
                results.Add(result);await progress.WriteLineAsync(JsonSerializer.Serialize(result));await progress.FlushAsync(token);
                if(counterpartyEligible&&!committed)blocked.Add($"{c.Map}/{c.Profile.NpcId}/{c.Skill}/{c.Level}/{c.State}/{c.DefenseState}/blind={sourceBlindFixture}");
                // Retain every independent result before enforcing the whole
                // gate. A legitimate host condition on one skill must not hide
                // which later numeric paths work or need their own fixture.
                Console.WriteLine($"Retail skill fixtures {results.Count}/{stateCases.Count}: {c.Profile.NpcId}/{c.Skill}/{c.Level}/{c.State}");
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output,"skill-acceptance.json"),JsonSerializer.Serialize(new
            {selection,equipmentFixture,targetStates,defenseStates,sourceBlindFixture,selected=stateCases.Count,assessed=results.Count,tested=stateCases.Count-excludedFriendly-excludedSelf-excludedAbsentHost-excludedSourceVariant,
                excludedFriendly,excludedSelf,excludedAbsentHost,excludedSourceVariant,deaths,blocked,results,
                scope="Explicit source-row inventory; source mode forces source level, existing-host uses actual unchanged-membership list entry, scripted-helper invokes public UseOnNow/LevelOf with the original fallback. Scripted helper has no returned start status; real calculation/HP events are required. Friendly, self-only, ambiguous and absent-entry contexts stay visible. No AI chooser or natural balance acceptance"}),token);
        Assert.Empty(blocked);
        policy.AssertClean();
    }
}
