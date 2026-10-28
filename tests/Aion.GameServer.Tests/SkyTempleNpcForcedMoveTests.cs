using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Knownlist;
using System.Reflection;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcForcedMoveTests
{
    private static readonly SkyTemplePlayerBody PlayerBody=new(.25f,.25f,2);
    private static RetailCollisionScene Scene(float? wallX=null,float low=10,float high=14,byte selector=6,ushort ground=320,
        IReadOnlyList<RetailCollisionRegionObservation>? regions=null)
    {
        CollisionFace[] faces=wallX is { } x?
            [new(new(x,8,low),new(x,14,low),new(x,8,high),0),new(new(x,14,low),new(x,14,high),new(x,8,high),0)]:[CollisionFixture.Face()];
        var bands=new List<CollisionBand>();
        if(wallX.HasValue)for(int h=((int)low/2)*2;h<=((int)high/2)*2;h+=2)bands.Add(new((short)h,0,1));
        else bands.Add(new(0));
        var raw=CollisionFixture.Read(CollisionFixture.Mesh(faces),CollisionFixture.Collision((_,_)=>bands));
        return CollisionFixture.Scene(CollisionFixture.Prepared(raw),CollisionFixture.Terrain((_,_)=>selector,(_,_)=>ground),regions);
    }
    [Theory]
    [InlineData(false,10)] [InlineData(true,12)]
    public void OrdinaryPlayerGroundAndGlideCalculateTwoMetresFromTheirActualPose(bool glide,float z)
    {
        var result=SkyTempleNpcForcedMove.Calculate(Scene(),new(9,10,z),new(11,10,z),PlayerBody,glide,CollisionFixture.QueryLimits);
        Assert.Equal(SkyTempleForcedMoveStatus.Endpoint,result.Status);Assert.Equal(new RetailSearchPoint(13,10,z),result.Endpoint);
        Assert.Equal(4,result.Queries.Count);Assert.All(result.Queries,q=>Assert.Equal(RetailCollisionStatus.Computed,q.Status));
        Assert.NotEmpty(result.Queries[0].Trace);Assert.Equal(new RetailSearchPoint(13,10,10),result.Queries[0].Point);
    }
    [Fact]
    public void ActualWallGeometryRejectsTwoMetresThenAdmitsNativeOneAndAHalfCandidate()
    {
        var result=SkyTempleNpcForcedMove.Calculate(Scene(12.8f),new(9,10,10),new(11,10,10),PlayerBody,false,CollisionFixture.QueryLimits);
        Assert.Equal(SkyTempleForcedMoveStatus.Endpoint,result.Status);Assert.Equal(new RetailSearchPoint(12.5f,10,10),result.Endpoint);
        Assert.Contains(result.Queries,q=>q.NativeResult==1&&q.Trace.Any(t=>t.Kind==RetailCollisionTraceKind.MeshTriangle));
    }
    [Fact]
    public void ThreeKnownBlockedCandidatesKeepOriginalPlayerPosition()
    {
        var result=SkyTempleNpcForcedMove.Calculate(Scene(12),new(9,10,10),new(11,10,10),PlayerBody,false,CollisionFixture.QueryLimits);
        Assert.Equal(SkyTempleForcedMoveStatus.NoDisplacement,result.Status);Assert.Equal(new RetailSearchPoint(11,10,10),result.Endpoint);
        Assert.Equal(6,result.Queries.Count); // Each real ground projection followed by a real forward hit.
    }
    [Theory]
    [InlineData(1.5f,0)] [InlineData(2,1)]
    public void ReverseProbeUsesPlayerUpperRatherThanNpcSourceUpper(float upper,int expectedValue)
    {
        var expected=(SkyTempleForcedMoveStatus)expectedValue;
        var result=SkyTempleNpcForcedMove.Calculate(Scene(10.85f,11.8f,12.2f),new(9,10,10),new(11,10,10),
            PlayerBody with{Upper=upper},false,CollisionFixture.QueryLimits);
        Assert.Equal(expected,result.Status);
        Assert.Equal(expected==SkyTempleForcedMoveStatus.Endpoint?new RetailSearchPoint(13,10,10):new RetailSearchPoint(11,10,10),result.Endpoint);
    }
    [Theory]
    [InlineData(.05f,13)] [InlineData(.25f,12.5f)]
    public void ForwardExtensionUsesCurrentPlayerHorizontalExtent(float front,float x)
    {
        var result=SkyTempleNpcForcedMove.Calculate(Scene(13.1f),new(9,10,10),new(11,10,10),
            new(front,front,2),false,CollisionFixture.QueryLimits);
        Assert.Equal(SkyTempleForcedMoveStatus.Endpoint,result.Status);Assert.Equal(new RetailSearchPoint(x,10,10),result.Endpoint);
    }
    [Theory]
    [InlineData(false,1)] [InlineData(true,0)]
    public void GroundRequiresSupportButNativeGlideMayRetainItsAirborneThreeDimensionalCandidate(bool gliding,int expectedValue)
    {
        var expected=(SkyTempleForcedMoveStatus)expectedValue;
        var result=SkyTempleNpcForcedMove.Calculate(Scene(selector:12),new(9,10,12),new(11,10,12),PlayerBody,gliding,CollisionFixture.QueryLimits);
        Assert.Equal(expected,result.Status);Assert.Equal(new RetailSearchPoint(gliding?13:11,10,12),result.Endpoint);
    }
    [Fact]
    public void ZeroGroundHeightIsAKnownRejectedCandidateRatherThanAFlatFallback()
    {
        var result=SkyTempleNpcForcedMove.Calculate(Scene(ground:0),new(9,10,0),new(11,10,0),PlayerBody,false,CollisionFixture.QueryLimits);
        Assert.Equal(SkyTempleForcedMoveStatus.NoDisplacement,result.Status);Assert.Equal(new RetailSearchPoint(11,10,0),result.Endpoint);
    }
    [Fact]
    public void UnknownDynamicMembershipWithholdsEndpointInsteadOfDeclaringNoDisplacement()
    {
        var result=SkyTempleNpcForcedMove.Calculate(Scene(selector:1,regions:[]),new(9,10,10),new(11,10,10),PlayerBody,false,CollisionFixture.QueryLimits);
        Assert.Equal(SkyTempleForcedMoveStatus.Unsupported,result.Status);Assert.Null(result.Endpoint);
        Assert.Contains(result.Queries,q=>q.Status==RetailCollisionStatus.UnsupportedDynamicScene);
    }
    [Fact]
    public void DefensiveGeometryBudgetCannotBecomeAClearCollisionAnswer()
    {
        var result=SkyTempleNpcForcedMove.Calculate(Scene(),new(9,10,10),new(11,10,10),PlayerBody,false,
            CollisionFixture.QueryLimits with{MaxTraceEntries=0});
        Assert.Equal(SkyTempleForcedMoveStatus.Unsupported,result.Status);Assert.Null(result.Endpoint);
    }
    [Theory]
    [InlineData(-.25f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void InvalidConsumedPlayerBoundsCannotProduceAnEndpoint(float bound)
    {
        var result=SkyTempleNpcForcedMove.Calculate(Scene(),new(9,10,10),new(11,10,10),PlayerBody with{Front=bound},false,CollisionFixture.QueryLimits);
        Assert.Equal(SkyTempleForcedMoveStatus.Unsupported,result.Status);Assert.Null(result.Endpoint);Assert.Empty(result.Queries);
    }

    private const string ProfilePath=@"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";
    private const string MapRoot=@"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying";
    private static void RequireAssets()=>Skip.IfNot(File.Exists(ProfilePath)&&File.Exists(Path.Combine(
        Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??MapRoot,"Collision.Dat")),"Explicit source-bound SkyTemple assets are required");
    private sealed class CurrentPlaceable:DespawnableNode
    {
        internal CurrentPlaceable(int idValue,RetailFieldObjectGeometry field)
        {
            id=idValue;type=DespawnableType.PLACEABLE;
            worldBound=new BoundingBox(new Vector3f(field.Minimum.X,field.Minimum.Y,field.Minimum.Z),
                new Vector3f(field.Maximum.X,field.Maximum.Y,field.Maximum.Z));
        }
    }
    private sealed class GeoScope:IDisposable
    {
        private readonly Dictionary<int,GeoMap> maps=(Dictionary<int,GeoMap>)typeof(GeoService)
            .GetField("geoMaps",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(GeoService.GetInstance())!;
        private readonly KeyValuePair<int,GeoMap>[] previous;
        internal GeoScope()=>previous=maps.ToArray();
        public void Dispose(){maps.Clear();foreach(var p in previous)maps.Add(p.Key,p.Value);}
    }
    private static SkyTempleNpcNavigationAssets LoadedAssets()
    {
        var assets=SkyTempleNpcNavigationAssets.Load(Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??MapRoot);
        var map=GeoService.GetInstance().GetMap(320050000);Assert.Empty(map.GetChildren());
        var scene=assets.Scene(1,new[]{(15,false),(16,false),(17,false)});
        foreach(int id in new[]{15,16,17})map.AttachChild(new CurrentPlaceable(id,
            scene.Field(new((uint)id,RetailFieldObjectGeometry.SupportedClass,scene.Declaration.SceneRevision))));
        assets.ValidateLoadedMap();return assets;
    }
    private static (Npc Source,Player Target,Effect Child,StaggerEffect Template,Effect Parent) OwnedChild(BossAiHarness h,bool gliding,bool initialize=true)
    {
        var p=SkyTempleNpcProfileCatalog.GetPreparedProfiles().First(p=>p.Skills.Any(s=>s.Id48==17039));
        var row=p.Skills.First(s=>s.Id48==17039);
        var source=h.SpawnWithAi(p.NpcId,"general",391.25f,406.25f,374.58f);
        var common=new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR);common.SetRace(Race.ELYOS);common.SetGender(Gender.MALE);
        common.SetName("RetailStaggerFixture");common.SetNote("");
        common.SetExp(DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(p.Level));
        var target=new Player(new PlayerAccountData(common,new PlayerAppearance()),new Account(1));
        target.SetKnownlist(new KnownList(target));target.SetEffectController(new PlayerEffectController(target));
        target.SetFlyController(new Aion.GameServer.Controllers.FlyController(target));
        target.SetPosition(h.World.CreatePosition(320050000,391.25f,404.25f,gliding?376.58f:374.58f,0,1));
        h.World.StoreObject(target);h.World.Spawn(target);target.GetLifeStats().SetCurrentHp(target.GetLifeStats().GetMaxHp());
        if(gliding){target.SetFlyState(FlyState.GLIDING);target.SetState(CreatureState.GLIDING);}
        var parentTemplate=DataManager.SKILL_DATA.GetSkillTemplate(17039);var parent=new Effect(source,target,parentTemplate,row.Level);
        var parentEffect=parentTemplate.GetEffectTemplate(1)!;parent.AddSuccessEffect(parentEffect);
        parent.SetReserveds(new(1,100,EffectReserved.ResourceType.HP,true),false);
        var childTemplate=DataManager.SKILL_DATA.GetSkillTemplate(8217);
        var child=new Effect(source,target,childTemplate,1,null,parent.GetForceType(),true,null);
        SkyTempleNpcSkillCombat.BindChild(parent,child,parentEffect);
        Assert.True(SkyTempleNpcSkillCombat.IsOwnedChild(child,17039,8217));
        var stagger=Assert.IsType<StaggerEffect>(childTemplate.GetEffectTemplate(1));
        if(initialize)child.Initialize();return(source,target,child,stagger,parent);
    }
    [SkippableTheory]
    [InlineData(false)] [InlineData(true)]
    public void ActualSourceOwnedChildUsesPlayerGeometryAndExistingStaggerCommitIncludingGlide(bool gliding)
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfilePath));
        using var originalGeo=new GeoScope();using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        var assets=LoadedAssets();using var configured=SkyTempleNpcNavigationAssets.InstallForTests(assets);
        var x=OwnedChild(h,gliding);Assert.True(x.Child.IsInSuccessEffects(1));
        Assert.True(SkyTempleNpcForcedMove.CanCommit(x.Child,out var endpoint,out var reason),reason);
        Assert.True(assets.Bounds.Contains(endpoint));Assert.InRange((int)x.Target.GetLevel(),1,80);
        x.Template.ApplyEffect(x.Child);
        Assert.Equal(endpoint,new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ()));
        Assert.True(x.Target.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));
        Assert.False(x.Target.IsInGlidingState());
        x.Child.EndEffect();Assert.False(x.Target.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));
        Assert.Empty(h.Clock.Faults);
    }
    [SkippableFact]
    public void RetiredActualParentCapabilityCannotFallThroughLegacyPlayerPushback()
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfilePath));
        using var originalGeo=new GeoScope();using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        using var configured=SkyTempleNpcNavigationAssets.InstallForTests(LoadedAssets());
        var x=OwnedChild(h,false);Assert.True(x.Child.IsInSuccessEffects(1));
        var before=new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ());
        h.World.Despawn(x.Source);
        Assert.True(SkyTempleNpcForcedMove.Owns(x.Child));Assert.False(SkyTempleNpcSkillCombat.IsOwnedChild(x.Child,17039,8217));
        x.Template.ApplyEffect(x.Child);
        Assert.False(x.Child.IsInSuccessEffects(1));Assert.False(x.Target.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));
        Assert.Equal(before,new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ()));Assert.Empty(h.Clock.Faults);
    }

    [SkippableTheory]
    [InlineData(false)] [InlineData(true)]
    public void PrepassRecalculatesCurrentPlayerGeometryOnceAndHitPhaseOnlyStartsAbnormalState(bool gliding)
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfilePath));
        using var originalGeo=new GeoScope();using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        var assets=LoadedAssets();using var configured=SkyTempleNpcNavigationAssets.InstallForTests(assets);
        var x=OwnedChild(h,gliding);Assert.True(x.Child.IsInSuccessEffects(1));
        // Actual motion after child calculation, before the source's scheduled prepass.
        h.World.UpdatePosition(x.Target,x.Target.GetX(),x.Target.GetY()-.5f,x.Target.GetZ(),x.Target.GetHeading());
        var before=new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ());
        var bounds=x.Target.GetObjectTemplate().GetBoundRadius();
        var expected=SkyTempleNpcForcedMove.Calculate(assets.CurrentScene(x.Source),
            new(x.Source.GetX(),x.Source.GetY(),x.Source.GetZ()),before,
            new(bounds.GetFront(),bounds.GetSide(),bounds.GetUpper()),gliding,SkyTempleNpcNavigationAssets.QueryLimits);
        Assert.True(expected.Supported,expected.Detail);Assert.NotNull(expected.Endpoint);
        Assert.True(SkyTempleNpcForcedMove.TryApplyPrepass(x.Child,out var detail),detail);
        var endpoint=expected.Endpoint!.Value;Assert.Equal(endpoint,new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ()));
        Assert.False(x.Target.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));
        Assert.False(x.Target.IsInGlidingState());
        Assert.True(SkyTempleNpcForcedMove.TryApplyPrepass(x.Child,out detail),detail);
        Assert.Equal(endpoint,new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ()));
        // A later current position must survive phase0: replaying the original forced
        // movement here would set the old endpoint again and emit another forced packet.
        var later=endpoint with{X=endpoint.X+.125f};
        h.World.UpdatePosition(x.Target,later.X,later.Y,later.Z,x.Target.GetHeading());
        x.Template.ApplyEffect(x.Child);
        Assert.Equal(later,new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ()));
        Assert.True(x.Target.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));
        x.Child.EndEffect();Assert.False(x.Target.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void RetiredPrepassCannotStartAbnormalStateAndDoesNotUndoAlreadyCommittedMovement()
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfilePath));
        using var originalGeo=new GeoScope();using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        using var configured=SkyTempleNpcNavigationAssets.InstallForTests(LoadedAssets());
        var x=OwnedChild(h,false);Assert.True(SkyTempleNpcForcedMove.TryApplyPrepass(x.Child,out var detail),detail);
        var moved=new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ());h.World.Despawn(x.Source);
        x.Template.ApplyEffect(x.Child);
        Assert.False(x.Child.IsInSuccessEffects(1));Assert.False(x.Target.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));
        Assert.Equal(moved,new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ()));Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ActualParentKeepsItsSuccessfulOutcomeWhenOwnedChildHasUnknownCurrentGeometry()
    {
        RequireAssets();using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(ProfilePath));
        using var originalGeo=new GeoScope();using var h=BossAiHarness.For(320050000).WithWorldSize(512).Build();
        // The actual harness map has no15/16/17 registration. Raw verified assets
        // alone are not a valid current scene, and the child must fail visibly.
        var assets=SkyTempleNpcNavigationAssets.Load(Environment.GetEnvironmentVariable(SkyTempleNpcNavigationAssets.RootEnvironment)??MapRoot);
        using var configured=SkyTempleNpcNavigationAssets.InstallForTests(assets);
        var x=OwnedChild(h,false,false);var parent=x.Parent;
        var before=new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ());
        parent.SetSpellStatus(SpellStatus.BLOCK);parent.SetTargetLoc(before.X,before.Y,before.Z);
        var primary=parent.GetSkillTemplate().GetEffectTemplate(1)!;primary.CalculateSubEffect(parent);
        Assert.True(parent.IsInSuccessEffects(1));Assert.Equal(100,parent.GetReserveds(1).GetValue());
        Assert.Null(parent.GetSubEffect());Assert.Equal(SpellStatus.BLOCK,parent.GetSpellStatus());
        Assert.Equal(before,new RetailSearchPoint(parent.GetTargetX(),parent.GetTargetY(),parent.GetTargetZ()));
        Assert.Equal(before,new RetailSearchPoint(x.Target.GetX(),x.Target.GetY(),x.Target.GetZ()));
        Assert.False(x.Target.GetEffectController().IsAbnormalSet(AbnormalState.STAGGER));Assert.Empty(h.Clock.Faults);
    }
}
