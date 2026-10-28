using System.Security.Cryptography;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailMovementCalculationStatus
{
    CalculatedApplicationChain, NativeEarlyReturn, IncompleteAtChild,
    UnsupportedConsumedDomain, StaleOwnership, DefensiveLimit, InvalidInput
}
public enum RetailMovementExecutionStatus { Unexecuted, HostEffectsUnexecuted, ObservedPrefix, ReconciliationRequired }
public enum RetailMovementInputOrigin { ConstructedSyntheticWorld, CurrentOwnerDeclaration }
public sealed record RetailMovementOwnership(string ActorIdentity, string ActorLifetime, string RequestRevision,
    string PathRevision, string WorldIdentity, string InstanceIdentity, string SceneRevision,
    string AssetRevision, string MembershipRevision, string ScriptRevision, string TimerRevision,
    string EvidenceSha256, RetailMovementInputOrigin Origin);
public sealed record RetailMovementDomainFailure(RetailMovementCalculationStatus Status, ulong NativeAddress,
    string Boundary, string Detail);
public sealed record RetailMovementLimits
{
    public int MaxOperations { get; init; } = 1_000_000;
    public int MaxSteps { get; init; } = 65_536;
    public int MaxPathPoints { get; init; } = 20_000;
    public int MaxMembershipCells { get; init; } = 65_536;
    public int MaxMembers { get; init; } = 65_536;
    public int MaxTimerEntries { get; init; } = 65_536;
    public int MaxDelayedWrappers { get; init; } = 16_384;
    public RetailSearchDefensiveLimits Search { get; init; } = new();
    public RetailCollisionQueryLimits Geometry { get; init; } = new(65_536, 1_000_000, 1_000_000, 65_536, 65_536);
}

internal sealed class RetailMovementStop(RetailMovementDomainFailure failure) : Exception(failure.Detail)
{ internal RetailMovementDomainFailure Failure { get; } = failure; }
internal static class RetailMovementArithmetic
{
    internal static bool Text(string? s) => !string.IsNullOrWhiteSpace(s);
    internal static bool Digest(string? s) => s?.Length == 64 && s.All(Uri.IsHexDigit);
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    internal static void Stop(RetailMovementCalculationStatus status, ulong address, string boundary, string detail) =>
        throw new RetailMovementStop(new(status, address, boundary, detail));
    internal static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool yes, string boundary, string detail, ulong address = 0) { if (!yes) Stop(RetailMovementCalculationStatus.InvalidInput, address, boundary, detail); }
    internal static float F(float x)
    { if (!float.IsFinite(x)) Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain, 0, "binary32", "A consumed arithmetic value is nonfinite/overflowed."); return x; }
    internal static RetailSearchPoint Point(RetailSearchPoint p) { F(p.X); F(p.Y); F(p.Z); return p; }
    internal static float Add(float a, float b) => F(RetailNpcNativeHeading.Add32(a,b));
    internal static float Sub(float a, float b) => F(RetailNpcNativeHeading.Sub32(a,b));
    internal static float Mul(float a, float b) => F(RetailNpcNativeHeading.Mul32(a,b));
    internal static float Div(float a, float b) => F(RetailNpcNativeHeading.Div32(a,b));
    internal static RetailSearchPoint Minus(RetailSearchPoint a, RetailSearchPoint b) => new(Sub(a.X,b.X),Sub(a.Y,b.Y),Sub(a.Z,b.Z));
    internal static RetailSearchPoint Along(RetailSearchPoint a, RetailSearchPoint d, float t) => new(Add(a.X,Mul(d.X,t)),Add(a.Y,Mul(d.Y,t)),Add(a.Z,Mul(d.Z,t)));
    internal static float Squared(RetailSearchPoint a, RetailSearchPoint b)
    { var d=Minus(a,b); return Add(Add(Mul(d.Y,d.Y),Mul(d.X,d.X)),Mul(d.Z,d.Z)); }
    internal static float Length(RetailSearchPoint a, RetailSearchPoint b) => F(System.MathF.Sqrt(Squared(a,b)));
    internal static bool Near(RetailSearchPoint a, RetailSearchPoint b)
    { var d=Minus(a,b); return d.X<.0001f&&d.X>-.0001f&&d.Y<.0001f&&d.Y>-.0001f&&d.Z<.0001f&&d.Z>-.0001f; }
    internal static int Int(float x, string boundary)
    { F(x); if ((double)x<int.MinValue||(double)x>=2147483648d) Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0,boundary,"Native int32 conversion outside admitted domain."); return (int)x; }
}

