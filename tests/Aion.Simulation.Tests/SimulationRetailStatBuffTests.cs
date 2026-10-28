using System.Security.Cryptography;
using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    /// <summary>Numeric StatUp component test using real existing skill entries
    /// and Effect controller registration/removal. Explicitly supplies effect
    /// success; does not claim natural AI choice, cast admission or probability.</summary>
    [SkippableFact]
    public async Task RetailStatisticsBuffsApplyAndRemoveSourceDefensesInSim()
    {
        Skip.IfNot(fixture.IsAvailable,fixture.SkipReason);
        string? input=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_BUFF_CASES");
        Skip.If(string.IsNullOrEmpty(input),"Requires the frozen external StatUp comparison");
        var bytes=File.ReadAllBytes(input!);
        Assert.Equal(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_BUFF_CASES_SHA256"),Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var data=JsonDocument.Parse(bytes);
        var cases=data.RootElement.GetProperty("mismatches").EnumerateArray()
            .Concat(data.RootElement.GetProperty("level_changes").EnumerateArray()).Select(x=>x.Clone()).ToArray();
        Assert.Equal(293,cases.Length);Assert.True(RetailNpcStatCatalog.Enabled);
        string output=Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")!;
        string run=Environment.GetEnvironmentVariable("AION_SIM_RUN_ID")!;
        using var policy=new SimulationLogPolicy(run,"RETAIL-STAT-BUFF",fixture.Clock,allowlistPath,
            new SimulationLogPolicyOptions {FailOnWarnings=false},fixture.LogCapture,fixture.LoggerFactory,evidence:evidence);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(30));var token=timeout.Token;
        await using var session=new SimulationL0Session(fixture,policy,"b01",1,"Farmer",Race.ASMODIANS)
            {IdentityClassLine=NaturalClassLine.MageSorcerer};
        var dashboard=new LiveBotDashboardState();
        await using var monitor=new LiveBotDashboardHost(run,["RETAIL-STAT-BUFF"],dashboard,
            int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT")??"17880"));
        session.Dashboard=dashboard;Console.WriteLine("Retail StatUp SIM monitor: "+monitor.Url);
        using var logs=new ThreeKeysRuntimeLogProvider(Path.Combine(output,"buff-events.jsonl"),()=>fixture.Clock.NowMillis);
        fixture.LoggerFactory.AddProvider(logs);
        await session.LoginCharacterListAsync(token);session.SelectCharacter(104728,"Farmer");
        await session.EnterWorldAsync(token);await session.SynchronizeAsync(token);
        var player=fixture.World.GetPlayer(session.CharacterId);Assert.False(player.IsStaff());
        var rows=cases.Select(c=>(Case:c,Member:RetailNpcStatCatalog.Members.Where(m=>m.MapId!=0&&m.Profile.NpcId==c.GetProperty("npc").GetInt32())
            .OrderBy(m=>m.MapId).FirstOrDefault())).ToArray();
        Assert.All(rows,x=>Assert.NotNull(x.Member));
        var tracked=new[]{StatEnum.PHYSICAL_DEFENSE,StatEnum.MAGICAL_DEFEND,StatEnum.MAGICAL_RESIST,
            StatEnum.EVASION,StatEnum.PARRY,StatEnum.BLOCK}.Concat(RetailNpcStatBuffs.Elements).ToArray();
        await using var outcomes=new StreamWriter(Path.Combine(output,"buff-outcomes.jsonl"));
        int checkedRows=0;
        foreach(var group in rows.GroupBy(x=>x.Member!.MapId))
        {
            var map=fixture.World.GetWorldMap(group.Key);var instance=map.GetMainWorldMapInstance();
            foreach(var old in instance.GetNpcs().ToArray())old.GetController().Delete();
            var spot=DataManager.SPAWNS_DATA.GetSpawnsByWorldId(group.Key).SelectMany(g=>g.GetSpawnTemplates()).FirstOrDefault()
                ??Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(group.Key,group.First().Member!.Profile.NpcId,
                    Math.Max(64,map.GetWorldSize()/2f),Math.Max(64,map.GetWorldSize()/2f),Math.Max(map.GetWaterLevel(),map.GetDeathLevel())+100,0);
            await TeleportForSetupAsync(session,player,group.Key,spot.GetX()+.5f,spot.GetY(),spot.GetZ(),token);
            await session.SynchronizeAsync(token);
            if(player.IsProtectionActive())player.GetController().StopProtectionActiveTask();
            foreach(var row in group)
            {
                token.ThrowIfCancellationRequested();var c=row.Case;var p=row.Member!.Profile;
                int id=c.GetProperty("skill").GetInt32(),position=c.GetProperty("effect").GetInt32(),level=c.GetProperty("source_level").GetInt32();
                var spawn=Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(group.Key,p.NpcId,spot.GetX(),spot.GetY(),spot.GetZ(),0,null,"retail_statistics_fixture");
                var npc=Assert.IsAssignableFrom<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn,instance.GetInstanceId()));
                try
                {
                    Assert.Same(p,npc.GetGameStats().StatisticsProfile);
                    var entry=Enumerable.Range(0,npc.GetSkillList().Size()).Select(i=>npc.GetSkillList().GetSkillOnPosition(i))
                        .FirstOrDefault(s=>s.GetSkillId()==id&&s.GetSkillLevel()==level);
                    Assert.NotNull(entry);Assert.True(RetailNpcStatCatalog.TryGetBuff(p,id,entry.GetSkillLevel(),out var source));
                    var template=DataManager.SKILL_DATA.GetSkillTemplate(id);var et=Assert.IsType<StatupEffect>(template.GetEffectTemplate(position));
                    var program=Assert.Single(source.Programs,x=>x.Position==position);Assert.True(program.Supported,program.UnsupportedReason);
                    var before=tracked.ToDictionary(s=>s,s=>npc.GetGameStats().GetStat(s,p.Values[RetailNpcStatDefenses.Field(s)!]).GetCurrent());
                    int hpBefore=npc.GetGameStats().GetMaxHp().GetCurrent();
                    Assert.All(tracked,s=>Assert.Empty(npc.GetGameStats().GetStatsSorted(s)));
                    var effect=new Effect(npc,npc,template,entry.GetSkillLevel());effect.AddSuccessEffect(et);effect.ApplyEffect();
                    Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
                    Assert.Contains(effect,npc.GetEffectController().GetAllEffects());
                    var observed=new List<object>();
                    foreach(var stat in tracked)
                    {
                        var sourceParts=c.GetProperty("source").EnumerateArray().Where(a=>a[0].GetString()==stat.ToString()).ToArray();
                        int percent=0,flat=0;
                        foreach(var part in sourceParts)
                            if(part[1].GetString()=="PERCENT")percent=unchecked(percent+part[2].GetInt32());else flat=unchecked(flat+part[2].GetInt32());
                        var seed=p.ScalarInputs.Fields[RetailNpcStatDefenses.Field(stat)!];var spec=seed.Specification;
                        long expected=Math.Clamp(seed.Value+seed.Value*percent/100+flat,spec.Minimum!.Value,spec.Maximum!.Value);
                        int actual=npc.GetGameStats().GetStat(stat,p.Values[spec.Field]).GetCurrent();
                        Assert.Equal(expected,actual);
                        var functions=npc.GetGameStats().GetStatsSorted(stat).ToArray();
                        var leaves=functions.Select(f=>f is StatFunctionProxy proxy?proxy.GetProxiedFunction():f).ToArray();
                        Assert.All(leaves,f=>Assert.True(f.GetType()==typeof(StatRateFunction)||f.GetType()==typeof(StatAddFunction)));
                        int registeredPercent=0,registeredFlat=0;
                        foreach(var f in leaves)
                            if(f.GetType()==typeof(StatRateFunction))registeredPercent=unchecked(registeredPercent+f.GetValue());
                            else registeredFlat=unchecked(registeredFlat+f.GetValue());
                        Assert.Equal(percent,registeredPercent);Assert.Equal(flat,registeredFlat);
                        observed.Add(new {stat=stat.ToString(),basis=seed.Value,percent,flat,registeredPercent,registeredFlat,
                            descriptors=functions.Length,minimum=spec.Minimum,maximum=spec.Maximum,expected,actual});
                    }
                    foreach(bool physical in new[]{true,false})
                    {
                        var name=physical?StatEnum.PHYSICAL_ATTACK:StatEnum.MAGICAL_ATTACK;
                        var expected=c.GetProperty("source").EnumerateArray().Where(a=>a[0].GetString()==name.ToString()).ToArray();
                        int percent=expected.Where(a=>a[1].GetString()=="PERCENT").Sum(a=>a[2].GetInt32());
                        int flat=expected.Where(a=>a[1].GetString()=="ADD").Sum(a=>a[2].GetInt32());
                        Assert.Equal(new(percent,flat),RetailNpcStatNormalInputs.CurrentModifiers(npc,physical));
                    }
                    var hpParts=c.GetProperty("source").EnumerateArray().Where(a=>a[0].GetString()=="MAXHP").ToArray();
                    int hpPercent=hpParts.Where(a=>a[1].GetString()=="PERCENT").Sum(a=>a[2].GetInt32());
                    int hpFlat=hpParts.Where(a=>a[1].GetString()=="ADD").Sum(a=>a[2].GetInt32());
                    long hpBasis=p.ScalarInputs.Fields["max_hp"].Value;
                    long hpExpected=Math.Clamp(hpBasis+hpBasis*hpPercent/100+hpFlat,1,2000000000);
                    int hpActual=npc.GetGameStats().GetMaxHp().GetCurrent();
                    // Shared Stat2 storage is binary32; preserve the measured
                    // representational margin, rather than claiming integer HP precision.
                    Assert.InRange(Math.Abs(hpExpected-hpActual),0,Math.Max(1,hpExpected/8388608+1));
                    effect.EndEffect();Assert.DoesNotContain(effect,npc.GetEffectController().GetAllEffects());
                    foreach(var stat in tracked)
                    {
                        Assert.Empty(npc.GetGameStats().GetStatsSorted(stat));
                        Assert.Equal(before[stat],npc.GetGameStats().GetStat(stat,p.Values[RetailNpcStatDefenses.Field(stat)!]).GetCurrent());
                    }
                    Assert.Equal(new(0,0),RetailNpcStatNormalInputs.CurrentModifiers(npc,true));
                    Assert.Equal(new(0,0),RetailNpcStatNormalInputs.CurrentModifiers(npc,false));
                    Assert.Empty(npc.GetGameStats().GetStatsSorted(StatEnum.MAXHP));
                    Assert.Equal(hpBefore,npc.GetGameStats().GetMaxHp().GetCurrent());
                    await outcomes.WriteLineAsync(JsonSerializer.Serialize(new {npc=p.NpcId,map=group.Key,skill=id,position,level,
                        source=source.Sha256,profile=p.SourceRecordSha256,observed,hpBasis,hpPercent,hpFlat,hpExpected,hpActual,removed=true}));await outcomes.FlushAsync(token);checkedRows++;
                }
                finally {npc.GetController().Delete();}
            }
        }
        Assert.Equal(cases.Length,checkedRows);
        await File.WriteAllTextAsync(Path.Combine(output,"buff-summary.json"),JsonSerializer.Serialize(new {checkedRows,
            scope="Numeric application/removal, source-selected existing entries; supplied success; no AI admission or natural encounter claim"}),token);
    }
}
