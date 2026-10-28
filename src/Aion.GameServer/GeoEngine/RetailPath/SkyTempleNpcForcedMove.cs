using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using static Aion.GameServer.GeoEngine.RetailPath.RetailCollisionMath;

namespace Aion.GameServer.GeoEngine.RetailPath;

internal enum SkyTempleForcedMoveStatus { Endpoint, NoDisplacement, Unsupported }
internal readonly record struct SkyTemplePlayerBody(float Front,float Side,float Upper);
internal sealed record SkyTempleForcedMoveResult(SkyTempleForcedMoveStatus Status,RetailSearchPoint? Endpoint,
    IReadOnlyList<RetailCollisionQueryResult> Queries,string? Detail)
{internal bool Supported=>Status!=SkyTempleForcedMoveStatus.Unsupported&&Endpoint.HasValue;}

/// <summary>Scoped17039-owned Stagger, with actual4.8 Player body/current pose and transport.
/// Main6904a0 ordinary User ground/glide candidate order and its complete1937-byte
/// geometry helpers are source/PE-bound. The verified typed retail map queries supply
/// computed geometry; this managed mapping is not an observed native actor/scene.</summary>
internal static class SkyTempleNpcForcedMove
{
    private sealed record Receipt(Npc Source,Player Target,long SourceLife,long TargetLife,string Generation,
        int Instance,string SceneRevision,RetailSearchPoint Endpoint);
    private static readonly ConditionalWeakTable<Effect,Receipt> receipts=new();
    private sealed class Prepass
    {
        internal bool Attempted,Committed;
        internal Receipt? Receipt;
    }
    private static readonly ConditionalWeakTable<Effect,Prepass> prepasses=new();
    private static readonly ILogger Log=AionLog.For(nameof(SkyTempleNpcForcedMove));
    internal static bool Owns(Effect effect)=>SkyTempleNpcSkillCombat.HasOwnedChild(effect,17039,8217);

    /// <summary>The authentic long-delay17039 producer calls this at its existing
    /// prepass event. Recompute from the actual current Player pose, then emit only
    /// movement. STAGGER belongs to the later primary-hit child application.</summary>
    internal static bool TryApplyPrepass(Effect effect,out string? detail)
    {
        detail=null;
        if(!Owns(effect)){detail="Stagger prepass is not privately source-owned";return false;}
        var state=prepasses.GetValue(effect,_=>new());
        lock(state)
        {
            if(state.Attempted)return CanApplyAtHit(effect,out _,out _,out detail);
            state.Attempted=true;
            if(!effect.IsInSuccessEffects(1))return Reject("Stagger child has no successful current secondary",out detail);
            if(!TryCalculate(effect,out var calculated)||!calculated.Supported)return Reject(calculated.Detail,out detail);
            if(!CanCommit(effect,out var endpoint,out detail))return Reject(detail,out detail);
            var target=(Player)effect.GetEffected();
            // Preserve the existing4.8 movement/flight owner. Current geometry was
            // sampled before stopping glide; stop callbacks can retire either actor.
            target.GetFlyController().OnStopGliding();target.GetController().OnStopMove();
            if(!effect.IsInSuccessEffects(1))return Reject("Stagger child was removed during stop callbacks",out detail);
            if(!CanCommit(effect,out endpoint,out detail))return Reject(detail,out detail);
            effect.SetTargetLoc(endpoint.X,endpoint.Y,endpoint.Z);
            Aion.GameServer.World.World.GetInstance().UpdatePosition(target,endpoint.X,endpoint.Y,endpoint.Z,target.GetHeading());
            if(!CanCommit(effect,out _,out detail)||new RetailSearchPoint(target.GetX(),target.GetY(),target.GetZ())!=endpoint)
                return Reject(detail??"Stagger prepass world membership declined position",out detail);
            var receipt=receipts.GetValue(effect,_=>throw new InvalidOperationException("Prepass geometry receipt was lost"));
            state.Receipt=receipt;
            state.Committed=true;
            PacketSendUtility.BroadcastPacketAndReceive(target,new SM_FORCED_MOVE(effect.GetEffector(),target.GetObjectId(),endpoint.X,endpoint.Y,endpoint.Z));
            Log.LogInformation("SkyTemple Stagger prepass source={Source} target={Target} map={Map} instance={Instance} endpoint={Endpoint} scene={Scene}",
                effect.GetEffector().GetObjectId(),target.GetObjectId(),target.GetWorldId(),target.GetInstanceId(),endpoint,receipt.SceneRevision);
            return true;

            bool Reject(string? reason,out string? failure)
            {
                failure=reason??"Stagger prepass geometry is unsupported";
                if(effect.GetSkillTemplate().GetEffectTemplate(1) is StaggerEffect template)effect.GetSuccessEffects().Remove(template);
                Log.LogWarning("SkyTemple Stagger prepass rejected source={Source} target={Target}: {Reason}",
                    effect.GetEffector().GetObjectId(),effect.GetEffected().GetObjectId(),failure);
                return false;
            }
        }
    }

