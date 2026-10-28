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
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    [SkippableFact]
    public async Task RetailStatisticsPeriodicSkillsUseExistingTimerAndFinalHpInSim()
    {
        Skip.IfNot(fixture.IsAvailable,fixture.SkipReason);
        Skip.If(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_PERIODIC_SIM")!="1","Requires private source-bound periodic SIM");
        Assert.True(RetailNpcStatCatalog.Enabled);
        string output=Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")??throw new InvalidOperationException("External evidence required");
        string run=Environment.GetEnvironmentVariable("AION_SIM_RUN_ID")??"retail-stat-periodic";
        using var policy=new SimulationLogPolicy(run,"RETAIL-STAT-PERIODIC",fixture.Clock,allowlistPath,
            new SimulationLogPolicyOptions {FailOnWarnings=false},fixture.LogCapture,fixture.LoggerFactory,evidence:evidence);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(40));var token=timeout.Token;
        await using var session=new SimulationL0Session(fixture,policy,"b01",1,"Asimgladiator",Race.ASMODIANS)
            {IdentityClassLine=NaturalClassLine.WarriorGladiator};
        var dashboard=new LiveBotDashboardState();
        await using var monitor=new LiveBotDashboardHost(run,["RETAIL-STAT-PERIODIC"],dashboard,
            int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT")??"17880"));
        session.Dashboard=dashboard;Console.WriteLine("Retail periodic SIM monitor: "+monitor.Url);
        using var log=new ThreeKeysRuntimeLogProvider(Path.Combine(output,"periodic-events.jsonl"),()=>fixture.Clock.NowMillis);
        fixture.LoggerFactory.AddProvider(log);
        await session.LoginCharacterListAsync(token);session.SelectCharacter(320000,"Asimgladiator");
        await session.EnterWorldAsync(token);await session.SynchronizeAsync(token);
        var player=fixture.World.GetPlayer(session.CharacterId);
        Assert.Equal(38,(int)player.GetLevel());Assert.False(player.IsStaff());Assert.False(player.IsInvulnerable());
        var maps=(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_MAPS")??"220010000,220030000").Split(',').Select(int.Parse).ToHashSet();
        var only=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_NPCS")?.Split(',').Select(int.Parse).ToHashSet();
        var families=(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_PERIODIC_FAMILIES")??"Poison,Bleed")
            .Split(',').ToHashSet(StringComparer.Ordinal);
        var cases=new List<(int Map,SkyTempleNpcProfile Profile,int Skill,int Level,RetailNpcStatPeriodicCombat.Part Part,string Hash)>();
        foreach(var m in RetailNpcStatCatalog.Members.Where(m=>maps.Contains(m.MapId)&&(only is null||only.Contains(m.Profile.NpcId))))
        {
            var groups=m.Profile.Source.Source.GetRawRootChildren("skills");if(groups.Length!=1)continue;
            var rows=Aion.GameServer.Dataholders.RetailNpc.RetailNpcSourceProfile.RawChildren(groups[0]).Where(x=>x[0].GetString()=="data").ToArray();
            foreach(var binding in m.Profile.Source.Source.SkillBindings.EnumerateArray())
            {
                if(binding.GetProperty("status").GetString()!="dual_join_agrees_static_only")continue;
                int id=binding.GetProperty("supported48_id").GetInt32();
                int level=SkyTempleNpcProfile.LeafInt(rows[binding.GetProperty("source_entry_index").GetInt32()],"skill_level");
                if(!RetailNpcStatCatalog.TryGetPeriodic(m.Profile,id,level,out var source))continue;
                foreach(var part in source.Parts.Where(p=>p.Supported&&families.Contains(p.Family.ToString())))
                    cases.Add((m.MapId,m.Profile,id,level,part,source.Sha256));
            }
        }
        Assert.NotEmpty(cases);var blocked=new List<string>();var results=new List<object>();
        await using var progress=new StreamWriter(Path.Combine(output,"periodic-outcomes.jsonl"));
        foreach(var group in cases.OrderBy(c=>c.Map).ThenBy(c=>c.Profile.NpcId).GroupBy(c=>c.Map))
        {
            var map=fixture.World.GetWorldMap(group.Key);Assert.NotNull(map);var instance=map.GetMainWorldMapInstance();
            foreach(var npc in instance.GetNpcs().ToArray())npc.GetController().Delete();
            var spot=group.SelectMany(c=>DataManager.SPAWNS_DATA.GetSpawnsForNpc(c.Map,c.Profile.NpcId)).SelectMany(s=>s.GetSpawnTemplates()).FirstOrDefault()
                ??DataManager.SPAWNS_DATA.GetSpawnsByWorldId(group.Key).SelectMany(s=>s.GetSpawnTemplates()).First();
            foreach(var c in group)
            {
                var trials=new List<object>();bool committed=false,eligible=true;string? exclusion=null;int consecutiveLethal=0;
                for(int trial=0;trial<256&&!committed;trial++)
                {
                    if(player.IsDead())
                    {
                        await session.AdvanceAsync(TimeSpan.FromSeconds(3),token);await session.SendPacketAsync(session.Api.Revive(),token);
                        await session.SynchronizeAsync(token);Assert.False(player.IsDead());
                    }
                    await TeleportForSetupAsync(session,player,c.Map,spot.GetX()+.5f,spot.GetY(),spot.GetZ(),token);
                    await session.SynchronizeAsync(token);
                    if(player.IsProtectionActive()){player.GetController().StopProtectionActiveTask();await session.SynchronizeAsync(token);}
                    // Restore through the ordinary client rest action and real
                    // regeneration. A revive restores only a fraction of maxHP;
                    // repeatedly casting at that wounded target cannot verify DoT.
                    if(player.GetLifeStats().GetCurrentHp()<player.GetLifeStats().GetMaxHp())
                    {
                        await session.SendPacketAsync(session.Api.Rest(sitting:true),token);await session.SynchronizeAsync(token);
                        for(int pulse=0;pulse<180&&player.GetLifeStats().GetCurrentHp()<player.GetLifeStats().GetMaxHp();pulse++)
                        {await session.AdvanceAsync(TimeSpan.FromSeconds(3),token);await session.SynchronizeAsync(token);}
                        await session.SendPacketAsync(session.Api.Rest(sitting:false),token);await session.SynchronizeAsync(token);
                        Assert.Equal(player.GetLifeStats().GetMaxHp(),player.GetLifeStats().GetCurrentHp());
                    }
                    var spawn=Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(c.Map,c.Profile.NpcId,player.GetX()-.5f,
                        player.GetY(),player.GetZ(),spot.GetHeading(),null,"retail_statistics_fixture");
                    var caster=Assert.IsAssignableFrom<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn,instance.GetInstanceId()));
                    bool wasFlying=player.IsInState(CreatureState.FLYING),hadFlyFlag=player.IsInFlyState(FlyState.FLYING);
                    Effect? bleedPrerequisite=null;var prerequisiteTemplate=new BleedEffect {checktime=0,Position=4};
                    try
                    {
                        // Controlled cast setup, matching the primary fixture:
                        // face the target for cone skills and leave spawn rest.
                        caster.UnsetState(CreatureState.RESTING);
                        caster.GetPosition().SetH(Aion.GameServer.Utils.PositionUtil.GetHeadingTowards(caster,player));
                        caster.SetTarget(player);var list=caster.GetSkillList();
                        if(!Enumerable.Range(0,list.Size()).Select(list.GetSkillOnPosition).Any(e=>e.GetSkillId()==c.Skill&&e.GetSkillLevel()==c.Level))
                        {eligible=false;exclusion="absent-existing-host-source-level-entry";break;}
                        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(caster,c.Skill,c.Level,player);Assert.NotNull(skill);
                        var properties=skill.GetSkillTemplate().GetProperties();
                        if(properties?.GetTargetRelation()==Aion.GameServer.SkillEngine.Properties.TargetRelationAttribute.ENEMY&&!caster.IsEnemy(player))
                        {eligible=false;exclusion="friendly-to-asmodian-target";break;}
                        if(properties?.GetFirstTarget()==Aion.GameServer.SkillEngine.Properties.FirstTargetAttribute.ME
                            &&properties.GetTargetType()==Aion.GameServer.SkillEngine.Properties.TargetRangeAttribute.ONLYONE)
                        {eligible=false;exclusion="self-only-host-target";break;}
                        var partTemplate=skill.GetSkillTemplate().GetEffectTemplate(c.Part.Position);
                        var conditions=(skill.GetSkillTemplate().GetStartconditions()?.GetConditions()??[])
                            .Concat(partTemplate?.EffectConditions?.GetConditions()??[]).ToArray();
                        bool targetFlying=conditions.OfType<TargetFlyingCondition>().Any(x=>x.Restriction==FlyingRestriction.FLY);
                        bool casterFlying=conditions.OfType<SelfFlyingCondition>().Any(x=>x.Restriction==FlyingRestriction.FLY)
                            ||conditions.OfType<OnFlyCondition>().Any();
                        bool needsBleed=conditions.OfType<AbnormalStateCondition>().Any(x=>x.Value==AbnormalState.BLEED);
                        if(targetFlying){player.SetState(CreatureState.FLYING);player.SetFlyState(FlyState.FLYING);}
                        if(casterFlying)caster.SetState(CreatureState.FLYING);
                        if(needsBleed&&!player.GetEffectController().IsAbnormalSet(AbnormalState.BLEED))
                        {
                            // Explicit numeric prerequisite, through the real
                            // base status hooks, with no second damage timer.
                            bleedPrerequisite=new(caster,player,new SkillTemplate {skillId=910007},1);
                            prerequisiteTemplate.StartEffect(bleedPrerequisite,AbnormalState.BLEED);
                        }
                        int before=player.GetLifeStats().GetCurrentHp(),maxHp=player.GetLifeStats().GetMaxHp();
                        int start=fixture.LogCapture.Entries.Count;bool started=skill.UseSkill();string[] ticks=[];
                        // A dependent periodic child can wait for a slower
                        // periodic preeffect. Observe the whole existing skill.
                        int longestInterval=skill.GetSkillTemplate().GetEffects()!.GetEffects().OfType<AbstractOverTimeEffect>()
                            .Select(e=>e.checktime).Append(c.Part.Interval).Max();
                        int maxPolls=Math.Max(200,(2*longestInterval+15000)/100);
                        for(int poll=0;started&&poll<maxPolls&&!player.IsDead();poll++)
                        {
                            await session.AdvanceAsync(TimeSpan.FromMilliseconds(100),token);await session.SynchronizeAsync(token);
                            ticks=fixture.LogCapture.Entries.Skip(start).Select(e=>e.Message).Where(e=>e.StartsWith("RETAIL_STAT_PERIODIC_INPUT "))
                                .Where(e=>{using var d=JsonDocument.Parse(e["RETAIL_STAT_PERIODIC_INPUT ".Length..]);var t=d.RootElement;
                                    return t.GetProperty("originalCaster").GetInt32()==caster.GetObjectId()&&t.GetProperty("skill").GetInt32()==c.Skill
                                        &&t.GetProperty("position").GetInt32()==c.Part.Position;}).ToArray();
                            if(ticks.Length>0)break;
                        }
                        committed=ticks.Length>0;
                        trials.Add(new {trial,started,source=caster.GetObjectId(),target=player.GetObjectId(),ticks,before,maxHp,
                            deadAfter=player.IsDead(),targetFlyingPrerequisite=targetFlying,casterFlyingPrerequisite=casterFlying,
                            casterHeading=caster.GetHeading(),canTarget=caster.CanTarget(player),
                            selectedTargets=skill.GetEffectedList().Select(t=>t.GetObjectId()).ToArray(),
                            bleedPrerequisite=needsBleed,
                            limitation=player.IsDead()&&!committed?"primary-killed-ordinary-target-before-periodic-observation":null,
                            diagnostics=fixture.LogCapture.Entries.Skip(start).Select(e=>e.Message)
                                .Where(e=>e.StartsWith("RETAIL_STAT_PERIODIC_COMPAT ")||e.Contains("properties failed")).ToArray()});
                        if(!started||trials.Count>0&&fixture.LogCapture.Entries.Skip(start).Any(e=>e.Message.StartsWith("RETAIL_STAT_PERIODIC_COMPAT ")))break;
                        consecutiveLethal=player.IsDead()&&!committed?consecutiveLethal+1:0;
                        if(consecutiveLethal>=3)break; // Retain as blocked, never weaken mob/gear or fabricate a positive tick.
                    }
                    finally
                    {
                        foreach(var e in player.GetEffectController().GetAbnormalEffects().Where(e=>e.GetSkillId()==c.Skill).ToArray())e.EndEffect();
                        if(bleedPrerequisite is not null)prerequisiteTemplate.EndEffect(bleedPrerequisite,AbnormalState.BLEED);
                        if(!wasFlying)player.UnsetState(CreatureState.FLYING);
                        if(!hadFlyFlag)player.UnsetFlyState(FlyState.FLYING);
                        caster.GetController().Delete();
                    }
                }
                if(eligible&&!committed)blocked.Add($"{c.Map}/{c.Profile.NpcId}/{c.Skill}/{c.Level}/{c.Part.Position}");
                var row=new {map=c.Map,npc=c.Profile.NpcId,skill=c.Skill,level=c.Level,position=c.Part.Position,
                    family=c.Part.Family.ToString(),interval=c.Part.Interval,skillHash=c.Hash,profileHash=c.Profile.SourceRecordSha256,
                    eligible,exclusion,committed,trials};results.Add(row);
                await progress.WriteLineAsync(JsonSerializer.Serialize(row));await progress.FlushAsync(token);
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output,"periodic-acceptance.json"),JsonSerializer.Serialize(new
            {selected=cases.Count,results,blocked,scope="Existing-host source-level casts, existing Effect timer and HP owner. Controlled unchanged ordinary target; no native selector, full effect-admission or natural difficulty claim."}),token);
        Assert.Empty(blocked);policy.AssertClean();
    }
}
