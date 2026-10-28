using System.Text.Json;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Retail;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    private async Task RunThreeKeysRoomsAsync(SimulationL0Session session,NaturalJourneyRuntime runtime,
        string output,CancellationToken token)
    {
        var rooms=NaturalAltgardContract.LoadLeg("vs").InstanceRooms!;
        var journey=new NaturalIshalgenJourney(session,runtime,new NaturalJourneyOptions(ClassLine:NaturalClassLine.WarriorGladiator));
        var player=fixture.World.GetPlayer(session.CharacterId);
        BotPosition At(VisibleObject o)=>new(o.GetX(),o.GetY(),o.GetZ(),o.GetHeading());
        BotNavigationGeometry Geometry()=>runtime.CreateGeometry();
        var fire=new NaturalFireRoomTravel(session,runtime,Geometry);
        var results=new List<object>();int successfulRooms=0;int? cohortInstance=null;var completedGuardians=new HashSet<int>();
        using var travel=new StreamWriter(new FileStream(Path.Combine(output,"room-positions.jsonl"),FileMode.CreateNew,
            FileAccess.Write,FileShare.Read)){AutoFlush=true};
        string phase="exterior-start";long last=-1;int fights=0;int guardianTemplate=0;
        var observedDead=new HashSet<int>();int deathCursor=0;
        session.AfterSynchronize=()=>
        {
            if(last==fixture.Clock.NowMillis)return;last=fixture.Clock.NowMillis;
            travel.WriteLine(JsonSerializer.Serialize(new{t=last,phase,map=player.GetWorldId(),instance=player.GetInstanceId(),
                position=At(player),client=session.CurrentPosition,hp=player.GetLifeStats().GetCurrentHp(),
                maxHp=player.GetLifeStats().GetMaxHp(),level=player.GetLevel(),fp=session.Api.World.CurrentFlightTime,dead=player.IsDead()}));
        };
        try
        {
            foreach(var room in rooms.Rooms)
            {
                guardianTemplate=room.SourceNpcId;
                long started=fixture.Clock.NowMillis;string? failure=null;bool entered=false,killed=false,left=false;
                try
                {
                    phase=room.Key+"-exterior-approach";session.BeginStep(phase,"normal-portal-and-room-travel-no-setup-teleports");
                    Assert.Equal(220020000,player.GetWorldId());
                    if(room.Key=="wind")await FlyToPortal(room.PortalNpcId);
                    if(room.Key=="fire")Assert.True(await fire.EnterAsync(Walk,Settle,token),"Ordinary recorded exterior flight failed.");
                    await Portal(room.PortalNpcId,rooms.MapId);entered=true;
                    Assert.Equal(rooms.MapId,player.GetWorldId());
                    cohortInstance??=player.GetInstanceId();Assert.Equal(cohortInstance.Value,player.GetInstanceId());
                    var cohort=player.GetWorldMapInstance().GetNpcs().Where(n=>SkyTempleNpcProfileCatalog.TryGet(n,out _)).ToArray();
                    // Re-entering the same instance does not resurrect a guardian
                    // killed in an earlier room after its ordinary corpse expiry.
                    // First entry still requires all25; later absence is allowed
                    // only for a previously confirmed guardian kill/exit below.
                    Assert.Equal(25,cohort.Select(n=>n.GetNpcId()).Union(completedGuardians).Count());
                    foreach(var n in cohort){Assert.True(SkyTempleNpcProfileCatalog.TryGet(n,out var p));Assert.Equal(p.MaxHp,n.GetLifeStats().GetMaxHp());}
                    phase=room.Key+"-room-travel";
                    session.BeginStep(phase,"normal-room-ground-jump-glide-and-combat");
                    if(room.Key=="fire")
                    {
                        bool Survived()=>player.GetWorldId()==rooms.MapId&&!player.IsDead();
                        Assert.True(await fire.ApproachDescentAsync(Walk,Survived,token),"Fire upper ground/jump/fall approach failed.");
                        Assert.True(await DescendFire(),"Fire ordinary FP-bounded descent failed.");
                        Assert.True(await fire.ClearBeforeBossAsync(Walk,Settle,async (id,_)=>await Kill(id),Survived,token),"Fire clearing corridor failed.");
                        var boss=Observed(room.SourceNpcId);Assert.NotNull(boss);
                        killed=await Kill(boss!.ObjectId);Assert.True(killed,"Fire boss was not killed normally.");
                        Assert.True(await fire.ApproachExitAsync(Walk,Settle,Survived,token),"Fire exit ground approach failed.");
                    }
                    else
                    {
                        var group=runtime.Data.SpawnsDh.GetSpawnsByWorldId(rooms.MapId).First(g=>g.GetNpcId()==room.SourceNpcId);
                        var spawn=group.GetSpawnTemplates().First();var goal=new BotPosition(spawn.GetX(),spawn.GetY(),spawn.GetZ(),0);
                        if(room.Key=="wind")
                        {
                            var monsters=runtime.Data.SpawnsDh.GetSpawnsByWorldId(rooms.MapId)
                                .Where(g=>g.GetNpcId()!=room.SourceNpcId&&runtime.IsAggressive(runtime.Data.NpcDataDh.GetNpcTemplate(g.GetNpcId())))
                                .SelectMany(g=>g.GetSpawnTemplates()).Select(s=>new BotPosition(s.GetX(),s.GetY(),s.GetZ(),0)).ToArray();
                            var glide=new NaturalFloorGlide(session,Geometry);
                            for(int attempt=0;attempt<12&&At(player).Z-goal.Z>12;attempt++)
                            {
                                bool glided=await glide.GlideTowardAsync(rooms.MapId,goal,Walk,async()=>{Assert.True(await Settle());},monsters,token);
                                Assert.False(player.IsDead());Assert.Equal(rooms.MapId,player.GetWorldId());
                                if(!glided)session.TraceDiagnostic("three-keys-glide-replan",new Dictionary<string,object?>
                                    {["attempt"]=attempt,["position"]=At(player),["goal"]=goal});
                            }
                            Assert.True(At(player).Z-goal.Z<=12,"Wind floor glide exhausted bounded ordinary replanning.");
                        }
                        // Static knowledge selects a vicinity, then actual observed
                        // packets select the living guardian identity and combat.
                        var approach=Geometry().GroundAround(rooms.MapId,goal,[12f,8f,5f]).OrderBy(p=>NaturalFlightPolicy.Distance(At(player),p)).First();
                        Assert.True(await Walk(approach));await Settle();
                        var guardian=Observed(room.SourceNpcId);Assert.NotNull(guardian);
                        ObserveDeaths();
                        killed=guardian!.IsCorpse||observedDead.Contains(guardian.ObjectId)||await Kill(guardian.ObjectId);Assert.True(killed,"Room guardian was not killed normally.");
                    }
                    phase=room.Key+"-exit";session.BeginStep(phase,"normal-exit-portal");await Portal(room.ExitNpcId,220020000);left=true;
                    Assert.False(player.IsDead());
                    completedGuardians.Add(room.SourceNpcId);
                    successfulRooms++;
                }
                catch(Exception ex)when(ex is not OperationCanceledException){failure=ex.ToString();throw;}
                finally
                {
                    var row=new{room=room.Key,entered,killed,left,failure,elapsed=fixture.Clock.NowMillis-started,fights,
                        endMap=player.GetWorldId(),end=At(player),scope="Shipped complete instance population; normal ground/jump/glide/flight/portal/combat; no inter-room setup teleports"};
                    results.Add(row);await File.WriteAllTextAsync(Path.Combine(output,"room-"+room.Key+".json"),JsonSerializer.Serialize(row),token);
                }
            }
        }
        finally
        {
            session.AfterSynchronize=null;
            await File.WriteAllTextAsync(Path.Combine(output,"rooms-summary.json"),JsonSerializer.Serialize(new{results,fights,
                complete=successfulRooms==3&&results.Count==3&&player.GetWorldId()==220020000&&!player.IsDead()}));
        }

        void ObserveDeaths()
        {
            foreach(var packet in session.PacketHistory.Skip(deathCursor))
            {
                if(packet.PacketType==typeof(SM_NPC_INFO))
                {
                    int id=packet.Get<int>("objectId");
                    if((packet.Get<ushort>("state")&15) is 7 or 8)observedDead.Add(id);else observedDead.Remove(id);
                }
                else if(BotAttackStatus.CarriesHp(packet)&&packet.Get<byte>("hpOrMp")==0)observedDead.Add(packet.Get<int>("objectId"));
            }
            deathCursor=session.PacketHistory.Count;
        }
        BotKnownObject? Observed(int template)
        {
            ObserveDeaths();
            return session.Api.World.Objects.Values.Where(n=>n.Kind==BotKnownObjectKind.Npc&&n.TemplateId==template)
                .OrderBy(n=>n.IsCorpse||observedDead.Contains(n.ObjectId)).ThenBy(n=>NaturalFlightPolicy.Distance(At(player),n.Position)).FirstOrDefault();
        }
        async Task<bool> Kill(int target)
        {
            var actual=player.GetWorldMapInstance().GetNpcs().FirstOrDefault(n=>n.GetObjectId()==target);
            var result=await journey.RunObservedCombatAsync(_=>Task.FromResult(target),token);fights++;
            Assert.Equal(0,result.Deaths);Assert.False(player.IsDead());
            // The public journey first rests/defends. That defense can kill this
            // very target before its requested combat begins; retain its actual
            // zero-HP packet instead of treating the later empty fight as failure.
            ObserveDeaths();bool killed=result.Killed||observedDead.Contains(target);
            if(killed&&actual is not null)Assert.True(actual.IsDead(),"Packet-driven combat claimed a kill of a still-living NPC.");
            return killed;
        }
        async Task<bool> DescendFire()
        {
            // The retained recording originated on a GM account. Its ordinary
            // clearance checks remain useful, but its full descent exceeds this
            // character's unboosted FP. Use the existing normal tier glider when
            // needed, landing/restoring on real intermediate floors.
            using var recording=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(runtime.RepoRoot,
                "parity-artifacts/e2e/natural-fire-room-route.json")));
            var points=recording.RootElement.GetProperty("descent").GetProperty("points").EnumerateArray().ToArray();
            float speed=session.Api.World.MovementSpeed??throw new InvalidDataException("Player speed not observed");
            double seconds=0;
            BotPosition Point(JsonElement p)=>new(p.GetProperty("x").GetSingle(),p.GetProperty("y").GetSingle(),p.GetProperty("z").GetSingle(),0);
            foreach(var pair in points.Zip(points.Skip(1)))seconds+=Math.Max(NaturalFlightPolicy.Distance(Point(pair.First),Point(pair.Second))/speed,
                (pair.Second.GetProperty("milliseconds").GetInt64()-pair.First.GetProperty("milliseconds").GetInt64())/1000d);
            int need=checked((int)Math.Ceiling(seconds)*2+5);
            if(need<=session.Api.World.MaxFlightTime)return await fire.DescendAsync(Walk,Settle,token);
            session.TraceDiagnostic("three-keys-fire-normal-fp-tier-descent",new Dictionary<string,object?>
                {["recordedNeed"]=need,["actualMaxFp"]=session.Api.World.MaxFlightTime,["speed"]=speed});
            var monsters=runtime.Data.SpawnsDh.GetSpawnsByWorldId(320050000)
                .Where(g=>runtime.IsAggressive(runtime.Data.NpcDataDh.GetNpcTemplate(g.GetNpcId())))
                .SelectMany(g=>g.GetSpawnTemplates()).Select(s=>new BotPosition(s.GetX(),s.GetY(),s.GetZ(),0)).ToArray();
            var glide=new NaturalFloorGlide(session,Geometry);
            for(int attempt=0;attempt<12&&At(player).Z-fire.DescentLanding.Z>8;attempt++)
            {
                bool glided=await glide.GlideTowardAsync(320050000,fire.DescentLanding,Walk,async()=>Assert.True(await Settle()),monsters,token);
                if(player.IsDead()||player.GetWorldId()!=320050000)return false;
                if(!glided)session.TraceDiagnostic("three-keys-glide-replan",new Dictionary<string,object?>
                    {["attempt"]=attempt,["position"]=At(player),["goal"]=fire.DescentLanding});
            }
            return fire.OnLowerFloor&&await Walk(fire.DescentLanding)&&await Settle();
        }
        Task<bool> Settle()=>Reconcile(true);
        Task<bool> Defend()=>Reconcile(false);
        async Task<bool> Reconcile(bool recover)
        {
            bool rested=false;
            for(int turn=0;turn<30;turn++)
            {
                await session.SynchronizeAsync(token);if(player.IsDead())return false;ObserveDeaths();
                var active=new HashSet<int>();
                NaturalCombatRetreatPolicy.ObserveEngagement(active,session.PacketHistory.TakeLast(400),session.CharacterId,
                    id=>session.Api.World.Objects.TryGetValue(id,out var n)&&n.TemplateId is int kind?runtime.Data.NpcDataDh.GetNpcTemplate(kind)?.GetL10n():null,
                    runtime.IsHostileSkill);
                var attacker=session.Api.World.Objects.Values.Where(n=>active.Contains(n.ObjectId)&&n.Kind==BotKnownObjectKind.Npc&&!n.IsCorpse&&!observedDead.Contains(n.ObjectId)&&
                    NaturalFlightPolicy.Distance(At(player),n.Position)<30).OrderBy(n=>NaturalFlightPolicy.Distance(At(player),n.Position)).FirstOrDefault();
                if(attacker is not null){rested=false;if(!await Kill(attacker.ObjectId))return false;continue;}
                if(!recover||rested)return !player.IsDead();
                await journey.RunObservedRestAsync(token);rested=true;
            }
            return false;
        }
        async Task<bool> Walk(BotPosition goal)
        {
            for(int replan=0;replan<64;replan++)
            {
                if(player.IsDead())return false;
                var from=At(player);float gap=NaturalFlightPolicy.Distance(from,goal);if(gap<.25f)return true;
                // Recorded Fire ledges contain closely spaced ordered points.
                // Skipping a one-metre point can strand the next ground edge.
                var hazards=Hazards(goal);
                var edge=gap<=5?Geometry().TraceEdge(player.GetWorldId(),from,goal):null;
                IReadOnlyList<BotPosition> route=hazards.Count>0?Geometry().FindJourneyPathAvoiding(player.GetWorldId(),from,goal,hazards):[];
                if(route.Count<2)route=edge is not null?new[]{from}.Concat(edge).ToArray():
                    hazards.Count>0?Geometry().FindFightThroughPath(player.GetWorldId(),from,goal,hazards):
                    Geometry().FindJourneyPath(player.GetWorldId(),from,goal);
                if(route.Count<2)return false;
                ObserveDeaths();
                var monsters=session.Api.World.Objects.Values.Where(n=>n.Kind==BotKnownObjectKind.Npc&&!n.IsCorpse&&
                    !observedDead.Contains(n.ObjectId)&&n.TemplateId is int id&&id!=guardianTemplate&&
                    runtime.IsAggressive(runtime.Data.NpcDataDh.GetNpcTemplate(id)))
                    .Select(n=>new NaturalObservedMonster(new NaturalNavigationObject(n.ObjectId,n.TemplateId!.Value,n.Position),
                        runtime.AggroRadius(runtime.Data.NpcDataDh.GetNpcTemplate(n.TemplateId!.Value)))).ToArray();
                // A fight-through route assigns hostile circles a COST. It is
                // not permission to walk through them all and pull the whole pack.
                var blocker=NaturalFightThrough.SelectNext(from,route,monsters);
                if(blocker is not null)
                {
                    session.TraceDiagnostic("three-keys-staged-pull",new Dictionary<string,object?>
                        {["target"]=blocker.Monster.Npc.ObjectId,["position"]=from,["staging"]=blocker.FiringPosition});
                    if(blocker.Staging.Count>1)
                    {
                        bool displaced=false;
                        var staging=new BotMover(session.Api.World,session.Api.Timing).CreateGroundPlan(blocker.Staging,from,
                            session.Api.World.MovementSpeed??throw new InvalidDataException("Player speed not observed"),TimeSpan.FromMilliseconds(200));
                        foreach(var chunk in staging.Frames.Chunk(5))
                        {
                            await GroundPiece(chunk);var before=At(player);if(!await Defend())return false;
                            if(NaturalFlightPolicy.Distance(before,At(player))>1){displaced=true;break;}
                        }
                        if(displaced)continue;
                    }
                    ObserveDeaths();
                    if(!observedDead.Contains(blocker.Monster.Npc.ObjectId)&&!await Kill(blocker.Monster.Npc.ObjectId))return false;
                    continue;
                }
                var plan=new BotMover(session.Api.World,session.Api.Timing).CreateGroundPlan(route,from,
                    session.Api.World.MovementSpeed??throw new InvalidDataException("Player movement speed not observed"),TimeSpan.FromMilliseconds(200));
                bool movedByDefense=false;
                foreach(var chunk in plan.Frames.Chunk(5))
                {
                    await GroundPiece(chunk);var beforeDefense=At(player);
                    if(!await Defend())return false;
                    if(NaturalFlightPolicy.Distance(At(player),beforeDefense)>1){movedByDefense=true;break;}
                }
                if(!movedByDefense&&NaturalFlightPolicy.Distance(At(player),goal)<.35f)return true;
            }
            return false;
        }
        async Task ApproachObject(int template)
        {
            var portal=player.GetWorldMapInstance().GetNpcs(template).Single();
            for(int attempt=0;attempt<8;attempt++)
            {
                Assert.True(await Defend());
                if(PositionUtil.IsInTalkRange(player,portal))return;
                // Long interaction approaches must also route around observed
                // packs; shortest interaction paths alone cut through them.
                if(NaturalFlightPolicy.Distance(At(player),At(portal))>6)
                {
                    var points=Geometry().GroundAround(player.GetWorldId(),At(portal),[2f]);
                    bool approached=false;
                    foreach(var point in points.OrderBy(p=>NaturalFlightPolicy.Distance(At(player),p)))
                        if(await Walk(point)){approached=true;break;}
                    if(approached)continue;
                }
                var from=At(player);var route=Geometry().FindInteractionPath(player.GetWorldId(),from,At(portal));
                if(route.Count<2)break;
                // Travel-waypoint tolerance must not leave an interaction four
                // metres away. Execute the checked interaction route to its end.
                var plan=new BotMover(session.Api.World,session.Api.Timing).CreateGroundPlan(route,from,
                    session.Api.World.MovementSpeed??throw new InvalidDataException("Player movement speed not observed"),TimeSpan.FromMilliseconds(200));
                bool interrupted=false;
                foreach(var chunk in plan.Frames.Chunk(5))
                {
                    await GroundPiece(chunk);var before=At(player);
                    Assert.True(await Defend());
                    if(NaturalFlightPolicy.Distance(before,At(player))>1){interrupted=true;break;}
                }
                if(!interrupted&&PositionUtil.IsInTalkRange(player,portal))return;
            }
            throw new InvalidDataException("No checked ordinary approach to portal "+template);
        }
        IReadOnlyList<BotNavigationHazard> Hazards(BotPosition goal)
        {
            ObserveDeaths();var result=new List<BotNavigationHazard>();
            foreach(var n in session.Api.World.Objects.Values)
            {
                if(n.Kind!=BotKnownObjectKind.Npc||n.IsCorpse||observedDead.Contains(n.ObjectId)||
                    n.TemplateId is not int id||id==guardianTemplate)continue;
                var data=runtime.Data.NpcDataDh.GetNpcTemplate(id);
                if(runtime.IsAggressive(data))result.Add(new(n.Position,runtime.AggroRadius(data)));
            }
            return result;
        }
        async Task GroundPiece(IReadOnlyList<BotMovementFrame> frames)
        {
            // Cutting a packet stream before its final STOP left the server
            // walking while the director started a fight. Rebuild each checked
            // piece as a complete ordinary START/samples/STOP stream.
            var piece=new BotMover(session.Api.World,session.Api.Timing).CreateGroundPlan(
                frames.Select(f=>f.Position).ToArray(),At(player),session.Api.World.MovementSpeed??
                throw new InvalidDataException("Player movement speed not observed"),TimeSpan.FromMilliseconds(200));
            await session.ExecuteMovementAsync(piece,token);await session.SynchronizeAsync(token);
        }
        async Task FlyToPortal(int template)
        {
            // The checked ground detour from Water's actual4.8 exit goes through
            // outdoor packs. Ordinary Morheim flight is available on this ledge;
            // keep the connection within its real zone, clearance and FP rules.
            Assert.True(await Settle());await session.AdvanceAsync(TimeSpan.FromSeconds(10),token);
            var from=At(player);var geo=Geometry();int map=player.GetWorldId();
            var portal=player.GetWorldMapInstance().GetNpcs(template).Single();
            var world=runtime.Data.WorldMaps2.GetTemplate(map);
            var zones=NaturalFlyZone.ForMap(runtime.RepoRoot,world,map);
            var ready=NaturalFlightPolicy.CanTakeOff(new(true,from,false,world.GetWaterLevel(),
                runtime.NowMillis,null,false,false,false),zones);
            Assert.True(ready.Allowed,ready.Reason);
            NaturalFlightRoute? selected=null;BotPosition landing=default;
            foreach(var point in geo.GroundAround(map,At(portal),[2f]).OrderBy(p=>NaturalFlightPolicy.Distance(from,p)))
            {
                var route=NaturalFlightProtocol.Plan(geo,map,from,point,MathF.Max(from.Z,point.Z)+10);
                if(!route.IsUsable)continue;
                var all=new[]{from}.Concat(route.Waypoints).ToArray();
                bool inZone=all.Zip(all.Skip(1)).All(pair=>Enumerable.Range(0,
                    Math.Max(1,(int)MathF.Ceiling(NaturalFlightPolicy.Distance(pair.First,pair.Second)))+1).All(i=>
                    {
                        float t=i/(float)Math.Max(1,(int)MathF.Ceiling(NaturalFlightPolicy.Distance(pair.First,pair.Second)));
                        var p=new BotPosition(pair.First.X+(pair.Second.X-pair.First.X)*t,
                            pair.First.Y+(pair.Second.Y-pair.First.Y)*t,pair.First.Z+(pair.Second.Z-pair.First.Z)*t,0);
                        return zones.Any(z=>!z.Forbids&&z.Contains(p.X,p.Y,p.Z))&&!zones.Any(z=>z.Forbids&&z.Contains(p.X,p.Y,p.Z));
                    }));
                if(!inZone)continue;
                selected=route;landing=point;break;
            }
            Assert.NotNull(selected);
            float speed=await NaturalFlightProtocol.TakeOffAsync(session,token);
            var budget=NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(selected!,from,speed),session.Api.World.CurrentFlightTime,zones);
            Assert.True(budget.Allowed,budget.Reason);
            session.TraceDiagnostic("three-keys-ordinary-exterior-flight",new Dictionary<string,object?>
                {["from"]=from,["landing"]=landing,["route"]=selected!.Waypoints,["budget"]=budget.Reason,["speed"]=speed});
            await session.ExecuteMovementAsync(new BotMover(session.Api.World,session.Api.Timing).CreateFlyPlan(
                selected.Waypoints,from,speed,TimeSpan.FromMilliseconds(200)),token);
            await NaturalFlightProtocol.LandAsync(session,token);await session.SynchronizeAsync(token);
            Assert.True(NaturalFlightPolicy.Distance(At(player),landing)<1);Assert.False(player.IsDead());
        }
        async Task Portal(int template,int destination)
        {
            for(int attempt=0;attempt<8;attempt++)
            {
                await ApproachObject(template);
                var portal=Observed(template);Assert.NotNull(portal);
                int first=session.PacketHistory.Count;
                // Damage legitimately cancels this three-second interaction.
                // Keep the current NPC view until an actual map-change packet;
                // BotWorldModel already clears it on that confirmed transition.
                if(!await NaturalAltgardQuestSteps.UseObjectAsync(session,portal!.ObjectId,null,token,reloadWorld:false))
                {
                    session.TraceDiagnostic("three-keys-portal-interrupted",new Dictionary<string,object?>
                        {["portal"]=template,["attempt"]=attempt,["hp"]=player.GetLifeStats().GetCurrentHp()});
                    Assert.True(await Defend(),"Could not defend after interrupted portal use.");
                    continue;
                }
                if(!session.PacketHistory.Skip(first).Any(p=>p.PacketType==typeof(SM_PLAYER_INFO)))await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO),token);
                session.AcceptTeleportPosition();await session.SynchronizeAsync(token);Assert.Equal(destination,player.GetWorldId());
                return;
            }
            throw new InvalidDataException("Portal use remained interrupted after ordinary defense: "+template);
        }
    }
}