    internal static bool CanApplyAtHit(Effect effect,out RetailSearchPoint endpoint,out bool prepassed,out string? detail)
    {
        endpoint=default;prepassed=false;detail=null;
        if(!prepasses.TryGetValue(effect,out var state))return CanCommit(effect,out endpoint,out detail);
        lock(state)
        {
            prepassed=true;
            if(!effect.IsInSuccessEffects(1)||!state.Committed||state.Receipt is not { } r||!SkyTempleNpcSkillCombat.IsOwnedChild(effect,17039,8217)||
                !Current(r.Source,r.Target)||r.Source.GetController().RetailLifeEpoch!=r.SourceLife||
                r.Target.GetController().RetailLifeEpoch!=r.TargetLife||r.Target.GetInstanceId()!=r.Instance||
                !SkyTempleNpcProfileCatalog.TryGet(r.Source,out var profile)||profile.Generation!=r.Generation)
            {detail="Stagger prepass/current lifetime retired or never committed";return false;}
            // The movement already happened. Current Player motion and dynamic scene
            // changes do not replay it at HP; only current child ownership is consumed.
            endpoint=r.Endpoint;return true;
        }
    }

    internal static bool TryCalculate(Effect effect,out SkyTempleForcedMoveResult result)
    {
        result=new(SkyTempleForcedMoveStatus.Unsupported,null,[],"not-owned-Stagger");
        if(!Owns(effect))return false;
        try
        {
            if(!SkyTempleNpcSkillCombat.IsOwnedChild(effect,17039,8217)||
                effect.GetEffector() is not Npc source||effect.GetEffected() is not Player target||!Current(source,target)||
                !SkyTempleNpcProfileCatalog.TryGet(source,out var profile))
            {result=result with{Detail="Stagger source/Player lifetime or instance changed"};return true;}
            if(target.IsInFlyingState())
            {result=result with{Detail="Free-flight Stagger is outside SkyTemple's non-FLY map domain"};return true;}
            var assets=SkyTempleNpcNavigationAssets.For(source);var scene=assets.CurrentScene(source);
            var bounds=target.GetObjectTemplate().GetBoundRadius();
            result=Calculate(scene,new(source.GetX(),source.GetY(),source.GetZ()),new(target.GetX(),target.GetY(),target.GetZ()),
                new(bounds.GetFront(),bounds.GetSide(),bounds.GetUpper()),target.IsInGlidingState(),SkyTempleNpcNavigationAssets.QueryLimits);
            if(result.Supported)
                lock(receipts)
                {
                    receipts.Remove(effect);receipts.Add(effect,new(source,target,source.GetController().RetailLifeEpoch,
                        target.GetController().RetailLifeEpoch,profile.Generation,target.GetInstanceId(),scene.Declaration.SceneRevision,result.Endpoint!.Value));
                }
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
        {result=new(SkyTempleForcedMoveStatus.Unsupported,null,[],ex.Message);}
        return true;
    }

    // A capability and the calculated endpoint are separate. A retired capability or
    // changed current scene never falls back to the global legacy collision owner.
    internal static bool CanCommit(Effect effect,out RetailSearchPoint endpoint,out string? detail)
    {
        endpoint=default;detail=null;
        if(!SkyTempleNpcSkillCombat.IsOwnedChild(effect,17039,8217)||!receipts.TryGetValue(effect,out var r)||!Current(r.Source,r.Target)||
            r.Target.IsInFlyingState()||
            r.Source.GetController().RetailLifeEpoch!=r.SourceLife||r.Target.GetController().RetailLifeEpoch!=r.TargetLife||
            r.Target.GetInstanceId()!=r.Instance||!SkyTempleNpcProfileCatalog.TryGet(r.Source,out var profile)||profile.Generation!=r.Generation)
        {detail="Stagger endpoint/current owner retired";return false;}
        try
        {
            var scene=SkyTempleNpcNavigationAssets.For(r.Source).CurrentScene(r.Source);
            if(scene.Declaration.SceneRevision!=r.SceneRevision)
            {detail="Stagger dynamic scene changed before commit";return false;}
            var bounds=r.Target.GetObjectTemplate().GetBoundRadius();
            var current=new RetailSearchPoint(r.Target.GetX(),r.Target.GetY(),r.Target.GetZ());
            List<RetailCollisionQueryResult> queries=[];
            if(!UserClear(scene,current,r.Endpoint,new(bounds.GetFront(),bounds.GetSide(),bounds.GetUpper()),
                SkyTempleNpcNavigationAssets.QueryLimits,queries))
            {detail="Stagger endpoint no longer has current Player clearance";return false;}
            endpoint=r.Endpoint;return true;
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException or RetailCollisionStop)
        {detail=ex.Message;return false;}
    }

    internal static SkyTempleForcedMoveResult Calculate(RetailCollisionScene scene,RetailSearchPoint source,RetailSearchPoint target,
        SkyTemplePlayerBody body,bool gliding,RetailCollisionQueryLimits limits)
    {
        List<RetailCollisionQueryResult> queries=[];
        try
        {
            Point(source);Point(target);Body(body);
            if(!scene.Bounds.Contains(source)||!scene.Bounds.Contains(target))throw new InvalidDataException("Stagger pose outside loaded scene");
            var direction=Unit(Subtract(target,source));float distance=2;
            for(int attempt=0;attempt<3;attempt++)
            {
                var candidate=Along(target,direction,distance);
                if(!gliding)candidate=candidate with{Z=Add(target.Z,3)};
                var projected=Query(RetailCollisionQuery.ProjectGround(scene,scene.Stamp,candidate,limits),queries,limits);
                if(projected.NativeResult==1&&projected.Point is null)throw new InvalidDataException("Ground result has no calculated point");
                if(gliding)
                {
                    if(projected.NativeResult==1&&candidate.Z<projected.Point!.Value.Z)candidate=candidate with{Z=projected.Point.Value.Z};
                }
                else
                {
                    if(projected.NativeResult==0||projected.Point!.Value.Z<.001f)
                    {distance=Multiply(System.MathF.Min(Multiply(attempt+3,.25f),1),2);continue;}
                    candidate=projected.Point.Value;
                }
                if(Squared(target,candidate)>.1f&&UserClear(scene,target,candidate,body,limits,queries))
                    return new(SkyTempleForcedMoveStatus.Endpoint,candidate,queries.AsReadOnly(),null);
                distance=Multiply(System.MathF.Min(Multiply(attempt+3,.25f),1),2);
            }
            return new(SkyTempleForcedMoveStatus.NoDisplacement,target,queries.AsReadOnly(),"Native ordinary User candidates all blocked or too short");
        }
        catch(Exception ex) when(ex is InvalidDataException or ArgumentException or RetailCollisionStop)
        {return new(SkyTempleForcedMoveStatus.Unsupported,null,queries.AsReadOnly(),ex.Message);}
    }

    private static void Body(SkyTemplePlayerBody b)
    {F(b.Front);F(b.Side);F(b.Upper);if(b.Front<0||b.Side<0||b.Upper<0)throw new InvalidDataException("Negative current Player body extent");}
    private static RetailSearchPoint Unit(RetailSearchPoint v)
    {
        float length=F(System.MathF.Sqrt(Dot(v,v)));
        return length>0?Scale(v,Divide(1,length)):v;
    }
    private static bool UserClear(RetailCollisionScene scene,RetailSearchPoint startPoint,RetailSearchPoint to,
        SkyTemplePlayerBody body,RetailCollisionQueryLimits limits,List<RetailCollisionQueryResult> queries)
    {
        Point(startPoint);Point(to);Body(body);
        if(Squared(startPoint,to)<Multiply(.001f,.001f))return true;
        if(!scene.Bounds.Contains(startPoint)||!scene.Bounds.Contains(to))return false;
        float extent=System.MathF.Max(body.Front,body.Side);
        // Maind7dff0's forward body check always lifts1.5; its User reverse child
        // d7dd80 separately liftsmax(current upper,1.5). These are not NPC bounds.
        var a=startPoint with{Z=Add(startPoint.Z,1.5f)};var b=to with{Z=Add(to.Z,1.5f)};
        if(Blocked(a,Along(b,Unit(Subtract(b,a)),extent)))return false;
        float height=System.MathF.Max(body.Upper,1.5f);
        a=to with{Z=Add(to.Z,height)};b=startPoint with{Z=Add(startPoint.Z,height)};
        if(Blocked(a,Along(b,Unit(Subtract(b,a)),extent)))return false;
        return !Blocked(to,startPoint);
        bool Blocked(RetailSearchPoint start,RetailSearchPoint end)
        {
            if(!scene.Bounds.Contains(start)||!scene.Bounds.Contains(end))throw new InvalidDataException("Player body probe leaves loaded scene");
            return Query(RetailCollisionQuery.SegmentBlocked(scene,scene.Stamp,start,end,0,0,
                BitConverter.UInt32BitsToSingle(0x3d016f00),limits),queries,limits).NativeResult==1;
        }
    }
    private static RetailCollisionQueryResult Query(RetailCollisionQueryResult q,List<RetailCollisionQueryResult> queries,RetailCollisionQueryLimits limits)
    {
        queries.Add(q);
        if(queries.Count>16||queries.Sum(x=>(long)x.Triangles)>limits.MaxTriangles||queries.Sum(x=>(long)x.Trace.Count)>limits.MaxTraceEntries)
            throw new InvalidDataException("Stagger aggregate geometry budget exceeded");
        if(q.Status!=RetailCollisionStatus.Computed||q.NativeResult is null)
            throw new InvalidDataException(q.Detail??"Stagger geometry: "+q.Status);
        return q;
    }
    private static bool Current(Npc source,Player target)=>source.IsSpawned()&&!source.IsDead()&&target.IsSpawned()&&!target.IsDead()&&
        !target.GetLifeStats().IsAboutToDie()&&source.GetWorldId()==320050000&&target.GetWorldId()==320050000&&source.GetInstanceId()==target.GetInstanceId();
}
