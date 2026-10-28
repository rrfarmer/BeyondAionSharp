using System.Collections.Immutable;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline subtotal for observed, source-matched equipment. This is
/// not a complete equipped-defense basis or a live player snapshot.</summary>
internal static class RetailNpcPlayerArmorDefenseObservation
{
    internal enum IssueKind { InvalidItem, NotReviewedArmor, UnsupportedItemState, MissingNativeContext,
        SourceMismatch, UnreviewedEnchant, PermitMismatch }
    internal readonly record struct Context(sbyte? LevelAdjustment,ushort? HighLevelFloor,bool? RevisionEnabled,
        RetailNpcArmorDefenseSource.Restriction? Restriction);
    internal sealed record Entry(int ObjectId,int ItemId,long Slot,int RequiredLevel,int RawDefense,int EnchantDefense,int RevisedDefense);
    internal sealed record Issue(int ObjectId,int ItemId,IssueKind Kind);
    internal sealed record Report(int RevisedArmorSubtotal,ImmutableArray<Entry> Entries,ImmutableArray<Issue> Issues)
    {
        // Other equipment contributions, bonuses, live eligibility, effects
        // and actor mutation are not established by this subtotal.
        internal bool GameplayReady=>false;
        internal string Limitation=>"Reviewed equipment subtotal only; eligibility, other equipment, bonuses, active effects and snapshot lifetime are unverified.";
    }

