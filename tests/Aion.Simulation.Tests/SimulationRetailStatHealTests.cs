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
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Controllers;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    private sealed class RetailHealHpWitness(Npc npc,Func<long> clock,TextWriter output)
        :Aion.GameServer.Controllers.Observer.ActionObserver(Aion.GameServer.Controllers.Observer.ObserverType.HP_CHANGED)
    {
        private int previous=npc.GetLifeStats().GetCurrentHp();
        public override void HpChanged(int value)
        {
            int current=npc.GetLifeStats().GetCurrentHp();
            lock(output)
            {
                output.WriteLine(JsonSerializer.Serialize(new {clock=clock(),actor=npc.GetObjectId(),before=previous,after=value,callbackHp=value,currentHp=current}));
                output.Flush();previous=current;
            }
        }
    }

    [SkippableFact]
    public async Task RetailStatisticsInstantHealsUseSourceNumbersAndFinalHpInSim()
    {
        Skip.IfNot(fixture.IsAvailable,fixture.SkipReason);
        Skip.If(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HEAL_SIM")!="1","Requires private source-bound heal SIM");
        Assert.True(RetailNpcStatCatalog.Enabled);
        string output=Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")??throw new InvalidOperationException("External evidence required");
        string run=Environment.GetEnvironmentVariable("AION_SIM_RUN_ID")??"retail-stat-heal";
        using var policy=new SimulationLogPolicy(run,"RETAIL-STAT-HEAL",fixture.Clock,allowlistPath,
            new SimulationLogPolicyOptions {FailOnWarnings=false},fixture.LogCapture,fixture.LoggerFactory,evidence:evidence);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(40));var token=timeout.Token;
        await using var session=new SimulationL0Session(fixture,policy,"b01",1,"Asimgladiator",Race.ASMODIANS)
            {IdentityClassLine=NaturalClassLine.WarriorGladiator};
        var dashboard=new LiveBotDashboardState();
        await using var monitor=new LiveBotDashboardHost(run,["RETAIL-STAT-HEAL"],dashboard,
            int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT")??"17880"));
        session.Dashboard=dashboard;Console.WriteLine("Retail heal SIM monitor: "+monitor.Url);
        using var log=new ThreeKeysRuntimeLogProvider(Path.Combine(output,"heal-events.jsonl"),()=>fixture.Clock.NowMillis);
        fixture.LoggerFactory.AddProvider(log);
        using var hpOutput=new StreamWriter(Path.Combine(output,"heal-hp-observations.jsonl"));
        using var mapOutput=new StreamWriter(Path.Combine(output,"heal-map-setups.jsonl"));
        using var progress=new StreamWriter(Path.Combine(output,"heal-outcomes.jsonl"));
        await session.LoginCharacterListAsync(token);session.SelectCharacter(320000,"Asimgladiator");
        await session.EnterWorldAsync(token);await session.SynchronizeAsync(token);
        var player=fixture.World.GetPlayer(session.CharacterId);
        Assert.Equal(38,(int)player.GetLevel());Assert.False(player.IsStaff());Assert.False(player.IsInvulnerable());
        var maps=(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_MAPS")??"220010000,220030000").Split(',').Select(int.Parse).ToHashSet();
        var only=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_NPCS")?.Split(',').Select(int.Parse).ToHashSet();
        var cases=new List<(int Map,SkyTempleNpcProfile Profile,int Skill,int Level,RetailNpcStatHealCombat.Part Part)>();
        foreach(var m in RetailNpcStatCatalog.Members.Where(m=>maps.Contains(m.MapId)&&(only is null||only.Contains(m.Profile.NpcId))))
        {
            var groups=m.Profile.Source.Source.GetRawRootChildren("skills");if(groups.Length!=1)continue;
            var rows=Aion.GameServer.Dataholders.RetailNpc.RetailNpcSourceProfile.RawChildren(groups[0]).Where(x=>x[0].GetString()=="data").ToArray();
            foreach(var b in m.Profile.Source.Source.SkillBindings.EnumerateArray())
            {
                if(b.GetProperty("status").GetString()!="dual_join_agrees_static_only")continue;
                int id=b.GetProperty("supported48_id").GetInt32(),index=b.GetProperty("source_entry_index").GetInt32();
                int level=SkyTempleNpcProfile.LeafInt(rows[index],"skill_level");
                if(!RetailNpcStatCatalog.TryGetHeal(m.Profile,id,level,out var source))continue;
                foreach(var part in source.Parts.Where(p=>p.Supported))cases.Add((m.MapId,m.Profile,id,level,part));
            }
        }
        Assert.NotEmpty(cases);var results=new List<object>();var blocked=new List<string>();
        foreach(var group in cases.GroupBy(c=>c.Map))
        {
            var instance=fixture.World.GetWorldMap(group.Key).GetMainWorldMapInstance();
            foreach(var old in instance.GetNpcs().ToArray())old.GetController().Delete();
            var spot=group.SelectMany(c=>DataManager.SPAWNS_DATA.GetSpawnsForNpc(c.Map,c.Profile.NpcId)).SelectMany(s=>s.GetSpawnTemplates()).FirstOrDefault()
                ??DataManager.SPAWNS_DATA.GetSpawnsByWorldId(group.Key).SelectMany(s=>s.GetSpawnTemplates()).FirstOrDefault();
            float anchorX=spot?.GetX()??64f,anchorY=spot?.GetY()??64f,anchorZ=spot?.GetZ()??100f;
            byte heading=spot?.GetHeading()??0;
            await mapOutput.WriteLineAsync(JsonSerializer.Serialize(new {map=group.Key,anchorX,anchorY,anchorZ,
                anchorKind=spot is null?"coordinate-only-stats-fixture-no-placement-claim":"existing-static-spawn",
                playerTravel=spot is not null}));await mapOutput.FlushAsync(token);
            foreach(var c in group)
            {
                var trials=new List<object>();bool eligible=true,committed=true;string? exclusion=null;
                foreach(string wound in new[]{"three-missing","half-missing"})
                {
                    bool observed=false;
                    for(int trial=0;trial<16&&!observed;trial++)
                    {
                        // A conditional map may have no static spawns. Self-heal
                        // numbers do not require placement or player travel;
                        // run that explicitly labeled component fixture there.
                        if(spot is not null)
                        {
                            await TeleportForSetupAsync(session,player,c.Map,anchorX+.5f,anchorY,anchorZ,token);
                            await session.SynchronizeAsync(token);
                        }
                        var spawn=Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(c.Map,c.Profile.NpcId,
                            anchorX,anchorY,anchorZ,heading,null,"retail_statistics_fixture");
                        var caster=Assert.IsAssignableFrom<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn,instance.GetInstanceId()));
                        RetailHealHpWitness? witness=null;
                        try
                        {
                            caster.UnsetState(CreatureState.RESTING);caster.SetTarget(caster);
                            int maxHp=caster.GetLifeStats().GetMaxHp(),materializedHp=(int)(float)c.Profile.MaxHp;
                            // Same existing stats-only HP representation contract:
                            // permit only this exact f32 conversion, below 0.00001%.
                            Assert.InRange(Math.Abs((double)materializedHp-c.Profile.MaxHp)/Math.Max(1,c.Profile.MaxHp),0,0.0000001);
                            Assert.Equal(materializedHp,maxHp);
                            if(maxHp<2){eligible=false;exclusion="no-positive-self-wound-for-current-profile";break;}
                            var list=caster.GetSkillList();
                            if(!Enumerable.Range(0,list.Size()).Select(list.GetSkillOnPosition).Any(e=>e.GetSkillId()==c.Skill&&e.GetSkillLevel()==c.Level))
                            {eligible=false;exclusion="absent-existing-host-source-level-entry";break;}
                            int missing=wound=="three-missing"?Math.Min(3,maxHp-1):Math.Max(1,maxHp/2);
                            // Explicit controlled injury only. Profile/maxHP,
                            // player stats, gear and ordinary healing are unchanged.
                            caster.GetLifeStats().SetCurrentHp(maxHp-missing);
                            int before=caster.GetLifeStats().GetCurrentHp();
                            witness=new(caster,()=>fixture.Clock.NowMillis,hpOutput);caster.GetObserveController().AddObserver(witness);
                            var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(caster,c.Skill,c.Level,caster);Assert.NotNull(skill);
                            var properties=skill.GetSkillTemplate().GetProperties();
                            if(properties?.GetTargetSpecies()==Aion.GameServer.SkillEngine.Properties.TargetSpeciesAttribute.PC)
                            {eligible=false;exclusion="host-heal-targets-players-only";break;}
                            int start=fixture.LogCapture.Entries.Count;bool started=skill.UseSkill();string[] heals=[];
                            int polls=Math.Max(300,(skill.GetSkillTemplate().GetDuration()+15000)/100);
                            for(int poll=0;started&&poll<polls;poll++)
                            {
                                await session.AdvanceAsync(TimeSpan.FromMilliseconds(100),token);await session.SynchronizeAsync(token);
                                heals=fixture.LogCapture.Entries.Skip(start).Select(e=>e.Message).Where(e=>e.StartsWith("RETAIL_STAT_HEAL_INPUT "))
                                    .Where(e=>{using var d=JsonDocument.Parse(e["RETAIL_STAT_HEAL_INPUT ".Length..]);var t=d.RootElement;
                                        return t.GetProperty("source").GetInt32()==caster.GetObjectId()&&t.GetProperty("skill").GetInt32()==c.Skill
                                            &&t.GetProperty("position").GetInt32()==c.Part.Position;}).ToArray();
                                if(heals.Length>0)break;
                            }
                            observed=heals.Length>0;
                            var diagnostics=fixture.LogCapture.Entries.Skip(start).Select(e=>e.Message).Where(e=>e.StartsWith("RETAIL_STAT_HEAL_COMPAT ")||e.Contains("properties failed")).ToArray();
                            trials.Add(new {trial,wound,source=caster.GetObjectId(),before,maxHp,started,heals,after=caster.GetLifeStats().GetCurrentHp(),
                                casterSpawned=caster.IsSpawned(),firstTarget=skill.GetFirstTarget()?.GetObjectId(),
                                firstTargetSpawned=skill.GetFirstTarget()?.IsSpawned(),canTargetSelf=caster.CanTarget(caster),
                                selectedTargets=skill.GetEffectedList().Select(t=>t.GetObjectId()).ToArray(),diagnostics});
                            if(!started||diagnostics.Length>0)break;
                        }
                        finally
                        {
                            if(witness is not null)caster.GetObserveController().RemoveObserver(witness);
                            caster.GetController().Delete();
                        }
                    }
                    committed&=observed;
                    if(!eligible)break;
                }
                if(eligible&&!committed)blocked.Add($"{c.Map}/{c.Profile.NpcId}/{c.Skill}/{c.Level}/{c.Part.Position}");
                var row=new {map=c.Map,npc=c.Profile.NpcId,skill=c.Skill,level=c.Level,position=c.Part.Position,profileHash=c.Profile.SourceRecordSha256,
                    sourceMaxHp=c.Profile.MaxHp,materializedMaxHp=(int)(float)c.Profile.MaxHp,
                    eligible,exclusion,committed=eligible&&committed,trials};results.Add(row);
                await progress.WriteLineAsync(JsonSerializer.Serialize(row));await progress.FlushAsync(token);
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output,"heal-acceptance.json"),JsonSerializer.Serialize(new
            {selected=cases.Count,results,blocked,scope="Controlled self-heal casts at existing source-projected host levels. Fixture current-HP injury, unchanged source maximum/profile/player/gear. Independent existing HP observer; no natural difficulty or native selector/targeting claim."}),token);
        Assert.Empty(blocked);policy.AssertClean();
    }
}
