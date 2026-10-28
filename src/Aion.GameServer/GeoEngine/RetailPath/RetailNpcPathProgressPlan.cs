namespace Aion.GameServer.GeoEngine.RetailPath;

public sealed record RetailNpcPathProgressContext(RetailNpcPathFollowEpoch Epoch,
    RetailNpcPathFollowEpoch ExpectedEpoch, bool? SerializedOwnership, string NativeNpcSha256, string EvidenceSha256);
public sealed record RetailNpcWorldMoveAcknowledgement(bool? MoveObjectReturnedSuccess,
    string MoveObjectEvidenceSha256);
public sealed record RetailNpcPathProgressResult(RetailNpcPathFollowStatus Status, int? Progress,
    int? PathIndex, IReadOnlyList<RetailNpcPathFollowOperation> Operations, string? Detail)
{
    public bool Success => Status == RetailNpcPathFollowStatus.Planned;
    public bool RuntimeVerified => false;
    public bool AppliesCommands => false;
    public RetailNpcPathProgressContext? Context { get; init; }
    public RetailNpcWorldMoveAcknowledgement? Acknowledgement { get; init; }
}

/// <summary>
/// Complete admitted serialized progress exchange: successful MoveObject permits CAS1->2
/// (WorldMoveNPC2378cf..8fa partial surrounding body); MoveByPath CAS2->0 increments
/// the path index only when old2, otherwise a distinct XCHG0. No send/timer acknowledges movement.
/// Does not implement the surrounding world/interest/callback owner.
/// </summary>
public static class RetailNpcPathProgressPlan
{
    public static RetailNpcPathProgressResult Acknowledge(RetailNpcPathProgressContext context,
        int progress, RetailNpcWorldMoveAcknowledgement acknowledgement)
    {
        var invalid = Validate(context);
        if (invalid is not null) return invalid with { Context = context, Acknowledgement = acknowledgement };
        if (acknowledgement is null || acknowledgement.MoveObjectReturnedSuccess is null ||
            !RetailCollisionMath.Digest(acknowledgement.MoveObjectEvidenceSha256))
            return Reject(RetailNpcPathFollowStatus.UnsupportedProgress, "Explicit successful/failed world move receipt is required; packet send is not an acknowledgement.")
                with { Context = context, Acknowledgement = acknowledgement };
        if (acknowledgement.MoveObjectReturnedSuccess == false)
            return new(RetailNpcPathFollowStatus.Planned, progress, null, Array.Empty<RetailNpcPathFollowOperation>(), null)
                { Context = context, Acknowledgement = acknowledgement };
        int value = progress == 1 ? 2 : progress;
        return new(RetailNpcPathFollowStatus.Planned, value, null,
            new[] { new RetailNpcPathFollowOperation(1, "CompareExchangeProgressExpected1Desired2", progress, value, "0x1402378e6") }, null)
            { Context = context, Acknowledgement = acknowledgement };
    }
    public static RetailNpcPathProgressResult Consume(RetailNpcPathProgressContext context, int progress, int pathIndex)
    {
        var invalid = Validate(context);
        if (invalid is not null) return invalid with { Context = context };
        if (progress == 2 && pathIndex == int.MaxValue)
            return Reject(RetailNpcPathFollowStatus.UnsupportedArithmetic, "Native signed index increment would wrap outside the admitted nonwrapping domain.") with { Context = context };
        var operations = new List<RetailNpcPathFollowOperation>
        { new(1, "CompareExchangeProgressExpected2Desired0", progress, progress == 2 ? 0 : progress, "0x140225f35") };
        if (progress == 2) operations.Add(new(2, "IncrementLocalPathIndex", pathIndex, pathIndex + 1, "0x140225f41"));
        else operations.Add(new(2, "ExchangeProgress0", progress, 0, "0x140225f4a"));
        return new(RetailNpcPathFollowStatus.Planned, 0, progress == 2 ? pathIndex + 1 : pathIndex, operations.AsReadOnly(), null) { Context = context };
    }
    private static RetailNpcPathProgressResult? Validate(RetailNpcPathProgressContext context)
    {
        if (context is null || context.Epoch is null || context.ExpectedEpoch is null ||
            !RetailCollisionMath.Digest(context.EvidenceSha256) || context.SerializedOwnership != true ||
            !string.Equals(context.NativeNpcSha256, RetailGroundPathSearch.SupportedNpcBinarySha256, StringComparison.OrdinalIgnoreCase))
            return Reject(RetailNpcPathFollowStatus.UnsupportedProgress, "Pinned source and explicit serialized progress owner/evidence are required.");
        if (context.Epoch != context.ExpectedEpoch)
            return Reject(RetailNpcPathFollowStatus.StaleSource, "Actor/path/world/instance ownership changed; no atomic commands apply.");
        if (new[] { context.Epoch.ActorIdentity, context.Epoch.ActorRevision, context.Epoch.OwnershipEpoch,
            context.Epoch.PathRevision, context.Epoch.WorldIdentity, context.Epoch.InstanceIdentity,
            context.Epoch.SceneRevision, context.Epoch.AssetRevision }.Any(x => !RetailCollisionMath.Text(x)))
            return Reject(RetailNpcPathFollowStatus.InvalidInput, "All actor/path/world/scene epoch identities are explicit.");
        return null;
    }
    private static RetailNpcPathProgressResult Reject(RetailNpcPathFollowStatus status, string detail) =>
        new(status, null, null, Array.Empty<RetailNpcPathFollowOperation>(), detail);
}
