using System.Text.Json;
using System.Text.RegularExpressions;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;
using Aion.GameServer.World;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    // Full production bootstrap/AI/packets in a labelled empty instance, one
    // ordinary SpawnEngine NPC at a time. This isolates movement from pack combat;
    // the shipped three-room population is tested separately, without edits.
    private async Task RunThreeKeysMovementChecksAsync(SimulationL0Session session,NaturalJourneyRuntime original,
        int[] selected,string output,CancellationToken token)
    {
        const int map=320050000;
        var player=fixture.World.GetPlayer(session.CharacterId);
        var controlled=WorldMapInstanceFactory.CreateWorldMapInstance(fixture.World.GetWorldMap(map),0,
            instance=>new GeneralInstanceHandler(instance),1);
        int instanceId=controlled.GetInstanceId();
        BotPosition At(VisibleObject o)=>new(o.GetX(),o.GetY(),o.GetZ(),o.GetHeading());
        BotNavigationGeometry Geometry()=>BotNavigationGeometry.ForServerWorld(instanceId,Race.ASMODIANS);
        var runtime=original with {CreateGeometry=Geometry};
        var journey=new NaturalIshalgenJourney(session,runtime,new NaturalJourneyOptions(ClassLine:NaturalClassLine.WarriorGladiator));
        var results=new List<object>();var failures=new List<int>();
        using var writer=new StreamWriter(new FileStream(Path.Combine(output,"movement-actors.jsonl"),FileMode.CreateNew,
            FileAccess.Write,FileShare.Read)){AutoFlush=true};
        var fixtures=new[]{
            (new BotPosition(391.25f,406.25f,374.58f,0),new BotPosition(391.25f,398.25f,374.58f,0)),
            (new BotPosition(368.25f,400.25f,375.05f,0),new BotPosition(362.25f,406.25f,376.32f,0)),
            (new BotPosition(386.75f,448.75f,375.05f,0),new BotPosition(386.75f,440.75f,374.58f,0))};
        foreach(int id in selected)
        {
            Npc? npc=null;string phase="setup";string? error=null;
            bool obstacle=false,chased=false,returned=false,regenerated=false,deadLifeRetired=false;
            bool fleeChargeUsed=false;
            bool returnedState=false;float walked=0;long lastSample=-1;BotPosition previous=default;
            int logStart=fixture.LogCapture.Entries.Count;long started=fixture.Clock.NowMillis;
            try
            {
                // Between cases only: normal character, no HP/equipment/skill edits.
                await TeleportForSetupAsync(session,player,map,461.581f,438.242f,374.965f,token,instanceId);
                await session.SynchronizeAsync(token);await journey.RunObservedRestAsync(token);
                // Keep the learned escape available without changing its real
                // four-minute cooldown. No NPC exists during this ordinary wait.
                if(session.Api.World.Skills.ContainsKey(3124)&&session.Api.Timing.TimeUntilCast(3124) is { } chargeWait&&chargeWait>TimeSpan.Zero)
                {await session.AdvanceAsync(chargeWait+TimeSpan.FromMilliseconds(100),token);await session.SynchronizeAsync(token);}
                Assert.True(SkyTempleNpcProfileCatalog.TryGet(map,id,out var profile));
                var assets=SkyTempleNpcNavigationAssets.Configured;
                var body=new SkyTempleNpcBody(profile.Bounds.Front,profile.Bounds.Side,profile.Bounds.Upper,
                    profile.Bounds.HorizontalRadius,profile.SourceRecordSha256,profile.NpcId);
                var scene=assets.Scene(instanceId,new[]{(15,true),(16,true),(17,true)});
                var planner=new SkyTempleNpcRoutePlanner(assets,scene,body,"controlled-"+id);
                var choice=fixtures.FirstOrDefault(pair=>
                    !planner.Visible(new(pair.Item1.X,pair.Item1.Y,pair.Item1.Z),body.Upper,
                        new(pair.Item2.X,pair.Item2.Y,pair.Item2.Z),player.GetObjectTemplate().GetBoundRadius().GetUpper())&&
                    planner.Find(new(pair.Item1.X,pair.Item1.Y,pair.Item1.Z),new(pair.Item2.X,pair.Item2.Y,pair.Item2.Z),"fixture-check").Success&&
                    Geometry().FindLocalPath(map,pair.Item1,pair.Item2).Count>1);
                Assert.NotEqual(default,choice);
                BotPosition home=choice.Item1,cover=choice.Item2;
                npc=Assert.IsAssignableFrom<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(
                    Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(map,id,home.X,home.Y,home.Z,home.Heading),instanceId));
                scene=assets.CurrentScene(npc);planner=new SkyTempleNpcRoutePlanner(assets,scene,body,"controlled-"+id);
                Assert.False(planner.Visible(SkyTempleNpcNavigation.Position(npc),body.Upper,
                    new(cover.X,cover.Y,cover.Z),player.GetObjectTemplate().GetBoundRadius().GetUpper()));
                Assert.Equal(profile.MaxHp,npc.GetLifeStats().GetMaxHp());Assert.Equal(profile.MaxHp,npc.GetLifeStats().GetCurrentHp());
                Assert.Single(controlled.GetNpcs());
                previous=At(npc);
                session.AfterSynchronize=()=>
                {
                    if(lastSample==fixture.Clock.NowMillis)return;lastSample=fixture.Clock.NowMillis;
                    var position=At(npc);walked+=NaturalFlightPolicy.Distance(previous,position);previous=position;
                    var nav=npc.GetMoveController().RetailNavigation;
                    returnedState|=npc.GetAi().GetState()==AIState.RETURNING;
                    writer.WriteLine(JsonSerializer.Serialize(new{t=lastSample,npc=id,obj=npc.GetObjectId(),phase,position,
                        hp=npc.GetLifeStats().GetCurrentHp(),maxHp=npc.GetLifeStats().GetMaxHp(),ai=npc.GetAi().GetState().ToString(),
                        playerLevel=player.GetLevel(),playerMaxHp=player.GetLifeStats().GetMaxHp(),
                        nav.LastFailure,nav.RemainingWaypoints,nav.IsReturning,player=At(player),playerHp=player.GetLifeStats().GetCurrentHp(),
                        clientPlayer=session.CurrentPosition,chase=SkyTempleNpcSkillState.GetChaseSnapshot(npc),
                        arrival=npc.GetMoveController().RetailArrival(player).ToString()}));
                };
                var stand=Geometry().GroundAround(map,home,[2f,3f]).First(p=>Geometry().HasLineOfSight(map,p,home));
                await TeleportForSetupAsync(session,player,map,stand.X,stand.Y,stand.Z,token,instanceId);
                await session.SynchronizeAsync(token);session.BeginStep("movement-"+id,"one-hit-obstacle-chase-return-regeneration");
                phase="tag";
                bool selfHealer=profile.Skills.Any(s=>s.Id48 is 16588 or 16836);
                int requiredDeficit=selfHealer?1:profile.HpRegen+1;
                for(int hit=0;hit<12&&profile.MaxHp-npc.GetLifeStats().GetCurrentHp()<requiredDeficit&&!npc.IsDead();hit++)
                {
                    await session.SendPacketAsync(session.Api.Target(npc.GetObjectId()),token);
                    await session.SendPacketAsync(session.Api.Attack(npc.GetObjectId(),player.GetGameStats().GetAttackSpeed().GetCurrent()),token);
                    await session.AdvanceAsync(TimeSpan.FromMilliseconds(player.GetGameStats().GetAttackSpeed().GetCurrent()+100),token);
                    await session.SynchronizeAsync(token);
                }
                Assert.False(npc.IsDead());Assert.InRange(npc.GetLifeStats().GetCurrentHp(),1,profile.MaxHp-1);
                phase="obstacle";
                await Walk(cover);
                var arrival=npc.GetMoveController().RetailArrival(player);
                Assert.False(arrival==SkyTempleArrival.Arrived&&!planner.Visible(SkyTempleNpcNavigation.Position(npc),body.Upper,
                    SkyTempleNpcNavigation.Position(player),player.GetObjectTemplate().GetBoundRadius().GetUpper()),"Range alone must not declare arrival through cover.");
                for(int wait=0;wait<80&&npc.GetMoveController().RetailArrival(player)!=SkyTempleArrival.Arrived;wait++)await Advance(200);
                obstacle=npc.GetMoveController().RetailArrival(player)==SkyTempleArrival.Arrived;
                Assert.True(obstacle,"NPC did not approach checked cover with both range and LOS.");
                phase="moving-target-chase";float beforeChase=walked;
                var escape=Geometry().GroundAround(map,At(player),[130f,110f,90f,70f],32)
                    .Where(p=>NaturalFlightPolicy.Distance(p,home)>60)
                    .OrderByDescending(p=>NaturalFlightPolicy.Distance(p,home))
                    .Select(p=>Geometry().FindJourneyPath(map,At(player),p)).FirstOrDefault(path=>path.Count>1);
                Assert.NotNull(escape);
                await Walk(escape![^1]);
                // Keep fleeing rather than standing at the first endpoint, where
                // a pursuer can legitimately catch up and renew its action lease.
                for(int leg=0;leg<4&&!returnedState;leg++)
                {
                    var actor=At(npc);var current=At(player);
                    var continuation=Geometry().GroundAround(map,current,[40f,30f,20f],32)
                        .Where(p=>NaturalFlightPolicy.Distance(p,actor)>NaturalFlightPolicy.Distance(current,actor)+5)
                        .OrderByDescending(p=>NaturalFlightPolicy.Distance(p,actor))
                        .Select(p=>Geometry().FindJourneyPath(map,current,p)).FirstOrDefault(path=>path.Count>1&&
                            NaturalFlightPolicy.Distance(path[^1],actor)>NaturalFlightPolicy.Distance(current,actor)+5);
                    if(continuation is null)break;
                    await Walk(continuation[^1]);
                }
                if(!returnedState)
                {
                    // Equal-speed ranged NPCs can legitimately keep casting on
                    // an unboosted runner. Use this Gladiator's actually learned
                    // Charge, not a GM buff or an NPC speed/leash adjustment.
                    Assert.True(session.Api.World.Skills.TryGetValue(3124,out var charge),"No learned ordinary escape skill.");
                    float before=session.Api.World.MovementSpeed??0;
                    await session.SendPacketAsync(session.Api.Cast(runtime.CreateSpellCast(session.Api.World,
                        session.CurrentPosition,3124,checked((byte)charge.Level),session.CharacterId)),token);
                    await Advance(500);
                    Assert.Contains(player.GetEffectController().GetAllEffects(),e=>e.GetSkillId()==3124);
                    Assert.True(session.Api.World.MovementSpeed>before);fleeChargeUsed=true;
                    for(int leg=0;leg<8&&!returnedState;leg++)
                    {
                        var actor=At(npc);var current=At(player);
                        var continuation=Geometry().GroundAround(map,current,[70f,50f,35f,20f],32)
                            .Where(p=>NaturalFlightPolicy.Distance(p,actor)>NaturalFlightPolicy.Distance(current,actor)+5)
                            .OrderByDescending(p=>NaturalFlightPolicy.Distance(p,actor))
                            .Select(p=>Geometry().FindJourneyPath(map,current,p)).FirstOrDefault(path=>path.Count>1&&
                                NaturalFlightPolicy.Distance(path[^1],actor)>NaturalFlightPolicy.Distance(current,actor)+5);
                        if(continuation is null)break;
                        await Walk(continuation[^1]);
                    }
                }
                chased=walked-beforeChase>3;
                Assert.True(chased,"NPC did not follow an actually moving out-of-range target.");
                phase="disengage";
                for(int wait=0;wait<250&&!returnedState;wait++)await Advance(200);
                Assert.True(returnedState,"No ordinary chase-to-return transition within the bounded observation.");
                phase="return";
                for(int wait=0;wait<300;wait++)
                {
                    if(npc.GetAi().GetState()!=AIState.RETURNING&&NaturalFlightPolicy.Distance(At(npc),home)<.11f){returned=true;break;}
                    await Advance(200);
                }
                Assert.True(returned,"The actual NPC did not finish returning to its own home.");
                phase="regeneration";
                for(int wait=0;wait<200&&npc.GetLifeStats().GetCurrentHp()<profile.MaxHp;wait++)await Advance(200);
                var regen=fixture.LogCapture.Entries.Skip(logStart).Where(l=>l.Message.StartsWith("RETAIL_SKYTEMPLE_REGEN ")&&
                    l.Message.Contains("object="+npc.GetObjectId()+" ")).ToArray();
                regenerated=npc.GetLifeStats().GetCurrentHp()==profile.MaxHp&&regen.Length>0;
                Assert.True(regenerated,"Full recovery must have an actual source regeneration event, not an arrival heal.");
                foreach(var entry in regen)
                {
                    var values=Regex.Matches(entry.Message,@"(\w+)=([^ ]+)").ToDictionary(m=>m.Groups[1].Value,m=>m.Groups[2].Value);
                    int before=int.Parse(values["before"]),after=int.Parse(values["after"]),requested=int.Parse(values["requested"]);
                    Assert.Equal(profile.HpRegen,requested);
                    Assert.Equal(Math.Min((long)profile.MaxHp,(long)before+requested),(long)after);
                }
                Assert.False(player.IsDead());
                phase="normal-kill-and-heartbeat-retirement";
                var killed=await journey.RunObservedCombatAsync(async _=>{await Walk(stand);return npc.GetObjectId();},token);
                Assert.True(killed.Killed);Assert.Equal(0,killed.Deaths);
                int deathLogStart=fixture.LogCapture.Entries.Count;
                for(int poll=0;poll<12;poll++)await Advance(2000);
                deadLifeRetired=npc.GetLifeStats().GetCurrentHp()==0&&!fixture.LogCapture.Entries.Skip(deathLogStart)
                    .Any(l=>l.Message.StartsWith("RETAIL_SKYTEMPLE_REGEN ")&&l.Message.Contains("object="+npc.GetObjectId()+" "));
                Assert.True(deadLifeRetired,"A dead/retired life must not receive a later heartbeat heal.");
            }
            catch(Exception ex)when(ex is not OperationCanceledException){error=ex.ToString();failures.Add(id);}
            finally
            {
                session.AfterSynchronize=null;
                var row=new{npc=id,obj=npc?.GetObjectId(),obstacle,chased,returned,regenerated,deadLifeRetired,fleeChargeUsed,walked,error,
                    elapsed=fixture.Clock.NowMillis-started,scope="Controlled full-bootstrap one-NPC instance; normal player packets; no HP/AI mutation; room travel separate"};
                results.Add(row);await File.WriteAllTextAsync(Path.Combine(output,"movement-"+id+".json"),JsonSerializer.Serialize(row),token);
                npc?.GetController().Delete();
            }
            async Task Advance(int milliseconds){await session.AdvanceAsync(TimeSpan.FromMilliseconds(milliseconds),token);await session.SynchronizeAsync(token);}
            async Task Walk(BotPosition goal)
            {
                var origin=At(player);var route=Geometry().FindJourneyPath(map,origin,goal);
                Assert.True(route.Count>1,"No checked player route to movement fixture goal.");
                float plannedSpeed=session.Api.World.MovementSpeed??throw new InvalidDataException("Player speed unobserved");
                var plan=new BotMover(session.Api.World,session.Api.Timing).CreateGroundPlan(route,origin,
                    plannedSpeed,TimeSpan.FromMilliseconds(200));
                foreach(var frame in plan.Frames)
                {
                    Assert.False(player.IsDead());
                    if(phase=="moving-target-chase"&&returnedState)
                    {
                        var stop=session.CurrentPosition;
                        await session.SendPacketAsync(GameClientPackets.Move(new(stop.X,stop.Y,stop.Z,stop.Heading,MovementMask.IMMEDIATE)),token);
                        await session.SynchronizeAsync(token);break;
                    }
                    if(session.Api.World.MovementSpeed is { } speed&&MathF.Abs(speed-plannedSpeed)>.001f)
                    {
                        var stop=session.CurrentPosition;
                        await session.SendPacketAsync(GameClientPackets.Move(new(stop.X,stop.Y,stop.Z,stop.Heading,MovementMask.IMMEDIATE)),token);
                        await session.SynchronizeAsync(token);await Walk(goal);return;
                    }
                    await session.ExecuteMovementAsync(plan with {Frames=[frame],Duration=frame.DelayBefore,Distance=0},token);
                    await session.SynchronizeAsync(token);
                }
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output,"movement-summary.json"),JsonSerializer.Serialize(new{selected,results,failures,
            all25AcceptanceComplete=false,next="Audit gates and run unmodified three-room travel"}),token);
        Assert.Empty(failures);
    }
}
