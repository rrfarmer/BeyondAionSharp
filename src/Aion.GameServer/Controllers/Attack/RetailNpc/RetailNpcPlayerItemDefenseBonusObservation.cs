using System.Collections.Immutable;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline, partial source-bound item defense bonuses. This does not
/// reserve equipment, authorize items, or capture complete player effects.</summary>
internal static class RetailNpcPlayerItemDefenseBonusObservation
{
    internal enum IssueKind { InvalidItem, UnsupportedItemState, MissingSource, MissingNativeContext, UnreviewedBonus }
    internal readonly record struct Context(sbyte? LevelAdjustment,ushort? HighLevelFloor,
        RetailNpcCurseItemBonusSource.RevisionContext Normal,RetailNpcCurseItemBonusSource.RevisionContext Charge);
    internal sealed record Entry(int ObjectId,int ItemId,int RequiredLevel,int Points);
    internal sealed record Issue(int ObjectId,int ItemId,IssueKind Kind);
    internal sealed record Report(int Points,ImmutableArray<Entry> Entries,ImmutableArray<Issue> Issues)
    {
        internal bool GameplayReady=>false;
    }
    internal static Report ProjectForOfflineFixture(RetailNpcCurseEquipmentObservation.Snapshot snapshot,
        RetailNpcItemDefenseBonusSource source,RetailNpcCurseClassPermitSource permits,
        Func<RetailNpcCurseEquipmentObservation.ItemObservation,Context?> context,
        Func<RetailNpcCurseEquipmentObservation.ItemObservation,RetailNpcCurseEquipmentObservation.FusionProjectionContext?>? fusionContext=null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(permits);ArgumentNullException.ThrowIfNull(context);
        if(snapshot.NativeClass is <0 or >=17||snapshot.PlayerLevel is <0 or >ushort.MaxValue||snapshot.Items.IsDefault)
            throw new ArgumentException("Invalid equipment observation",nameof(snapshot));
        var entries=ImmutableArray.CreateBuilder<Entry>();var issues=ImmutableArray.CreateBuilder<Issue>();
        var objects=new HashSet<int>();int total=0;
        foreach(var row in snapshot.Items)
        {
            void Reject(IssueKind kind)=>issues.Add(new(row.ObjectId,row.ItemId,kind));
            bool sharedTwoHand=false;
            var same=snapshot.Items.Where(x=>x.ObjectId==row.ObjectId).ToArray();
            if(same.Length>1)
            {
                long main=(long)ItemSlot.MAIN_HAND,sub=(long)ItemSlot.SUB_HAND;
                var primaries=same.Where(x=>x.OccupiedSlot==main).ToArray();
                var secondaries=same.Where(x=>x.OccupiedSlot==sub).ToArray();
                if(same.Length!=2||primaries.Length!=1||secondaries.Length!=1)
                {Reject(IssueKind.InvalidItem);continue;}
                var primary=primaries[0];var secondary=secondaries[0];
                if(primary.Status!=RetailNpcCurseEquipmentObservation.Status.ReviewedWeaponHpOnly
                    ||primary.WeaponHp?.IsRetailTwoHanded!=true
                    ||secondary.Status is not (RetailNpcCurseEquipmentObservation.Status.UnknownItem or RetailNpcCurseEquipmentObservation.Status.SourceUnavailable)
                    ||primary.HostSlot!=(main|sub)||secondary.HostSlot!=(main|sub)
                    ||primary.ItemId!=secondary.ItemId||primary.HostSourceName!=secondary.HostSourceName
                    ||primary.EnchantLevel!=secondary.EnchantLevel||primary.EnchantBonus!=secondary.EnchantBonus
                    ||primary.Tempering!=secondary.Tempering||primary.ChargePoints!=secondary.ChargePoints
                    ||primary.HasFusion!=secondary.HasFusion||primary.Fusion!=secondary.Fusion
                    ||primary.HasRandomBonus!=secondary.HasRandomBonus||primary.HasSocketOrIdian!=secondary.HasSocketOrIdian)
                {Reject(IssueKind.InvalidItem);continue;}
                if(row.OccupiedSlot==sub)continue;
                sharedTwoHand=true;
            }
            if(row.ObjectId<=0||!objects.Add(row.ObjectId)||row.OccupiedSlot<=0
                ||(row.OccupiedSlot&(row.OccupiedSlot-1))!=0||!sharedTwoHand&&row.HostSlot!=row.OccupiedSlot)
            {Reject(IssueKind.InvalidItem);continue;}
            bool offhand=row.Status==RetailNpcCurseEquipmentObservation.Status.SkippedRetailOffhandWeapon;
            if(offhand&&(row.OccupiedSlot!=(long)ItemSlot.SUB_HAND||row.EnchantLevel!=0
                ||!snapshot.Items.Any(x=>x.Status==RetailNpcCurseEquipmentObservation.Status.ReviewedWeaponHpOnly
                    &&x.OccupiedSlot==(long)ItemSlot.MAIN_HAND&&x.ObjectId!=row.ObjectId)))
            {Reject(IssueKind.UnsupportedItemState);continue;}
            if(row.HasRandomBonus||row.HasSocketOrIdian
                ||row.EnchantBonus!=0||row.Tempering!=0
                ||row.Status is not (RetailNpcCurseEquipmentObservation.Status.ReviewedInputsOnly
                    or RetailNpcCurseEquipmentObservation.Status.ReviewedWeaponHpOnly
                    or RetailNpcCurseEquipmentObservation.Status.ReviewedOtherEquipmentHpOnly
                    or RetailNpcCurseEquipmentObservation.Status.SkippedRetailOffhandWeapon))
            {Reject(IssueKind.UnsupportedItemState);continue;}
            if(row.ItemBonusStatus!=RetailNpcCurseItemBonusSource.Failure.None||row.ItemBonus is not { } bonus
                ||bonus.ItemId!=row.ItemId||!string.Equals(bonus.Name,row.HostSourceName,StringComparison.OrdinalIgnoreCase)
                ||source.Lookup(bonus,out var selected)!=RetailNpcItemDefenseBonusSource.Failure.None||selected is null
                ||!permits.TryLookup(bonus,snapshot.NativeClass,out byte permit,out _))
            {Reject(IssueKind.MissingSource);continue;}
            if(context(row) is not { } inputs||inputs.Normal.SpecialServerType!=0
                ||inputs.Normal.BonusFlags is not { } normalFlags||(normalFlags&2)!=0
                ||offhand&&normalFlags!=8||sharedTwoHand&&normalFlags!=0x100
                ||row.ChargePoints>0&&(inputs.Charge.BonusFlags is not { } chargeFlags||(chargeFlags&2)!=0)
                ||row.ChargePoints>0&&(offhand||sharedTwoHand)&&inputs.Charge.BonusFlags!=0x200
                ||permit!=0&&(inputs.LevelAdjustment is null||inputs.LevelAdjustment!=0&&inputs.HighLevelFloor is null))
            {Reject(IssueKind.MissingNativeContext);continue;}
            int required=permit==0?0:RetailNpcCurseArmorSource.AdjustClassPermit(permit,inputs.LevelAdjustment!.Value,inputs.HighLevelFloor.GetValueOrDefault());
            if(required is <0 or >ushort.MaxValue)
            {Reject(IssueKind.MissingNativeContext);continue;}
            var status=selected.Resolve(bonus,row.ChargePoints,(ushort)required,(ushort)snapshot.PlayerLevel,inputs.Normal,inputs.Charge,out int points);
            if(status!=RetailNpcItemDefenseBonusSource.Failure.None)
            {Reject(IssueKind.UnreviewedBonus);continue;}
            if(row.HasFusion)
            {
                if(!sharedTwoHand||row.WeaponHp is not { } mainWeapon
                    ||row.Fusion is not {WeaponStatus:RetailNpcCurseWeaponHpSource.Failure.None,Weapon.IsRetailTwoHanded:true,BonusStatus:RetailNpcCurseItemBonusSource.Failure.None,Bonus:{ } fusedBonus} fused
                    ||fused.ItemId!=fused.Weapon.ItemId||fused.ItemId!=fusedBonus.ItemId
                    ||fused.Weapon.HostGroup!=mainWeapon.HostGroup||fusedBonus.Kind!="weapon"
                    ||fusedBonus.HostGroup!=fused.Weapon.HostGroup
                    ||!string.Equals(fused.HostSourceName,fused.Weapon.Name,StringComparison.OrdinalIgnoreCase)
                    ||!string.Equals(fusedBonus.Name,fused.Weapon.Name,StringComparison.OrdinalIgnoreCase)
                    ||source.Lookup(fusedBonus,out var secondary)!=RetailNpcItemDefenseBonusSource.Failure.None||secondary is null
                    ||!permits.TryLookup(fusedBonus,snapshot.NativeClass,out byte fusedPermit,out _))
                {Reject(IssueKind.MissingSource);continue;}
                if(fusionContext?.Invoke(row) is not {LinkedItemActive:true,StandardBonusPath:true,AdditionalEffectsAbsent:true,SecondaryChargePoints:{ } fusedCharge} fusion
                    ||fusion.Normal.SpecialServerType!=inputs.Normal.SpecialServerType||fusion.Normal.ActorPresent!=inputs.Normal.ActorPresent
                    ||fusion.Normal.ActorType!=inputs.Normal.ActorType||fusion.Normal.HighLevelFlag!=inputs.Normal.HighLevelFlag
                    ||fusedPermit!=0&&(fusion.SecondaryLevelAdjustment is null||fusion.SecondaryLevelAdjustment!=0&&fusion.SecondaryHighLevelFloor is null))
                {Reject(IssueKind.MissingNativeContext);continue;}
                int fusedRequired=fusedPermit==0?0:RetailNpcCurseArmorSource.AdjustClassPermit(fusedPermit,fusion.SecondaryLevelAdjustment!.Value,fusion.SecondaryHighLevelFloor.GetValueOrDefault());
                if(fusedRequired is <0 or >ushort.MaxValue)
                {Reject(IssueKind.MissingNativeContext);continue;}
                var levels=new RetailNpcCurseItemBonusSource.FusionLevels((ushort)required,(ushort)fusedRequired,permit,fusedPermit);
                status=secondary.ResolveFused(fusedBonus,fusedCharge,(ushort)snapshot.PlayerLevel,levels,fusion.Normal,fusion.Charge,out int fusedPoints);
                if(status!=RetailNpcItemDefenseBonusSource.Failure.None)
                {Reject(IssueKind.UnreviewedBonus);continue;}
                points=unchecked(points+fusedPoints);
            }
            else if(row.Fusion is not null){Reject(IssueKind.UnsupportedItemState);continue;}
            total=unchecked(total+points);entries.Add(new(row.ObjectId,row.ItemId,required,points));
        }
        return new(total,entries.ToImmutable(),issues.ToImmutable());
    }
}
