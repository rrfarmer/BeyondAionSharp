using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Joins reviewed 5.8 resistance and resource inputs per vampire target.
/// Ordinary admission is limited to the two selected single-target skills.</summary>
internal sealed class RetailNpcVampirePlayerInputs
{
    internal sealed record Sources(RetailNpcPlayerCurseAbsoluteCapture.Catalog Absolutes,
        RetailNpcPlayerCurseItemCapture.Catalog Items,RetailNpcPlayerCurseItemCapture.DirectCatalog Direct,
        RetailNpcPlayerResistanceCapture.Catalog Buffs);

    private readonly RetailNpcPlayerCurseAbsoluteCapture.Catalog absolutes;
    private readonly RetailNpcPlayerCurseItemCapture.Catalog items;
    private readonly RetailNpcPlayerCurseItemCapture.DirectCatalog direct;
    private readonly RetailNpcPlayerResistanceCapture.Catalog buffs;
    private Sources? selectedSource;
    private Player? capturedTarget;
    private RetailNpcPlayerResistanceSnapshot.Receipt? resistance;
    private int attempted;
    internal RetailNpcCurseResourceOwner.Plan? PreparedPlan {get;private set;}

    internal RetailNpcVampirePlayerInputs(Sources sources)
        :this(sources.Absolutes,sources.Items,sources.Direct,sources.Buffs) {selectedSource=sources;}

    internal RetailNpcVampirePlayerInputs(RetailNpcPlayerCurseAbsoluteCapture.Catalog absolutes,
        RetailNpcPlayerCurseItemCapture.Catalog items,RetailNpcPlayerCurseItemCapture.DirectCatalog direct,
        RetailNpcPlayerResistanceCapture.Catalog buffs)
    {
        this.absolutes=absolutes??throw new ArgumentNullException(nameof(absolutes));
        this.items=items??throw new ArgumentNullException(nameof(items));
        this.direct=direct??throw new ArgumentNullException(nameof(direct));
        this.buffs=buffs??throw new ArgumentNullException(nameof(buffs));
    }

    private bool SourceCurrent()=>selectedSource is null||ReferenceEquals(RetailNpcStatCatalog.VampirePlayerSources,selectedSource);

    // Each area recipient needs its own single-use resistance receipt and
    // resource plan. Source catalogs may be shared; captured actor state may not.
    internal RetailNpcVampirePlayerInputs NewTargetForOfflineFixture()
        => selectedSource is { } sources ? new(sources) : new(absolutes,items,direct,buffs);

    // Preflight is deliberately non-consuming. The curse decision must capture
    // a fresh receipt after the primary hit, since that hit can end a shield
    // and legitimately change the target's stat mutation stamp.
    internal bool CanCaptureForOfflineFixture(Player target)
        => SourceCurrent()
            && RetailNpcPlayerResistanceSnapshot.TryCaptureReceiptFromMappedHostSeedForOfflineFixture(target,
                absolutes,items,direct,buffs,out var receipt)
            && receipt is not null && SourceCurrent() && receipt.IsCurrent(target);

    internal bool CanPrepareForOfflineFixture(Player target,Effect effect)
        => CanCaptureForOfflineFixture(target)
            && (RetailNpcCurseResourcePlanBuilder.TryPreflightFromCatalog(target,effect,out _)
                || RetailNpcCurseResourceOwner.CanReplaceSelectedForOfflineFixture(target,effect))
            && SourceCurrent();

    internal RetailNpcPlayerResistanceSnapshot.Receipt? Capture(Player target)
    {
        if(Interlocked.Exchange(ref attempted,1)!=0)throw new InvalidOperationException("Vampire input context already used");
        if(!SourceCurrent())return null;
        if(!RetailNpcPlayerResistanceSnapshot.TryCaptureReceiptFromMappedHostSeedForOfflineFixture(target,
            absolutes,items,direct,buffs,out var receipt) ||receipt is null)return null;
        if(!SourceCurrent()||!receipt.IsCurrent(target))return null;
        capturedTarget=target;resistance=receipt;return receipt;
    }

    internal RetailNpcCurseResourceOwner.Plan? Build(Player target,Effect effect)
    {
        if(!SourceCurrent()||!ReferenceEquals(target,capturedTarget)||resistance is not { } captured
            ||!captured.HasEquivalentInputForOfflineFixture(target)||PreparedPlan is not null)return null;
        RetailNpcCurseResourceOwner.Plan? plan;
        bool built=target.GetEquipment().GetEquippedItems().Count==0
            ?RetailNpcCurseResourcePlanBuilder.TryBuildPlainFromCatalog(target,effect,out plan,out _)
            :RetailNpcCurseResourcePlanBuilder.TryBuildFlatEquipmentFromCatalog(target,effect,out plan,out _);
        if(!built||plan is null)return null;
        var before=plan.SnapshotCurrent;var after=plan.SnapshotWithCurse??before;
        bool ResistanceCurrent(Player p)=>SourceCurrent()&&ReferenceEquals(p,target)&&captured.HasEquivalentInputForOfflineFixture(p);
        var joined=plan with {SnapshotCurrent=p=>before(p)&&ResistanceCurrent(p),
            SnapshotWithCurse=p=>after(p)&&ResistanceCurrent(p),SelectedVampireInputs=true};
        if(!joined.SnapshotCurrent(target))return null;
        PreparedPlan=joined;return joined;
    }
}