/// <summary>
/// Immutable, inactive bridge from individually hashed reader assets to computed policy.
/// A declaration of current ownership is not a live observation or a default empty world.
/// Java GeoService/NpcMoveController were read first; no bot navigation or runtime adapter.
/// </summary>
public sealed class RetailGroundMovementScene
{
    public RetailGroundMovementScene(RetailSearchSourceReceipts sources, RetailCollisionScene collision,
        RetailMovementOwnership ownership, RetailPathSurfaceQueryOptions surfaceOptions, ulong? fieldTreePointer,
        bool nativePathTablesAvailable, RetailNpcMovementFpEnvironment floatingEnvironment)
    {
        ArgumentNullException.ThrowIfNull(sources); ArgumentNullException.ThrowIfNull(collision);
        ArgumentNullException.ThrowIfNull(ownership); ArgumentNullException.ThrowIfNull(surfaceOptions);
        Sources=sources; Collision=collision; Ownership=ownership; SurfaceOptions=surfaceOptions;
        FieldTreePointer=fieldTreePointer; NativePathTablesAvailable=nativePathTablesAvailable;
        FloatingEnvironment=floatingEnvironment;
        Surfaces=new(sources.Path,sources.Terrain,surfaceOptions);
    }
    public RetailSearchSourceReceipts Sources { get; }
    public RetailCollisionScene Collision { get; }
    public RetailMovementOwnership Ownership { get; }
    public RetailPathSurfaceQueryOptions SurfaceOptions { get; }
    public RetailPathSurfaceQuery Surfaces { get; }
    public ulong? FieldTreePointer { get; }
    public bool NativePathTablesAvailable { get; }
    public RetailNpcMovementFpEnvironment FloatingEnvironment { get; }
    public bool RuntimeVerified => false;
    internal void Validate()
    {
        var p=Sources.Path; var id=Sources.PathIdentity; var o=Ownership;
        RetailMovementArithmetic.Require(o.Origin is RetailMovementInputOrigin.ConstructedSyntheticWorld or RetailMovementInputOrigin.CurrentOwnerDeclaration,"ownership-origin","Only the two explicit provenance meanings are admitted; an undefined enum cannot supply current ownership.");
        if(!RetailNpcNativeHeading.Atan2(0,0x3f800000,FloatingEnvironment).Success)
            RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0,"floating-environment","Explicit admitted native scalar FP declaration is required; no host FP-state proof is implied.");
        RetailMovementArithmetic.Require(p is not null&&id is not null&&Sources.Terrain is not null&&Sources.TerrainIdentity is not null,"assets","Path/height/geometry bindings must all be supplied.");
        bool Match(RetailSearchMapAssetIdentity i, ReadOnlySpan<byte> bytes, Guid? guid, int w, int h) =>
            i.BuildGuid==guid&&i.WorldWidthMetres==w&&i.WorldHeightMetres==h&&i.Asset.ByteLength==bytes.Length&&
            string.Equals(i.Asset.Sha256,Convert.ToHexString(SHA256.HashData(bytes)),StringComparison.OrdinalIgnoreCase);
        RetailMovementArithmetic.Require(Match(id,p.RawFile.Span,p.BuildGuid,p.WorldWidthMetres,p.WorldHeightMetres),"path-identity","Actual path bytes differ from their individually bound identity.");
        var terrain=Sources.Terrain!;
        RetailMovementArithmetic.Require(Match(Sources.TerrainIdentity!,terrain.RawFile.Span,terrain.BuildGuid,terrain.WorldWidthMetres,terrain.WorldHeightMetres)&&
            ReferenceEquals(terrain,Collision.Terrain)&&p.BuildGuid==terrain.BuildGuid&&p.BuildGuid==Collision.Prepared.Raw.MeshGuid&&
            p.WorldWidthMetres==terrain.WorldWidthMetres&&p.WorldHeightMetres==terrain.WorldHeightMetres,
            "geometry-identity","Path/terrain/collision GUID, dimensions or owned height instance differ.");
        RetailMovementArithmetic.Require(Sources.NativeNpcBinary.Sha256.Equals(RetailGroundPathSearch.SupportedNpcBinarySha256,StringComparison.OrdinalIgnoreCase)&&
            Sources.NativeNpcBinary.ByteLength==RetailGroundPathSearch.SupportedNpcBinaryBytes,"native-identity","Wrong pinned NPC binary declaration.");
        RetailMovementArithmetic.Require(Sources.CollisionAsset is not null&&Sources.CollisionAsset.ByteLength==Collision.Prepared.Raw.CollisionBytes.Length&&
            string.Equals(Sources.CollisionAsset.Sha256,Collision.Prepared.Raw.CollisionSha256,StringComparison.OrdinalIgnoreCase),"collision-identity","Search and computed scene must bind the same actual collision asset.");
        RetailMovementArithmetic.Require(new[]{o.ActorIdentity,o.ActorLifetime,o.RequestRevision,o.PathRevision,o.WorldIdentity,o.InstanceIdentity,o.SceneRevision,o.AssetRevision,o.MembershipRevision,o.ScriptRevision,o.TimerRevision}.All(RetailMovementArithmetic.Text)&&RetailMovementArithmetic.Digest(o.EvidenceSha256),"ownership","All distinct owner/revision identities are required.");
        RetailMovementArithmetic.Require(Sources.PolicyContext.ActorIdentity==o.ActorIdentity&&RetailMovementArithmetic.Text(Sources.PolicyContext.DynamicContextIdentity)&&RetailMovementArithmetic.Text(Sources.PolicyContext.CollisionContextIdentity)&&RetailMovementArithmetic.Text(Sources.PolicyContext.Evidence),"policy-context","Actor/policy current source declarations must match the owned actor, with explicit dynamic/collision context identities.");
        var c=Collision.Declaration;
        if(o.WorldIdentity!=c.WorldIdentity||o.InstanceIdentity!=c.InstanceIdentity||o.SceneRevision!=c.SceneRevision||o.AssetRevision!=c.AssetRevision)
            RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.StaleOwnership,0,"scene-epoch","Scene and movement owner declarations differ.");
        if((o.Origin==RetailMovementInputOrigin.ConstructedSyntheticWorld)!=c.SyntheticFixture)
            RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.InvalidInput,0,"origin","Movement and geometry declarations must agree on constructed-synthetic versus current-owner provenance; neither declaration establishes runtime proof.");
    }
    internal RetailPathSurfaceRef? Lookup(RetailSearchPoint p)
    {
        var r=Surfaces.EnumerateSurfaceCandidates(p.X,p.Y,p.Z);
        if(r.Status==RetailSurfaceQueryStatus.OutsideWorld)return null;
        if(r.Status!=RetailSurfaceQueryStatus.Complete)
            RetailMovementArithmetic.Stop(r.Status==RetailSurfaceQueryStatus.ResourceLimit?RetailMovementCalculationStatus.DefensiveLimit:RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1400fa8d0,"surface-lookup",r.Detail??r.Status.ToString());
        return r.Candidates.FirstOrDefault(c=>c.Status==RetailSurfaceCandidateStatus.WithinTolerance)?.Surface;
    }
    internal RetailSearchPoint Position(RetailPathSurfaceRef s)
    {
        var h=Surfaces.ReadHeight(s); float z=h.Metres;
        if(h.Status==RetailSurfaceHeightStatus.NoGround&&s.Node.Type is >=4 and <16)z=h.RawHeight.Kind==RetailPathHeightKind.NoGround?-10240f:h.RawHeight.Centimetres/100f;
        else if(h.Status!=RetailSurfaceHeightStatus.Resolved)RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x140224520,"surface-height",h.Status.ToString());
        return RetailMovementArithmetic.Point(new(s.CentreXMetres,s.CentreYMetres,z));
    }
}