    internal static Report ProjectForOfflineFixture(RetailNpcCurseEquipmentObservation.Snapshot snapshot,
        RetailNpcArmorDefenseSource source,Func<RetailNpcCurseEquipmentObservation.ItemObservation,Context?> context,
        RetailNpcShieldDefenseSource? shields=null,RetailNpcOtherEquipmentDefenseSource? otherEquipment=null,
        RetailNpcWeaponDefenseSource? weapons=null,
        Func<RetailNpcCurseEquipmentObservation.ItemObservation,RetailNpcCurseEquipmentObservation.FusionProjectionContext?>? fusionContext=null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(context);
        if(snapshot.NativeClass is <0 or >=17 ||snapshot.PlayerLevel is <0 or >ushort.MaxValue||snapshot.Items.IsDefault)
            throw new ArgumentException("Invalid equipment observation",nameof(snapshot));
        var entries=ImmutableArray.CreateBuilder<Entry>();var issues=ImmutableArray.CreateBuilder<Issue>();
        var objects=new HashSet<int>();int total=0;
        foreach(var row in snapshot.Items)
        {
            void Reject(IssueKind kind)=>issues.Add(new(row.ObjectId,row.ItemId,kind));
            bool sharedTwoHand=false;
            var same=snapshot.Items.Where(x=>x.ObjectId==row.ObjectId).ToArray();
            if(same.Length>1 &&weapons is not null)
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
            if(row.HasFusion)
            {
                if(!sharedTwoHand||weapons is null||row.WeaponHp is not { } mainWeapon
                    ||row.Fusion is not {WeaponStatus:RetailNpcCurseWeaponHpSource.Failure.None,Weapon.IsRetailTwoHanded:true,BonusStatus:RetailNpcCurseItemBonusSource.Failure.None,Bonus:{ }} fused
                    ||fused.ItemId!=fused.Weapon.ItemId||fused.ItemId!=fused.Bonus.ItemId||fused.Weapon.HostGroup!=mainWeapon.HostGroup
                    ||!string.Equals(fused.HostSourceName,fused.Weapon.Name,StringComparison.OrdinalIgnoreCase)
                    ||weapons.Lookup(fused.Weapon,0)!=RetailNpcWeaponDefenseSource.Failure.None
                    ||fusionContext?.Invoke(row) is not {LinkedItemActive:true,StandardBonusPath:true,AdditionalEffectsAbsent:true,SecondaryChargePoints:>=0 and <=1000000})
                {Reject(IssueKind.UnsupportedItemState);continue;}
            }
            else if(row.Fusion is not null){Reject(IssueKind.UnsupportedItemState);continue;}
            if(row.HasRandomBonus||row.HasSocketOrIdian
                ||row.EnchantBonus!=0||row.Tempering!=0||row.ChargePoints is <0 or >1000000)
            {Reject(IssueKind.UnsupportedItemState);continue;}
            if(weapons is not null &&row.WeaponHp is { } weapon
                &&row.Status is RetailNpcCurseEquipmentObservation.Status.ReviewedWeaponHpOnly or RetailNpcCurseEquipmentObservation.Status.SkippedRetailOffhandWeapon)
            {
                bool main=row.Status==RetailNpcCurseEquipmentObservation.Status.ReviewedWeaponHpOnly;
                if(row.OccupiedSlot!=(long)(main?ItemSlot.MAIN_HAND:ItemSlot.SUB_HAND)
                    ||weapon.IsRetailTwoHanded!=sharedTwoHand)
                {Reject(IssueKind.InvalidItem);continue;}
                var weaponResult=weapons.Lookup(weapon,row.EnchantLevel);
                if(weaponResult!=RetailNpcWeaponDefenseSource.Failure.None)
                {Reject(weaponResult==RetailNpcWeaponDefenseSource.Failure.UnreviewedEnchant?IssueKind.UnreviewedEnchant:IssueKind.SourceMismatch);continue;}
                entries.Add(new(row.ObjectId,row.ItemId,row.OccupiedSlot,0,0,0,0));
                continue;
            }
            if(row.Status==RetailNpcCurseEquipmentObservation.Status.ReviewedOtherEquipmentHpOnly
                &&row.OtherHp is {HostGroup:"SHIELD"} shieldHp &&shields is not null)
            {
                if(row.OccupiedSlot!=ItemSlot.SUB_HAND.GetSlotIdMask())
                {Reject(IssueKind.InvalidItem);continue;}
                var shieldResult=shields.Lookup(row.ItemId,row.HostSourceName,row.EnchantLevel,out var shield);
                if(shieldResult!=RetailNpcShieldDefenseSource.Failure.None ||shield is null)
                {Reject(shieldResult==RetailNpcShieldDefenseSource.Failure.UnreviewedEnchant?IssueKind.UnreviewedEnchant:IssueKind.SourceMismatch);continue;}
                if(shieldHp.ItemId!=shield.Id||shieldHp.HostMaxEnchant!=shield.HostMaxEnchant
                    ||!string.Equals(shieldHp.Name,shield.Name,StringComparison.OrdinalIgnoreCase)
                    ||!shield.TryGetClassPermit(snapshot.NativeClass,out byte shieldPermit,out _))
                {Reject(IssueKind.SourceMismatch);continue;}
                if(context(row) is not {RevisionEnabled:{ } shieldRevision,Restriction:{ } shieldRestriction} shieldInputs
                    ||shieldPermit!=0 && (shieldInputs.LevelAdjustment is null ||shieldInputs.LevelAdjustment!=0 && shieldInputs.HighLevelFloor is null))
                {Reject(IssueKind.MissingNativeContext);continue;}
                int shieldRequired=shieldPermit==0?0:RetailNpcCurseArmorSource.AdjustClassPermit(shieldPermit,shieldInputs.LevelAdjustment!.Value,shieldInputs.HighLevelFloor.GetValueOrDefault());
                if(shieldRequired is <0 or >ushort.MaxValue ||shieldRestriction is not (RetailNpcArmorDefenseSource.Restriction.Computed
                    or RetailNpcArmorDefenseSource.Restriction.Original or RetailNpcArmorDefenseSource.Restriction.Zero))
                {Reject(IssueKind.MissingNativeContext);continue;}
                int shieldRevised=RetailNpcShieldDefenseSource.RevisedPhysicalDefense(shield,(ushort)shieldRequired,(ushort)snapshot.PlayerLevel,shieldRevision,shieldRestriction);
                total=unchecked(total+shieldRevised);
                entries.Add(new(row.ObjectId,row.ItemId,row.OccupiedSlot,shieldRequired,shield.RawPhysicalDefense,0,shieldRevised));
                continue;
            }
            if(row.Status==RetailNpcCurseEquipmentObservation.Status.ReviewedOtherEquipmentHpOnly
                &&row.OtherHp is {HostGroup:not "SHIELD"} otherHp &&otherEquipment is not null)
            {
                long allowed=otherHp.HostGroup switch {"HEAD"=>(long)ItemSlot.HELMET,"EARRING"=>(long)ItemSlot.EARRING_RIGHT_OR_LEFT,
                    "RING"=>(long)ItemSlot.RING_RIGHT_OR_LEFT,"NECKLACE"=>(long)ItemSlot.NECKLACE,"BELT"=>(long)ItemSlot.WAIST,_=>0};
                if((row.OccupiedSlot&allowed)==0)
                {Reject(IssueKind.InvalidItem);continue;}
                var otherResult=otherEquipment.Lookup(row.ItemId,row.HostSourceName,row.EnchantLevel,out var other);
                if(otherResult!=RetailNpcOtherEquipmentDefenseSource.Failure.None ||other is null)
                {Reject(otherResult==RetailNpcOtherEquipmentDefenseSource.Failure.UnreviewedEnchant?IssueKind.UnreviewedEnchant:IssueKind.SourceMismatch);continue;}
                if(otherHp.ItemId!=other.Id||otherHp.HostGroup!=other.HostGroup||otherHp.HostMaxEnchant!=other.HostMaxEnchant
                    ||!string.Equals(otherHp.Name,other.Name,StringComparison.OrdinalIgnoreCase)
                    ||!other.TryGetClassPermit(snapshot.NativeClass,out byte otherPermit,out _))
                {Reject(IssueKind.SourceMismatch);continue;}
                if(context(row) is not {RevisionEnabled:{ } otherRevision,Restriction:{ } otherRestriction} otherInputs
                    ||otherPermit!=0 && (otherInputs.LevelAdjustment is null ||otherInputs.LevelAdjustment!=0 && otherInputs.HighLevelFloor is null))
                {Reject(IssueKind.MissingNativeContext);continue;}
                int otherRequired=otherPermit==0?0:RetailNpcCurseArmorSource.AdjustClassPermit(otherPermit,otherInputs.LevelAdjustment!.Value,otherInputs.HighLevelFloor.GetValueOrDefault());
                if(otherRequired is <0 or >ushort.MaxValue ||otherRestriction is not (RetailNpcArmorDefenseSource.Restriction.Computed
                    or RetailNpcArmorDefenseSource.Restriction.Original or RetailNpcArmorDefenseSource.Restriction.Zero))
                {Reject(IssueKind.MissingNativeContext);continue;}
                int otherRevised=RetailNpcOtherEquipmentDefenseSource.RevisedPhysicalDefense(other,(ushort)otherRequired,(ushort)snapshot.PlayerLevel,otherRevision,otherRestriction);
                total=unchecked(total+otherRevised);
                entries.Add(new(row.ObjectId,row.ItemId,row.OccupiedSlot,otherRequired,other.RawPhysicalDefense,0,otherRevised));
                continue;
            }
            if(row.Status!=RetailNpcCurseEquipmentObservation.Status.ReviewedInputsOnly||row.Source is not { } hp)
            {Reject(IssueKind.NotReviewedArmor);continue;}
            var result=source.Lookup(row.ItemId,row.HostSourceName,row.EnchantLevel,out var defense);
            if(result!=RetailNpcArmorDefenseSource.Failure.None ||defense is null)
            {Reject(result==RetailNpcArmorDefenseSource.Failure.UnreviewedEnchant?IssueKind.UnreviewedEnchant:IssueKind.SourceMismatch);continue;}
            if(hp.ItemId!=defense.Id||!string.Equals(hp.Name,defense.Name,StringComparison.OrdinalIgnoreCase)
                ||hp.Table!=defense.Table||hp.HostMaxEnchant!=defense.HostMaxEnchant||hp.RetailMaxEnchant!=defense.RetailMaxEnchant)
            {Reject(IssueKind.SourceMismatch);continue;}
            if(!hp.TryGetClassPermit(snapshot.NativeClass,out byte permit,out bool present)
                ||row.RawClassPermit!=permit||row.ClassPermitPresent!=present)
            {Reject(IssueKind.PermitMismatch);continue;}
            if(context(row) is not {RevisionEnabled:{ } revision,Restriction:{ } restriction} inputs
                ||permit!=0 && (inputs.LevelAdjustment is null ||inputs.LevelAdjustment!=0 && inputs.HighLevelFloor is null))
            {Reject(IssueKind.MissingNativeContext);continue;}
            int required=permit==0?0:RetailNpcCurseArmorSource.AdjustClassPermit(permit,inputs.LevelAdjustment!.Value,inputs.HighLevelFloor.GetValueOrDefault());
            if(required is <0 or >ushort.MaxValue ||restriction is not (RetailNpcArmorDefenseSource.Restriction.Computed
                or RetailNpcArmorDefenseSource.Restriction.Original or RetailNpcArmorDefenseSource.Restriction.Zero))
            {Reject(IssueKind.MissingNativeContext);continue;}
            int revised=RetailNpcArmorDefenseSource.RevisedPhysicalDefense(defense,(ushort)required,(ushort)snapshot.PlayerLevel,revision,restriction);
            total=unchecked(total+revised);
            entries.Add(new(row.ObjectId,row.ItemId,row.OccupiedSlot,required,defense.RawPhysicalDefense,defense.EnchantPhysicalDefense,revised));
        }
        return new(total,entries.ToImmutable(),issues.ToImmutable());
    }
}
