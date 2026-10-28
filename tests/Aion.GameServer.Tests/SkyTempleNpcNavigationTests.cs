using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using System.Reflection;
using Aion.GameServer.Retail;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World.Knownlist;
using Aion.GameServer.Dataholders;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Tests;

/// <summary>Exercises the actual live managed adapter's asset/scene/search/follow backend.
/// Declared test placeable states are explicit scenarios, not observations of a running instance.</summary>
[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcNavigationTests
{
    private const string DefaultRoot=@"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying";
    private const string ProfileFile=@"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";
    private static readonly Lazy<(SkyTempleNpcNavigationAssets Assets,SkyTempleNpcBody Body)> fixture=new(()=>
    {
        var profile=SkyTempleNpcProfileCatalog.Parse(File.ReadAllBytes(ProfileFile))[212769];
        return(SkyTempleNpcNavigationAssets.Load(Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??DefaultRoot),
            new(profile.Bounds.Front,profile.Bounds.Side,profile.Bounds.Upper,profile.Bounds.HorizontalRadius,profile.SourceRecordSha256,profile.NpcId));
    });
    private static void RequireAssets()
    {
        string root=Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??DefaultRoot;
        Skip.IfNot(File.Exists(System.IO.Path.Combine(root,"IDDF2Flying-path.dat"))&&File.Exists(ProfileFile),"Explicit installed SkyTemple/profile assets are required; no synthetic flat fallback.");
    }
    [SkippableTheory]
    [InlineData(368.25f,400.25f,375.05f,362.25f,406.25f,376.32f)]
    [InlineData(386.75f,448.75f,375.05f,386.75f,440.75f,374.58f)]
    [InlineData(391.25f,406.25f,374.58f,391.25f,398.25f,374.58f)]
    public void ActualAirSpiritBodySearchesDirectedDetourAndEveryCommittedStepUsesGeometryAndAck(float sx,float sy,float sz,float gx,float gy,float gz)
    {
        RequireAssets();var(assets,body)=fixture.Value;
        var scene=assets.Scene(1,new[]{(15,false),(16,false),(17,false)});
        var planner=new SkyTempleNpcRoutePlanner(assets,scene,body,"actual-map-adapter-test");
        var source=new RetailSearchPoint(sx,sy,sz);var goal=new RetailSearchPoint(gx,gy,gz);
        var result=planner.Find(source,goal,"detour-case");
        Assert.True(result.Success,result.Failure);Assert.NotNull(result.Search);Assert.True(result.Search!.SourceValidation.PathBytesVerified);
        Assert.True(result.Search.SourceValidation.TerrainBytesVerified);Assert.True(result.Route!.Points.Count>2,"A direct chord must not replace the source-linked detour.");
        var route=result.Route;var current=source;bool reached=false;
        for(int tick=0;tick<400;tick++)
        {
            var step=planner.Follow(route,current,.7f);Assert.True(step.HasNativeOutcome,step.Detail);
            if(step.NativeValue==2){reached=true;break;}
            // Whole-waypoint movement writes progress1; the actual position ACK makes2.
            // On the next call Consume advances beyond the last point, whose native
            // loop-head count guard returns1 (not the duplicate-waypoint return2).
            if(step.NativeValue==1&&step.FinalPathIndex==route.Points.Count&&
                SkyTempleNpcNavigation.Squared(current,goal)<.01f){reached=true;break;}
            Assert.True(step.NativeValue==0,$"tick={tick}; index={route.Index}/{route.Points.Count}; current={current}; goal={goal}; native={step.NativeValue}; queries="+
                string.Join(" | ",step.GeometryCalls.Select(q=>$"{q.Kind} {q.Input}->{q.End}: {q.Result.Status}/{q.Result.NativeResult} point={q.Result.Point} {q.Result.Detail}")));
            var next=step.NextPosition!.Value;
            Assert.True(planner.StepClear(current,next),$"Actor clearance rejects {current} -> {next}");
            Assert.True(next.IsFinite);Assert.True(scene.Bounds.Contains(next));
            var ack=RetailNpcPathProgressPlan.Acknowledge(new(step.Epoch!,step.Epoch!,true,
                RetailGroundPathSearch.SupportedNpcBinarySha256,body.SourceHash),step.FinalProgress!.Value,new(true,body.SourceHash));
            Assert.True(ack.Success);route=route with {Index=step.FinalPathIndex!.Value,Progress=ack.Progress!.Value};current=next;
        }
        Assert.True(reached,"The source route must finish without a teleport or straight fallback.");
        Assert.True(Aion.GameServer.Controllers.Movement.SkyTempleNpcNavigation.Squared(current,goal)<.01f);
    }
    [SkippableFact]
    public void StackedFloorSelectionKeepsHeightAndNearbyMissingGoalDoesNotGainStartFallback()
    {
        RequireAssets();var(assets,body)=fixture.Value;var planner=new SkyTempleNpcRoutePlanner(assets,
            assets.Scene(2,new[]{(15,false),(16,false),(17,false)}),body,"layers");
        var low=planner.Surface(new(47.75f,133.75f,575.26f));var high=planner.Surface(new(47.75f,133.75f,582.75f));
        Assert.NotNull(low);Assert.NotNull(high);Assert.NotEqual(low!.NativeId,high!.NativeId);
        // Keep this below100m: native's long-distance branch intentionally runs
        // its checked segment before endpoint lookup;9000m did not test this rule.
        var missing=planner.Find(new(391.25f,406.25f,374.58f),new(391.25f,398.25f,380),"wrong-floor");
        Assert.False(missing.Success);Assert.Null(missing.Route);
        Assert.Equal(RetailGroundSearchTermination.MissingGoal,missing.Search!.Termination);
    }
    [SkippableFact]
    public void ThreeActualFieldRangesUseCurrentInstanceActivityAndSceneRevision()
    {
        RequireAssets();var(assets,_)=fixture.Value;
        var inactive=assets.Scene(1,new[]{(15,false),(16,false),(17,false)});
        var active=assets.Scene(1,new[]{(15,true),(16,false),(17,false)});
        var other=assets.Scene(2,new[]{(15,true),(16,false),(17,false)});
        Assert.NotEqual(inactive.Declaration.SceneRevision,active.Declaration.SceneRevision);
        Assert.NotEqual(active.Declaration.InstanceIdentity,other.Declaration.InstanceIdentity);
        // Retained actual field15 has bounds near(372..,249..); choose a real face
        // barycentre and cross it, rather than passing an asserted collision result.
        var face=assets.Collision.Raw.GetTriangle(80);
        var centre=new RetailSearchPoint((face.A.X+face.B.X+face.C.X)/3,(face.A.Y+face.B.Y+face.C.Y)/3,(face.A.Z+face.B.Z+face.C.Z)/3);
        var a=centre with {Z=centre.Z+2};var b=centre with {Z=centre.Z-2};
        var on=RetailCollisionQuery.FirstContact(active,active.Stamp,a,b,0,.001f,SkyTempleNpcNavigationAssets.QueryLimits);
        Assert.Equal(RetailCollisionStatus.Computed,on.Status);
        Assert.Contains(on.ConsumedFieldObjects,o=>o.EditorId==15&&o.RawState==1);
        var stale=RetailCollisionQuery.FirstContact(active,inactive.Stamp,a,b,0,.001f,SkyTempleNpcNavigationAssets.QueryLimits);
        Assert.Equal(RetailCollisionStatus.StaleSource,stale.Status);Assert.Null(stale.NativeResult);
        Assert.Throws<InvalidDataException>(()=>assets.Scene(1,new[]{(15,true),(16,false)}));
    }
    [Fact]
    public void MissingPackageNeverCreatesFlatOrUnverifiedAssets()
    {
        Assert.Throws<DirectoryNotFoundException>(()=>SkyTempleNpcNavigationAssets.Load(System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid().ToString("N"))));
    }
    [Fact]
    public void PublicationRequiresActualCompletePlaceablesWithoutCreatingAnInstance()
    {
        var map=new Node("loaded-SkyTemple");
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcNavigationAssets.ValidatePlaceables(map));
        var nested=new Node("loaded-static-sector");map.AttachChild(nested);
        foreach(int id in new[]{17,15,16})nested.AttachChild(new DespawnableNode
            {id=id,type=DespawnableNode.DespawnableType.PLACEABLE});
        var validated=SkyTempleNpcNavigationAssets.ValidatePlaceables(map);
        Assert.Equal(new[]{15,16,17},validated.Select(n=>n.id));
        Assert.All(validated,n=>Assert.False(n.IsActive(98765)));
    }
    [Fact]
    public void PublicationRejectsMismatchedDynamicClassAndDuplicateOrUnknownIds()
    {
        Node Map(int third)
        {
            var node=new Node("loaded-SkyTemple");
            foreach(int id in new[]{15,16,third})node.AttachChild(new DespawnableNode
                {id=id,type=DespawnableNode.DespawnableType.PLACEABLE});
            return node;
        }
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcNavigationAssets.ValidatePlaceables(Map(16)));
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcNavigationAssets.ValidatePlaceables(Map(18)));
        var wrong=Map(17);((DespawnableNode)wrong.GetChildren().First()).type=DespawnableNode.DespawnableType.NONE;
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcNavigationAssets.ValidatePlaceables(wrong));
    }

    private static SkyTempleNpcNavigationAssets ManagedLoadedScene()
    {
        var assets=SkyTempleNpcNavigationAssets.Load(Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??DefaultRoot);
        var map=GeoService.GetInstance().GetMap(320050000);
        Assert.Empty(map.GetChildren()); // BossAiHarness just installed its fresh actual GeoMap.
        // Explicit current managed instance objects, not an asserted clear query. Their
        // actual geometry still comes from the verified native field ranges/triangles.
        var scene=assets.Scene(1,new[]{(15,false),(16,false),(17,false)});
        foreach(int id in new[]{15,16,17})
        {
            var field=scene.Field(new((uint)id,RetailFieldObjectGeometry.SupportedClass,scene.Declaration.SceneRevision));
            map.AttachChild(new CurrentPlaceable(id,field.Minimum,field.Maximum));
        }
        assets.ValidateLoadedMap();return assets;
    }

    [SkippableFact]
    public void UnreachablePatrolPointStopsItsOwnedMoveWithoutFakeArrivalOrCombatGiveup()
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfileFile));
        using var originalGeoMaps=new LoadedGeoMapScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        SystemClock.UseSource(()=>1_700_000_000_000L+h.Clock.NowMillis);
        using var sceneScope=SkyTempleNpcNavigationAssets.InstallForTests(ManagedLoadedScene());
        var npc=h.SpawnWithAi(212761,"general",431.52f,133.12f,214.70917f);
        npc.GetAi().SetStateIfNot(AIState.WALKING);EmoteManager.EmoteStartWalking(npc);
        var start=SkyTempleNpcNavigation.Position(npc);var move=npc.GetMoveController();
        MoveTaskManager.GetInstance().RearmForDeterministicSimulation();
        Assert.True(move.MoveToPoint(436.89f,132.17f,221.1f));
        h.Clock.Advance(TimeSpan.FromMilliseconds(16000));
        Assert.Equal(AIState.IDLE,npc.GetAi().GetState());Assert.Null(npc.GetTarget());
        Assert.Equal(start,SkyTempleNpcNavigation.Position(npc));
        Assert.False(move.IsInMove());Assert.False(move.RetailDestinationReached());
        Assert.Equal(npc.GetLifeStats().GetMaxHp(),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ActualLargeFlameSpiritIdleRouteUsesNativeFollowerClearanceBeforePositionAck()
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfileFile));
        using var originalGeoMaps=new LoadedGeoMapScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        var assets=ManagedLoadedScene();using var sceneScope=SkyTempleNpcNavigationAssets.InstallForTests(assets);
        var npc=h.SpawnWithAi(212780,"general",144.69f,274.35f,538.3314f);
        npc.GetAi().SetStateIfNot(AIState.WALKING);EmoteManager.EmoteStartWalking(npc);
        var goal=new RetailSearchPoint(145.94f,275.65f,538.3314f);
        var nav=npc.GetMoveController().RetailNavigation;
        bool arrived=false;
        for(uint tick=0;tick<30;tick++)
        {
            var step=nav.Tick(false,goal,tick*200);
            if(step.Kind==SkyTempleMoveKind.Arrived){arrived=true;break;}
            Assert.True(step.Kind==SkyTempleMoveKind.Move,$"tick={tick}; from={SkyTempleNpcNavigation.Position(npc)}; {step.Kind}: {step.Detail}");
            Assert.True(step.Follow!.HasNativeOutcome);Assert.Equal(0u,step.Follow.NativeValue);
            Assert.True(nav.CanApply(step));
            h.World.UpdatePosition(npc,step.Next.X,step.Next.Y,step.Next.Z,step.Heading,false);
            Assert.Equal(step.Next,SkyTempleNpcNavigation.Position(npc));
            nav.Confirm(step,true);
        }
        Assert.True(arrived,"The captured idle route must complete using the computed native follower, without a teleport or replacement route.");
        Assert.InRange(SkyTempleNpcNavigation.Squared(SkyTempleNpcNavigation.Position(npc),goal),0,.01f);
        Assert.Null(nav.LastFailure);Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ActualFireSpiritPursuitPointCanResolveItsReturnLayer()
    {
        RequireAssets();
        var assets=SkyTempleNpcNavigationAssets.Load(Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??DefaultRoot);
        var p=SkyTempleNpcProfileCatalog.Parse(File.ReadAllBytes(ProfileFile))[212778];
        var scene=assets.Scene(2,new[]{(15,false),(16,false),(17,false)});
        var planner=new SkyTempleNpcRoutePlanner(assets,scene,
            new(p.Bounds.Front,p.Bounds.Side,p.Bounds.Upper,p.Bounds.HorizontalRadius,p.SourceRecordSha256,p.NpcId),"SIM-135219");
        var start=new RetailSearchPoint(62.503487f,150.62326f,579.30646f);
        var goal=new RetailSearchPoint(59.13f,149.381f,579.785f);
        var candidates=assets.Surfaces.EnumerateSurfaceCandidates(start.X,start.Y,start.Z);
        var found=planner.Find(start,goal,"actual-SIM-return-layer");
        Assert.True(found.Success,System.Text.Json.JsonSerializer.Serialize(new
        {
            found.Failure,candidates.Status,candidates.Detail,candidates.QueryCentimetres,
            candidates=candidates.Candidates.Select(c=>new{c.Status,c.Surface.Layer,c.Surface.Node.Type,c.Height}),
            projected=planner.Ground(start with{Z=start.Z+2}),
        }));
        Assert.Contains(found.Search!.ConsumedPolicy,c=>c.Purpose==RetailSearchPolicyPurpose.MissingStartFallback);
        Assert.Equal("native-missing-start-checked-segment",found.Route!.Resolution);
        Assert.Equal(found.Search.Points[^1],found.Route.Goal);
        Assert.Equal(goal,found.Route.RequestedGoal);
        var reverse=planner.Find(goal,start,"resolved-start-missing-goal");
        Assert.False(reverse.Success);
        Assert.Equal(RetailGroundSearchTermination.MissingGoal,reverse.Search!.Termination);
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void BlockedRetailNormalAttemptRetriesWithoutRecursionOrInventingAttackRecovery(bool staggered)
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfileFile));
        using var originalGeoMaps=new LoadedGeoMapScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        SystemClock.UseSource(()=>1_700_000_000_000L+h.Clock.NowMillis);
        using var scene=SkyTempleNpcNavigationAssets.InstallForTests(ManagedLoadedScene());
        var npc=h.SpawnWithAi(212769,"general",391.25f,406.25f,374.58f);
        // A complete mortal player, rather than BossAiHarness's decision-only player,
        // supplies the real stats/flight controller read by a normal calculation.
        var common=new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR);common.SetRace(Race.ELYOS);common.SetGender(Gender.MALE);
        common.SetName("RetryTarget"+common.GetPlayerObjId());common.SetNote("");common.SetExp(89979199);
        var target=new Player(new PlayerAccountData(common,new PlayerAppearance()),new Account(1));
        target.SetEffectController(new PlayerEffectController(target));target.SetKnownlist(new KnownList(target));
        target.SetFlyController(new Aion.GameServer.Controllers.FlyController(target));PlayerStatFunctions.AddPredefinedStatFunctions(target);
        target.SetPosition(h.World.CreatePosition(320050000,391.25f,404.25f,374.58f,0,1));
        h.World.StoreObject(target);h.World.Spawn(target);target.GetLifeStats().SetCurrentHp(target.GetLifeStats().GetMaxHp());
        h.Engage(npc,target);
        if(staggered)npc.GetEffectController().SetAbnormal(Aion.GameServer.SkillEngine.Effects.AbnormalState.STUMBLE);
        else npc.SetState(CreatureState.RESTING);
        Assert.False(npc.CanAttack());
        var before=npc.GetGameStats().GetAttackCounter();
        SimpleAttackManager.PerformAttack((NpcAI)npc.GetAi(),0);
        Assert.Equal(before,npc.GetGameStats().GetAttackCounter());
        Assert.Equal(0,SkyTempleNpcSkillState.GetNextActionInterval(npc));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(before,npc.GetGameStats().GetAttackCounter());
        Assert.Equal(0,SkyTempleNpcSkillState.GetNextActionInterval(npc));
        if(staggered)npc.GetEffectController().UnsetAbnormal(Aion.GameServer.SkillEngine.Effects.AbnormalState.STUMBLE);
        else npc.UnsetState(CreatureState.RESTING);
        Assert.True(npc.CanAttack());
        h.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.True(npc.GetGameStats().GetAttackCounter()==before+1,
            $"state={npc.GetAi().GetState()},sub={npc.GetAi().GetSubState()},target={npc.GetTarget()?.GetObjectId()},"+
            $"hate={npc.GetAggroList().GetHate(target)},see={npc.CanSee(target)},arrival={npc.GetMoveController().RetailArrival(target)},"+
            $"sourceDead={npc.IsDead()},targetDead={target.IsDead()},canAttack={npc.CanAttack()},casting={npc.GetCastingSkill()?.GetSkillId()},"+
            $"due={h.Clock.NextDueMillis},tick={h.Clock.NowMillis},failure={npc.GetMoveController().RetailNavigation.LastFailure}");
        Assert.True(SkyTempleNpcSkillState.GetNextActionInterval(npc)>0);
        Assert.Empty(h.Clock.Faults);
    }

    // GeoMap's actual attachment owner selects a chunk from the world AABB. Keep
    // the source-derived field bounds even though the test scene's collision faces
    // are calculated by the retail backend, not this host activity node.
    private sealed class CurrentPlaceable:DespawnableNode
    {
        internal CurrentPlaceable(int editorId,RetailSearchPoint min,RetailSearchPoint max)
        {
            id=editorId;type=DespawnableType.PLACEABLE;
            worldBound=new BoundingBox(new Vector3f(min.X,min.Y,min.Z),new Vector3f(max.X,max.Y,max.Z));
        }
    }

    private sealed class LoadedGeoMapScope:IDisposable
    {
        private readonly Dictionary<int,GeoMap> maps=(Dictionary<int,GeoMap>)typeof(GeoService)
            .GetField("geoMaps",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(GeoService.GetInstance())!;
        private readonly KeyValuePair<int,GeoMap>[] previous;
        internal LoadedGeoMapScope()=>previous=maps.ToArray();
        public void Dispose(){maps.Clear();foreach(var pair in previous)maps.Add(pair.Key,pair.Value);}
    }

    [SkippableFact]
    public void ActualNpcControllerUsesItsExisting200msTaskAndRejectsRetargetedStep()
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfileFile));
        using var originalGeoMaps=new LoadedGeoMapScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        SystemClock.UseSource(()=>1_700_000_000_000L+h.Clock.NowMillis);
        var assets=ManagedLoadedScene();using var sceneScope=SkyTempleNpcNavigationAssets.InstallForTests(assets);
        var npc=h.SpawnWithAi(212769,"general",391.25f,406.25f,374.58f);
        var target=h.SpawnPlayer(391.25f,398.25f,374.58f);
        h.Engage(npc,target); // Real current-target hate reaches the native738 producer.
        npc.GetGameStats().SetFightStartingTime();npc.GetGameStats().RenewLastAttackedTime();
        EmoteManager.EmoteStartAttacking(npc,target);
        var start=SkyTempleNpcNavigation.Position(npc);var move=npc.GetMoveController();
        Assert.NotEqual(SkyTempleArrival.Arrived,move.RetailArrival(target));
        MoveTaskManager.GetInstance().RearmForDeterministicSimulation();
        move.MoveToTargetObject();h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.NotEqual(start,SkyTempleNpcNavigation.Position(npc));
        Assert.True(assets.Bounds.Contains(SkyTempleNpcNavigation.Position(npc)));
        Assert.Null(move.RetailNavigation.LastFailure);
        // Move the actual target to a linked adjacent surface; no target-position answer
        // or collision result is supplied to the adapter.
        var source=assets.Surfaces.EnumerateSurfaceCandidates(target.GetX(),target.GetY(),target.GetZ())
            .Candidates.First(c=>c.Status==RetailSurfaceCandidateStatus.WithinTolerance).Surface;
        Assert.NotNull(source);var next=assets.Surfaces.GetCardinalNeighbor(source!,3);
        Assert.Equal(RetailSurfaceQueryStatus.Found,next.Status);Assert.NotNull(next.Target);
        var nextSurface=next.Target!;
        var height=assets.Surfaces.ReadHeight(nextSurface);Assert.Equal(RetailSurfaceHeightStatus.Resolved,height.Status);
        h.World.UpdatePosition(target,nextSurface.CentreXMetres,nextSurface.CentreYMetres,height.Metres,target.GetHeading(),false);
        h.Clock.Advance(TimeSpan.FromMilliseconds(400));
        Assert.Null(move.RetailNavigation.LastFailure);
        Assert.True(SkyTempleNpcSkillState.TryGetApproachGoal(npc,out var goal));
        var pending=move.RetailNavigation.Tick(true,default,unchecked(SystemClock.RetailTick32()+200),goal,npc.GetTarget(),true);
        Assert.Equal(SkyTempleMoveKind.Move,pending.Kind);
        Assert.True(move.RetailNavigation.CanApply(pending));
        var replacement=h.SpawnPlayer(386.75f,448.75f,375.05f);npc.SetTarget(replacement);
        Assert.False(move.RetailNavigation.CanApply(pending));
        move.RetailNavigation.Confirm(pending,false);move.AbortMove();
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ForcedBolcan16710StopsAtItsComputedSkillRangeInsteadOfNormalRangeTwo()
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfileFile));
        using var originalGeoMaps=new LoadedGeoMapScope();
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        SystemClock.UseSource(()=>1_700_000_000_000L+h.Clock.NowMillis);
        var assets=ManagedLoadedScene();using var sceneScope=SkyTempleNpcNavigationAssets.InstallForTests(assets);
        var npc=h.SpawnWithAi(212876,"general",391.25f,406.25f,374.58f);
        Assert.True(SkyTempleNpcProfileCatalog.TryGet(npc,out var profile));Assert.Equal(2,profile.Attacks[0].Range);
        var from=SkyTempleNpcNavigation.Position(npc);
        var body=new SkyTempleNpcBody(profile.Bounds.Front,profile.Bounds.Side,profile.Bounds.Upper,
            profile.Bounds.HorizontalRadius,profile.SourceRecordSha256,profile.NpcId);
        var planner=new SkyTempleNpcRoutePlanner(assets,assets.CurrentScene(npc),body,"actual-forced-range-test");
        RetailSearchPoint? targetPoint=null;
        foreach(var delta in new[]{(0f,-6f),(6f,0f),(-6f,0f),(0f,6f),(0f,-8f),(8f,0f)})
        {
            var ground=planner.Ground(new(from.X+delta.Item1,from.Y+delta.Item2,from.Z+2));
            if(ground is { } p&&planner.Surface(p) is not null&&SkyTempleNpcNavigation.Squared(from,p)>25&&
                planner.Visible(from,body.Upper,p,2)){targetPoint=p;break;}
        }
        Assert.NotNull(targetPoint);var point=targetPoint!.Value;
        var target=h.SpawnPlayer(point.X,point.Y,point.Z);npc.SetTarget(target);npc.GetAi().SetStateIfNot(AIState.FIGHT);
        npc.QueueSkill(16710,24,0);
        var queued=npc.GetNextQueuedSkill();Assert.NotNull(queued);
        Assert.True(SkyTempleNpcSkillState.TryGetApproachGoal(npc,out var goal));
        Assert.Same(target,goal.Target);Assert.Equal(16710,goal.SkillId);Assert.Equal(16,goal.Range);
        var move=npc.GetMoveController();Assert.Equal(SkyTempleArrival.OutOfRange,move.RetailArrival(target));
        Assert.Equal(SkyTempleArrival.Arrived,move.RetailArrival(target,goal.Range));
        Assert.True(move.RetailDestinationReached());
        var step=move.RetailNavigation.Tick(true,default,SystemClock.RetailTick32(),goal,npc.GetTarget(),true);
        Assert.Equal(SkyTempleMoveKind.Arrived,step.Kind);Assert.Equal(from,SkyTempleNpcNavigation.Position(npc));
        var template=DataManager.SKILL_DATA.GetSkillTemplate(16710);
        var skill=new Aion.GameServer.SkillEngine.Model.Skill(template,npc,24,target,null!);
        var properties=Assert.IsType<Properties>(template.GetProperties());
        Assert.True(FirstTargetRangeProperty.Set(skill,properties,Properties.CastState.CAST_START));
        Assert.Same(queued,npc.GetNextQueuedSkill()); // Goal queries did not dequeue.
        move.AbortMove();Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void NavigationTimeTreatsTickZeroAsARealSampleAndResetAsANewOwner()
    {
        var timing=new SkyTempleNpcNavigationTiming();
        Assert.Equal(200u,timing.MovementMilliseconds(0));
        Assert.Equal(0u,timing.MovementMilliseconds(0));
        Assert.Equal(200u,timing.MovementMilliseconds(200));
        timing.SearchStarted(0);timing.TargetSampled(0);
        Assert.False(timing.CanSearch(999));Assert.True(timing.CanSearch(1000));
        Assert.Equal(200u,timing.PredictionMilliseconds(200));Assert.Null(timing.PredictionMilliseconds(0));
        Assert.False(timing.FailureExpired(0,false,15_000));Assert.True(timing.HasFailure);
        timing.Reset();Assert.False(timing.HasFailure);Assert.True(timing.CanSearch(0));
        Assert.Null(timing.PredictionMilliseconds(200));Assert.Equal(200u,timing.MovementMilliseconds(0));
    }

    [Fact]
    public void NavigationMovementAndPredictionUseElapsedAcrossUnsignedRollover()
    {
        var timing=new SkyTempleNpcNavigationTiming();
        Assert.Equal(200u,timing.MovementMilliseconds(0xffffff00));timing.TargetSampled(0xffffff00);
        Assert.Equal(300u,timing.MovementMilliseconds(44));Assert.Equal(300u,timing.PredictionMilliseconds(44));
        Assert.Equal(1000u,timing.MovementMilliseconds(2044)); // Explicit managed catch-up cap.
        Assert.Null(timing.PredictionMilliseconds(2044));
    }

    [Fact]
    public void NavigationRetryGateExpiresAtOneSecondAcrossRollover()
    {
        var timing=new SkyTempleNpcNavigationTiming();timing.SearchStarted(0xffffff00);
        Assert.False(timing.CanSearch(743));Assert.True(timing.CanSearch(744));
        timing.SearchSucceeded();Assert.True(timing.CanSearch(0));
    }

    [Fact]
    public void ManagedFailedRouteBoundIncludesEqualityAndReturnDoesNotGiveUp()
    {
        var timing=new SkyTempleNpcNavigationTiming();
        Assert.False(timing.FailureExpired(0xffffff00,false,15_000));
        Assert.False(timing.FailureExpired(14_743,false,15_000));
        Assert.True(timing.FailureExpired(14_744,false,15_000));
        Assert.False(timing.FailureExpired(14_744,true,15_000));
        timing.ProgressConfirmed();Assert.False(timing.HasFailure);
        Assert.False(timing.FailureExpired(14_744,false,15_000));
        Assert.False(timing.FailureExpired(99_999,false,0));
    }

    private sealed class StopObserver(Action action):ActionObserver(ObserverType.MOVE)
    {public override void Moved()=>action();}

    [SkippableTheory]
    [InlineData(false)] [InlineData(true)]
    public void ActualFailedChaseStopsOutsideNavLockAndRetargetDuringStopCannotGiveUpTheReplacement(bool retarget)
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfileFile));
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        uint tick=0;SystemClock.UseSource(()=>tick);
        var npc=h.SpawnWithAi(212769,"general",391.25f,406.25f,374.58f);
        Assert.True(SkyTempleNpcProfileCatalog.TryGet(npc,out var profile));
        var common=new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR);common.SetRace(Race.ELYOS);common.SetGender(Gender.MALE);
        common.SetName("NavLeaseEmission");common.SetNote("");
        common.SetExp(DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(profile.Level));
        var target=new Player(new PlayerAccountData(common,new PlayerAppearance()),new Account(1));
        target.SetEffectController(new PlayerEffectController(target));target.SetKnownlist(new KnownList(target));
        target.SetFlyController(new Aion.GameServer.Controllers.FlyController(target));PlayerStatFunctions.AddPredefinedStatFunctions(target);
        target.SetPosition(h.World.CreatePosition(320050000,391.25f,404.25f,374.58f,0,1));
        h.World.StoreObject(target);h.World.Spawn(target);target.GetLifeStats().SetCurrentHp(target.GetLifeStats().GetMaxHp());
        var replacement=h.SpawnPlayer(386.75f,448.75f,375.05f);
        h.Engage(npc,target);
        // The genuine normal packet commits738 through the longer attack cadence,
        // while movement resumes at its shorter source motion recovery. This
        // creates a real window for NAV's bounded failure lease before native
        // chase expiry; refreshing hate AFTER A18/failure would not do so.
        Assert.True(SkyTempleNpcCombat.TryGetNormalTiming(npc,target,out var normal));
        var counter=npc.GetGameStats().GetAttackCounter();npc.GetController().AttackTarget(target,normal.ImpactDelayMs,false);
        Assert.Equal(counter+1,npc.GetGameStats().GetAttackCounter());
        tick=checked((uint)SkyTempleNpcSkillState.GetNextMovementInterval(npc)+1u);
        h.World.UpdatePosition(target,391.25f,398.25f,374.58f,0,false);
        // This is a real typed rejection branch, not an asserted search failure.
        npc.SetState(CreatureState.FLYING);
        var move=npc.GetMoveController();var nav=move.RetailNavigation;
        move.MoveToTargetObject();int callbacks=0;
        npc.GetObserveController().AddObserver(new StopObserver(()=>
        {
            callbacks++;Assert.False(Monitor.IsEntered(nav));
            if(retarget)npc.SetTarget(replacement);
        }));
        move.MoveToDestination();Assert.Equal("unsupported-alternate-motion",nav.LastFailure);
        tick=checked(tick+15_000u);
        Assert.True(tick<SkyTempleNpcSkillState.GetChaseSnapshot(npc).Deadline);
        Assert.True(SkyTempleNpcSkillState.TryEvaluateChase(npc,out var chase));
        Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Continue,chase.Disposition);
        move.MoveToDestination();
        Assert.True(callbacks>0);
        if(retarget)
        {
            Assert.False(move.IsInMove());
            Assert.Same(replacement,npc.GetTarget());Assert.Equal(AIState.FIGHT,npc.GetAi().GetState());
            Assert.Equal(1000,npc.GetAggroList().GetHate(target)); // No old TargetGiveup dispatched.
        }
        else Assert.Equal(0,npc.GetAggroList().GetHate(target));
        move.AbortMove();Assert.Empty(h.Clock.Faults);
    }

    [SkippableTheory]
    [InlineData("tick")] [InlineData("cancel")] [InlineData("target")] [InlineData("life")]
    public void GiveUpLeaseIsInvalidatedByNewerWorkRetargetOrRetirement(string change)
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfileFile));
        using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        SystemClock.UseSource(()=>0);
        var npc=h.SpawnWithAi(212769,"general",391.25f,406.25f,374.58f);
        var target=h.SpawnPlayer(391.25f,398.25f,374.58f);
        npc.SetTarget(target);npc.GetAi().SetStateIfNot(AIState.FIGHT);npc.SetState(CreatureState.FLYING);
        var nav=npc.GetMoveController().RetailNavigation;
        Assert.Equal(SkyTempleMoveKind.Blocked,nav.Tick(true,default,0).Kind);
        var step=nav.Tick(true,default,15_000);Assert.Equal(SkyTempleMoveKind.GiveUp,step.Kind);
        var lease=Assert.IsType<SkyTempleGiveUpLease>(nav.ClaimGiveUp(step));Assert.True(nav.IsCurrentGiveUp(lease));
        switch(change)
        {
            case "tick":nav.Tick(true,default,15_001);break;
            case "cancel":nav.Cancel();break;
            case "target":npc.SetTarget(h.SpawnPlayer(390,398,374.58f));break;
            case "life":h.World.Despawn(npc);break;
        }
        Assert.False(nav.IsCurrentGiveUp(lease));Assert.False(nav.FinishGiveUp(lease));
        Assert.Null(nav.ClaimGiveUp(step));Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ActualCancelledSkillMovementRecoveryUsesTheExistingMoveTaskAndResumesAtItsDeadline()
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfileFile));
        using var oldGeo=new LoadedGeoMapScope();using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        uint tick=10_000;SystemClock.UseSource(()=>tick);
        using var scene=SkyTempleNpcNavigationAssets.InstallForTests(ManagedLoadedScene());
        var npc=h.SpawnWithAi(212769,"general",391.25f,406.25f,374.58f);
        Assert.True(SkyTempleNpcProfileCatalog.TryGet(npc,out var profile));
        var common=new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR);common.SetRace(Race.ELYOS);common.SetGender(Gender.MALE);
        common.SetName("RetailRecoveryFixture");common.SetNote("");
        common.SetExp(DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(profile.Level));
        var target=new Player(new PlayerAccountData(common,new PlayerAppearance()),new Account(1));
        target.SetEffectController(new PlayerEffectController(target));target.SetKnownlist(new KnownList(target));
        target.SetFlyController(new Aion.GameServer.Controllers.FlyController(target));PlayerStatFunctions.AddPredefinedStatFunctions(target);
        //16725 actually has2m source reach: start a real permitted cast, cancel it,
        // then move the same actual target out of reach before testing recovery/chase.
        target.SetPosition(h.World.CreatePosition(320050000,391.25f,404.25f,374.58f,0,1));
        h.World.StoreObject(target);h.World.Spawn(target);target.GetLifeStats().SetCurrentHp(target.GetLifeStats().GetMaxHp());
        Assert.Equal(profile.Level,(int)target.GetLevel());Assert.False(target.IsInvulnerable());
        BossAiHarness.MakeMutuallyKnown(npc,target);
        h.Engage(npc,target); // A real encounter retains its target through AttackComplete/Think.
        Assert.True(npc.GetAggroList().GetHate(target)>0);
        var move=npc.GetMoveController();move.MoveToTargetObject();
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16725,21,target);
        Assert.NotNull(skill);
        // This binding has a source17000ms delay, which also gates the native
        // zero-initialized reuse anchor. The fixture's10000ms tick is too early.
        Assert.False(SkyTempleNpcSkillState.CanStartSkill(npc,skill));
        tick=checked((uint)profile.Skills[0].Delay+1u);
        string Snapshot()=>$"tick={tick};sourceHP={npc.GetLifeStats().GetCurrentHp()};targetHP={target.GetLifeStats().GetCurrentHp()};"+
            $"sourceState={npc.GetState()};targetState={target.GetState()};visual={target.GetVisualState()};"+
            $"spawn={npc.IsSpawned()}/{target.IsSpawned()};dead={npc.IsDead()}/{target.IsDead()};"+
            $"enemy={npc.IsEnemy(target)};canTarget={npc.CanTarget(target)};hate={npc.GetAggroList().GetHate(target)};"+
            $"casting={npc.GetCastingSkill()?.GetSkillId()};source={SkyTempleNpcNavigation.Position(npc)};"+
            $"target={SkyTempleNpcNavigation.Position(target)};move={move.IsInMove()};"+
            $"movementRecovery={SkyTempleNpcSkillState.GetNextMovementInterval(npc)};delay={profile.Skills[0].Delay}";
        // Diagnose the actual rejection stage on a separate Skill instance. Property
        // validation mutates its target list, so it must never prepare the real cast.
        var diagnostic=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16725,21,target);
        Assert.NotNull(diagnostic);var template=diagnostic.GetSkillTemplate();
        var properties=Assert.IsType<Properties>(template.GetProperties());
        Assert.True(FirstTargetProperty.Set(diagnostic,properties),"FirstTargetProperty: "+Snapshot());
        Assert.True(FirstTargetRangeProperty.Set(diagnostic,properties,Properties.CastState.CAST_START),
            $"FirstTargetRangeProperty range={properties.GetFirstTargetRange()},arrival={move.RetailArrival(target,properties.GetFirstTargetRange())}: "+Snapshot());
        var targets=properties.ValidateEffectedList(diagnostic.GetEffectedList(),diagnostic.GetFirstTarget(),npc,template,diagnostic.GetX(),diagnostic.GetY(),diagnostic.GetZ());
        Assert.True(targets.IsValid(),"Target list properties: "+Snapshot());
        Assert.NotEmpty(diagnostic.GetEffectedList());
        Assert.True(template.GetStartconditions()?.Validate(diagnostic)??true,"PreCast/startconditions: "+Snapshot());
        Assert.True(template.GetEndConditions()?.CanValidate(diagnostic)??true,"Cast cost conditions: "+Snapshot());
        if(template.GetActions() is { } actions)foreach(var action in actions.GetActions())
            Assert.True(action.CanAct(diagnostic),$"Cast cost action {action.GetType().Name}: "+Snapshot());
        Assert.True(npc.CanTarget(target)&&target.IsSpawned()&&!target.IsDead(),"CanTargetFirstTarget: "+Snapshot());
        Assert.True(diagnostic.CanUseSkill(Properties.CastState.CAST_START),"Combined CanUseSkill: "+Snapshot());
        Assert.True(SkyTempleNpcSkillState.CanStartSkill(npc,skill),"Source count/reuse admission: "+Snapshot());
        Assert.True(skill.UseSkill(),"Actual UseSkill after successful independent stages: "+Snapshot());Assert.Same(skill,npc.GetCastingSkill());
        npc.GetController().CancelCurrentSkill(target);
        Assert.Equal(100,SkyTempleNpcSkillState.GetNextMovementInterval(npc));
        Assert.Same(target,npc.GetTarget());Assert.Equal(AIState.FIGHT,npc.GetAi().GetState());
        h.World.UpdatePosition(target,391.25f,398.25f,374.58f,0);
        var start=SkyTempleNpcNavigation.Position(npc);move.MoveToDestination();
        Assert.Equal(start,SkyTempleNpcNavigation.Position(npc));Assert.Null(move.RetailNavigation.LastFailure);
        tick+=99;move.MoveToDestination();Assert.Equal(start,SkyTempleNpcNavigation.Position(npc));
        tick++;Assert.Equal(0,SkyTempleNpcSkillState.GetNextMovementInterval(npc));
        // The recovery is eligible at100ms. Movement is serviced on its existing
        //200ms task quantum, not a synthetic1ms catch-up step after the99ms probe.
        tick+=100;move.MoveToDestination();
        Assert.True(start!=SkyTempleNpcNavigation.Position(npc),"Recovery expired but controller did not move: "+
            Snapshot()+$";lastFailure={move.RetailNavigation.LastFailure};state={npc.GetAi().GetState()};target={npc.GetTarget()?.GetObjectId()}");
        move.AbortMove();Assert.Empty(h.Clock.Faults);
    }
}
