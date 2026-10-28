using System.Collections.Immutable;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Read-only host equipment observation for a potential NPC curse.
/// It does not establish retail equipment eligibility, revision flags, effect
/// ownership or a stable snapshot after the equipment lock is released.</summary>
internal static class RetailNpcCurseEquipmentObservation
{
    internal enum Status { ReviewedInputsOnly, SourceUnavailable, ItemNotEquipped, UnknownItem,
        IdentityMismatch, UnreviewedEnchantLevel, HostTemplateDrift, SlotMismatch, ItemChanged, OutsideRetailStatLoop,
        SkippedRetailOffhandWeapon, ReviewedWeaponHpOnly, ReviewedOtherEquipmentHpOnly }
    internal sealed record FusionObservation(int ItemId,string? HostSourceName,
        RetailNpcCurseWeaponHpSource.Failure? WeaponStatus,RetailNpcCurseWeaponHpSource.Candidate? Weapon,
        RetailNpcCurseItemBonusSource.Failure? BonusStatus,RetailNpcCurseItemBonusSource.Candidate? Bonus);
    internal sealed record ItemObservation(int ObjectId,int ItemId,string? HostSourceName,long HostSlot,long OccupiedSlot,
        int EnchantLevel,int EnchantBonus,int Tempering,Status Status,RetailNpcCurseArmorSource.Candidate? Source,
        RetailNpcCurseWeaponHpSource.Candidate? WeaponHp,RetailNpcCurseOtherEquipmentHpSource.Candidate? OtherHp,
        byte? RawClassPermit,bool? ClassPermitPresent,RetailNpcCurseAuthorizationHpSource.Failure? AuthorizationStatus,int? AuthorizationHp,
        int ChargePoints,bool HasFusion,bool HasRandomBonus,bool HasSocketOrIdian,RetailNpcCurseItemBonusSource.Failure? ItemBonusStatus,RetailNpcCurseItemBonusSource.Candidate? ItemBonus,
        FusionObservation? Fusion=null);
    internal sealed record Snapshot(int NativeClass,int PlayerLevel,ImmutableArray<ItemObservation> Items)
    {
        // Live eligibility, native item state and effect lifecycle are not yet bound.
        internal bool GameplayReady=>false;
    }

    internal enum ProjectionFailure { None, InvalidSnapshot, UnsupportedItem, MissingItemSource,
        UnsupportedItemState, MissingNativeContext, InvalidBonus }
    internal readonly record struct ArmorProjectionContext(sbyte? LevelAdjustment,ushort? HighLevelFloor,
        bool? ArmorRevision,bool? RestrictedBypass,RetailNpcCurseItemBonusSource.RevisionContext Normal,
        RetailNpcCurseItemBonusSource.RevisionContext Charge);
    internal readonly record struct ArmorProjection(int ItemCount,int RevisedArmorHp,
        RetailNpcCurseStatMath.ItemResourceBonus HpBonus,RetailNpcCurseStatMath.ItemResourceBonus MpBonus);
    internal readonly record struct FusionProjectionContext(bool? LinkedItemActive,bool? StandardBonusPath,bool? AdditionalEffectsAbsent,int? SecondaryChargePoints,
        sbyte? SecondaryLevelAdjustment,ushort? SecondaryHighLevelFloor,
        RetailNpcCurseItemBonusSource.RevisionContext Normal,RetailNpcCurseItemBonusSource.RevisionContext Charge);

    /// <summary>Translate a verified ordinary wear slot into the flag passed
    /// by the 5.8 equipment bonus owner. The caller must separately establish
    /// item identity, eligibility, the two-hand classification and lifecycle.
    /// This does not establish that an item was worn by the retail server.</summary>
    internal static bool TryOrdinaryWearBonusFlags(long occupiedSlot,long claimedSlot,bool verifiedTwoHanded,out int flags)
    {
        flags=0;
        long main=ItemSlot.MAIN_HAND.GetSlotIdMask(),sub=ItemSlot.SUB_HAND.GetSlotIdMask();
        if(verifiedTwoHanded)
        {
            if(occupiedSlot!=main ||claimedSlot!=(main|sub))return false;
            flags=0x100;return true;
        }
        // The reviewed mapping covers the 17 ordinary slots through waist.
        // Reject combinations, mismatched claims and separate unreviewed slots.
        if(occupiedSlot<=0 ||occupiedSlot>ItemSlot.WAIST.GetSlotIdMask()
            ||(occupiedSlot&(occupiedSlot-1))!=0 ||claimedSlot!=occupiedSlot)return false;
        flags=occupiedSlot==main?0x10:occupiedSlot==sub?0x8:0x20;
        return true;
    }

    /// <summary>Combine a captured armor-only observation with explicit native
    /// inputs. This is a point-in-time offline projection, not a stable live
    /// player snapshot or a complete curse resource plan. Every observed item
    /// must be understood; other equipment, socket effects and missing inputs
    /// fail visibly rather than contributing zero.</summary>
    internal static bool TryProjectArmor(Snapshot snapshot,Func<ItemObservation,ArmorProjectionContext?> context,
        out ArmorProjection projection,out ProjectionFailure failure,out int failedItemId)
        =>TryProjectEquipment(snapshot,null,context,out projection,out failure,out failedItemId);

    /// <summary>With the separately pinned class-permit source, also project
    /// reviewed main-hand weapons, unfused two-handed weapons, unmodified
    /// separate off-hand weapons, shields and ordinary accessories. A fused
    /// projection additionally requires explicitly supplied native linked-item
    /// state and charge; item sets, sockets and random effects remain unsupported.
    /// A projection is not permission to apply a live curse.</summary>
    internal static bool TryProjectEquipment(Snapshot snapshot,RetailNpcCurseClassPermitSource? permits,
        Func<ItemObservation,ArmorProjectionContext?> context,out ArmorProjection projection,out ProjectionFailure failure,out int failedItemId,
        Func<ItemObservation,FusionProjectionContext?>? fusionContext=null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);ArgumentNullException.ThrowIfNull(context);
        projection=default;failure=ProjectionFailure.InvalidSnapshot;failedItemId=0;
        if(snapshot.NativeClass is <0 or >=17 || snapshot.PlayerLevel is <0 or >ushort.MaxValue ||snapshot.Items.IsDefault)
            return false;
        int count=0,armorHp=0,hpPoints=0,hpPercent=0,mpPoints=0,mpPercent=0;
        var objects=new HashSet<int>();
        RetailNpcCurseItemBonusSource.RevisionContext? actor=null;
        foreach(var row in snapshot.Items)
        {
            failedItemId=row.ItemId;
            var sameObject=snapshot.Items.Where(x=>x.ObjectId==row.ObjectId).ToArray();
            bool sharedTwoHand=false;
            if(sameObject.Length>1)
            {
                long main=ItemSlot.MAIN_HAND.GetSlotIdMask(),sub=ItemSlot.SUB_HAND.GetSlotIdMask();
                var primaries=sameObject.Where(x=>x.OccupiedSlot==main).ToArray();
                var secondaries=sameObject.Where(x=>x.OccupiedSlot==sub).ToArray();
                if(sameObject.Length!=2 ||primaries.Length!=1 ||secondaries.Length!=1)
                {failure=ProjectionFailure.UnsupportedItemState;return false;}
                var primary=primaries[0];var secondary=secondaries[0];
                if(
                    primary.Status!=Status.ReviewedWeaponHpOnly ||primary.WeaponHp?.IsRetailTwoHanded!=true
                    ||secondary.Status is not (Status.UnknownItem or Status.SourceUnavailable)
                    ||primary.HostSlot!=(main|sub) ||secondary.HostSlot!=(main|sub)
                    ||primary.ItemId!=secondary.ItemId ||primary.HostSourceName!=secondary.HostSourceName
                    ||primary.EnchantLevel!=secondary.EnchantLevel ||primary.EnchantBonus!=secondary.EnchantBonus
                    ||primary.Tempering!=secondary.Tempering ||primary.ChargePoints!=secondary.ChargePoints
                    ||primary.HasFusion!=secondary.HasFusion ||primary.HasRandomBonus!=secondary.HasRandomBonus
                    ||primary.Fusion!=secondary.Fusion
                    ||primary.HasSocketOrIdian!=secondary.HasSocketOrIdian)
                {failure=ProjectionFailure.UnsupportedItemState;return false;}
                if(row.OccupiedSlot==sub)continue;
                sharedTwoHand=true;
            }
            else if(row.OccupiedSlot==ItemSlot.MAIN_HAND.GetSlotIdMask()
                &&row.HostSlot==(ItemSlot.MAIN_HAND.GetSlotIdMask()|ItemSlot.SUB_HAND.GetSlotIdMask()))
            {failure=ProjectionFailure.UnsupportedItemState;return false;}
            if(row.ObjectId<=0 || !objects.Add(row.ObjectId)
                ||row.Status is not (Status.ReviewedInputsOnly or Status.ReviewedWeaponHpOnly or Status.ReviewedOtherEquipmentHpOnly or Status.SkippedRetailOffhandWeapon))
            {failure=ProjectionFailure.UnsupportedItem;return false;}
            bool offhand=row.Status==Status.SkippedRetailOffhandWeapon;
            if(offhand && (row.OccupiedSlot!=ItemSlot.SUB_HAND.GetSlotIdMask() ||row.HostSlot!=row.OccupiedSlot
                ||row.EnchantLevel!=0 ||row.Tempering!=0 ||row.EnchantBonus!=0
                ||!snapshot.Items.Any(x=>x.Status==Status.ReviewedWeaponHpOnly &&x.OccupiedSlot==ItemSlot.MAIN_HAND.GetSlotIdMask()
                    &&x.ObjectId!=row.ObjectId)))
            {failure=ProjectionFailure.UnsupportedItemState;return false;}
            if(row.ItemBonus is not { } bonus
                ||row.AuthorizationStatus!=RetailNpcCurseAuthorizationHpSource.Failure.None
                ||row.AuthorizationHp is not { } authorization ||row.ItemBonusStatus!=RetailNpcCurseItemBonusSource.Failure.None
                ||row.Status==Status.ReviewedInputsOnly && row.Source is null
                ||(row.Status==Status.ReviewedWeaponHpOnly ||offhand) && row.WeaponHp is null
                ||row.Status==Status.ReviewedOtherEquipmentHpOnly && row.OtherHp is null)
            {failure=ProjectionFailure.MissingItemSource;return false;}
            if(row.WeaponHp is { } weapon && (bonus.Kind!="weapon" ||weapon.ItemId!=bonus.ItemId
                    ||weapon.HostGroup!=bonus.HostGroup ||!string.Equals(weapon.Name,bonus.Name,StringComparison.OrdinalIgnoreCase))
                ||row.OtherHp is { } other && (bonus.Kind!="other" ||other.ItemId!=bonus.ItemId
                    ||other.HostGroup!=bonus.HostGroup ||!string.Equals(other.Name,bonus.Name,StringComparison.OrdinalIgnoreCase)))
            {failure=ProjectionFailure.MissingItemSource;return false;}
            byte permit;
            if(row.Status==Status.ReviewedInputsOnly)
            {
                if(row.RawClassPermit is not { } observed ||!row.Source!.TryGetClassPermit(snapshot.NativeClass,out permit,out _)
                    ||permit!=observed ||permits is not null && (!permits.TryLookup(bonus,snapshot.NativeClass,out byte match,out _) ||match!=permit))
                {failure=ProjectionFailure.MissingItemSource;return false;}
            }
            else if(permits is null ||!permits.TryLookup(bonus,snapshot.NativeClass,out permit,out _))
            {failure=ProjectionFailure.MissingItemSource;return false;}
            if(row.HasRandomBonus ||row.HasSocketOrIdian)
            {failure=ProjectionFailure.UnsupportedItemState;return false;}
            if(context(row) is not { } inputs ||inputs.Normal.SpecialServerType!=0 ||inputs.ArmorRevision is not { } armorRevision
                ||inputs.RestrictedBypass is not { } restricted)
            {failure=ProjectionFailure.MissingNativeContext;return false;}
            if(actor is { } previous && (previous.SpecialServerType!=inputs.Normal.SpecialServerType
                ||previous.ActorPresent!=inputs.Normal.ActorPresent ||previous.ActorType!=inputs.Normal.ActorType
                ||previous.HighLevelFlag!=inputs.Normal.HighLevelFlag))
            {failure=ProjectionFailure.MissingNativeContext;return false;}
            actor=inputs.Normal;
            int required=permit;
            if(permit!=0)
            {
                if(inputs.LevelAdjustment is not { } adjustment ||adjustment!=0 && inputs.HighLevelFloor is null)
                {failure=ProjectionFailure.MissingNativeContext;return false;}
                required=RetailNpcCurseArmorSource.AdjustClassPermit(permit,adjustment,inputs.HighLevelFloor.GetValueOrDefault());
            }
            if(required is <0 or >ushort.MaxValue)
            {failure=ProjectionFailure.MissingNativeContext;return false;}
            if(inputs.Normal.BonusFlags is not { } normalFlags ||(normalFlags&2)!=0
                ||offhand && normalFlags!=8
                ||sharedTwoHand && normalFlags!=0x100
                ||row.ChargePoints>0 && (inputs.Charge.BonusFlags is not { } chargeFlags ||(chargeFlags&2)!=0
                    ||(offhand ||sharedTwoHand) && chargeFlags!=0x200))
            {failure=ProjectionFailure.InvalidBonus;return false;}
            var hp=row.Source is { } armorSource
                ?bonus.ResolveArmorSelected(armorSource,snapshot.NativeClass,inputs.LevelAdjustment,inputs.HighLevelFloor,
                    RetailNpcCurseStatMath.Resource.Hp,row.ChargePoints,(ushort)snapshot.PlayerLevel,inputs.Normal,inputs.Charge,out var hpBonus)
                :bonus.ResolveSelected(RetailNpcCurseStatMath.Resource.Hp,row.ChargePoints,(ushort)required,
                    (ushort)snapshot.PlayerLevel,inputs.Normal,inputs.Charge,out hpBonus);
            var mp=row.Source is { } armorSource2
                ?bonus.ResolveArmorSelected(armorSource2,snapshot.NativeClass,inputs.LevelAdjustment,inputs.HighLevelFloor,
                    RetailNpcCurseStatMath.Resource.Mp,row.ChargePoints,(ushort)snapshot.PlayerLevel,inputs.Normal,inputs.Charge,out var mpBonus)
                :bonus.ResolveSelected(RetailNpcCurseStatMath.Resource.Mp,row.ChargePoints,(ushort)required,
                    (ushort)snapshot.PlayerLevel,inputs.Normal,inputs.Charge,out mpBonus);
            if(hp!=RetailNpcCurseItemBonusSource.Failure.None ||mp!=RetailNpcCurseItemBonusSource.Failure.None)
            {failure=ProjectionFailure.InvalidBonus;return false;}
            if(row.HasFusion)
            {
                if(!sharedTwoHand ||row.Fusion is not {WeaponStatus: RetailNpcCurseWeaponHpSource.Failure.None,
                        Weapon.IsRetailTwoHanded: true, BonusStatus: RetailNpcCurseItemBonusSource.Failure.None, Bonus: { } fused}
                    ||row.WeaponHp is not { } mainWeapon ||row.Fusion.Weapon.HostGroup!=mainWeapon.HostGroup
                    ||row.Fusion.ItemId!=row.Fusion.Weapon.ItemId ||row.Fusion.ItemId!=fused.ItemId
                    ||fused.Kind!="weapon" ||fused.HostGroup!=row.Fusion.Weapon.HostGroup
                    ||!string.Equals(row.Fusion.HostSourceName,row.Fusion.Weapon.Name,StringComparison.OrdinalIgnoreCase)
                    ||!string.Equals(fused.Name,row.Fusion.Weapon.Name,StringComparison.OrdinalIgnoreCase)
                    ||permits is null ||!permits.TryLookup(fused,snapshot.NativeClass,out byte fusedPermit,out _))
                {failure=ProjectionFailure.MissingItemSource;return false;}
                if(fusionContext?.Invoke(row) is not { LinkedItemActive:true, StandardBonusPath:true, AdditionalEffectsAbsent:true,
                        SecondaryChargePoints: { } fusedCharge } fusion
                    ||fusion.Normal.SpecialServerType!=inputs.Normal.SpecialServerType
                    ||fusion.Normal.ActorPresent!=inputs.Normal.ActorPresent ||fusion.Normal.ActorType!=inputs.Normal.ActorType
                    ||fusion.Normal.HighLevelFlag!=inputs.Normal.HighLevelFlag)
                {failure=ProjectionFailure.MissingNativeContext;return false;}
                int fusedRequired=fusedPermit;
                if(fusedPermit!=0)
                {
                    if(fusion.SecondaryLevelAdjustment is not { } adjustment ||adjustment!=0 && fusion.SecondaryHighLevelFloor is null)
                    {failure=ProjectionFailure.MissingNativeContext;return false;}
                    fusedRequired=RetailNpcCurseArmorSource.AdjustClassPermit(fusedPermit,adjustment,fusion.SecondaryHighLevelFloor.GetValueOrDefault());
                }
                if(fusedRequired is <0 or >ushort.MaxValue)
                {failure=ProjectionFailure.MissingNativeContext;return false;}
                var levels=new RetailNpcCurseItemBonusSource.FusionLevels((ushort)required,(ushort)fusedRequired,permit,fusedPermit);
                var fusedHpStatus=fused.ResolveFusedSelected(RetailNpcCurseStatMath.Resource.Hp,fusedCharge,(ushort)snapshot.PlayerLevel,
                    levels,fusion.Normal,fusion.Charge,out var fusedHp);
                var fusedMpStatus=fused.ResolveFusedSelected(RetailNpcCurseStatMath.Resource.Mp,fusedCharge,(ushort)snapshot.PlayerLevel,
                    levels,fusion.Normal,fusion.Charge,out var fusedMp);
                if(fusedHpStatus!=RetailNpcCurseItemBonusSource.Failure.None ||fusedMpStatus!=RetailNpcCurseItemBonusSource.Failure.None)
                {failure=ProjectionFailure.InvalidBonus;return false;}
                hpBonus=new(unchecked(hpBonus.Points+fusedHp.Points),unchecked(hpBonus.Percent+fusedHp.Percent));
                mpBonus=new(unchecked(mpBonus.Points+fusedMp.Points),unchecked(mpBonus.Percent+fusedMp.Percent));
            }
            else if(row.Fusion is not null)
            {failure=ProjectionFailure.UnsupportedItemState;return false;}
            int itemHp=offhand?0:row.Source is { } armor
                ?RetailNpcCurseArmorSource.RevisedHpWithAuthorization(armor,authorization,required,snapshot.PlayerLevel,armorRevision,restricted)
                :authorization; // Reviewed weapon/other raw and default-enchant HP are zero.
            armorHp=unchecked(armorHp+itemHp);
            hpPoints=unchecked(hpPoints+hpBonus.Points);hpPercent=unchecked(hpPercent+hpBonus.Percent);
            mpPoints=unchecked(mpPoints+mpBonus.Points);mpPercent=unchecked(mpPercent+mpBonus.Percent);count++;
        }
        projection=new(count,armorHp,new(hpPoints,hpPercent),new(mpPoints,mpPercent));
        failure=ProjectionFailure.None;failedItemId=0;return true;
    }

    internal static Snapshot Capture(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Capture(player.GetEquipment(),player.GetPlayerClass().GetClassId(),player.GetLevel(),
            RetailNpcStatCatalog.CurseArmorSource,RetailNpcStatCatalog.CurseOffhandSource,RetailNpcStatCatalog.CurseWeaponHpSource,
            RetailNpcStatCatalog.CurseOtherEquipmentHpSource,RetailNpcStatCatalog.CurseAuthorizationHpSource,RetailNpcStatCatalog.CurseItemBonusSource);
    }

    /// <summary>Point-in-time witness for the same actor life, class, level and
    /// captured equipment. It does not lock the actor across later operations,
    /// detect an ABA change, validate stat/effect ownership or establish native
    /// item eligibility; those remain separate requirements for live admission.</summary>
    internal static Func<Player,bool> EquipmentWitness(Player owner,Snapshot expected,Func<Player,Snapshot> recapture)
    {
        ArgumentNullException.ThrowIfNull(owner);ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(recapture);
        var equipment=owner.GetEquipment();long life=owner.GetController().RetailLifeEpoch;
        return current=>
        {
            if(!ReferenceEquals(current,owner) ||!current.IsSpawned() ||current.GetLifeStats().GetCurrentHp()<=0
                ||current.GetController().RetailLifeEpoch!=life ||!ReferenceEquals(current.GetEquipment(),equipment)
                ||current.GetPlayerClass().GetClassId()!=expected.NativeClass ||current.GetLevel()!=expected.PlayerLevel
                ||expected.Items.IsDefault ||expected.Items.Any(x=>x.Status==Status.ItemChanged))return false;
            var actual=recapture(current);
            return actual is not null && actual.NativeClass==expected.NativeClass && actual.PlayerLevel==expected.PlayerLevel
                &&!actual.Items.IsDefault &&!actual.Items.Any(x=>x.Status==Status.ItemChanged)
                &&expected.Items.SequenceEqual(actual.Items) &&current.IsSpawned() &&current.GetLifeStats().GetCurrentHp()>0
                &&current.GetController().RetailLifeEpoch==life &&ReferenceEquals(current.GetEquipment(),equipment)
                &&current.GetPlayerClass().GetClassId()==expected.NativeClass &&current.GetLevel()==expected.PlayerLevel;
        };
    }

    internal static Snapshot Capture(Equipment equipment,int nativeClass,int playerLevel,RetailNpcCurseArmorSource? source,
        RetailNpcCurseOffhandSource? offhand=null,RetailNpcCurseWeaponHpSource? weaponHp=null,
        RetailNpcCurseOtherEquipmentHpSource? otherHp=null,RetailNpcCurseAuthorizationHpSource? authorizationHp=null,
        RetailNpcCurseItemBonusSource? itemBonus=null)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if(nativeClass is <0 or >=17||playerLevel is <0 or >ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(nativeClass));
        // Equip and UnEquipItem use the Equipment instance monitor, while
        // InspectEquippedSlots uses the dictionary monitor. Take them in that
        // order so this capture cannot interleave with those operations. Other
        // item mutations and weapon switching are not covered by this lock.
        lock(equipment)
            return equipment.InspectEquippedSlots(slots=>
                new Snapshot(nativeClass,playerLevel,slots.Select(entry=>Observe(entry.Key,entry.Value,nativeClass,source,offhand,weaponHp,otherHp,authorizationHp,itemBonus)).ToImmutableArray()));
    }

    private static ItemObservation Observe(long occupiedSlot,Item item,int nativeClass,RetailNpcCurseArmorSource? source,
        RetailNpcCurseOffhandSource? offhand,RetailNpcCurseWeaponHpSource? weaponHp,RetailNpcCurseOtherEquipmentHpSource? otherHp,
        RetailNpcCurseAuthorizationHpSource? authorizationHp,RetailNpcCurseItemBonusSource? itemBonus)
    {
        var template=item.GetItemTemplate();int id=item.GetItemId(),objectId=item.GetObjectId();
        string? name=template.GetRetailSourceName();long slot=item.GetEquipmentSlot();
        int enchant=item.GetEnchantLevel(),bonus=item.GetEnchantBonus(),tempering=item.GetTempering();bool equipped=item.IsEquipped();
        var conditioning=item.GetConditioningInfo();int charge=conditioning?.GetChargePoints()??0;
        var fusedTemplate=item.GetFusionedItemTemplate();bool fusion=fusedTemplate is not null;
        int fusedId=fusedTemplate?.GetTemplateId()??0;string? fusedName=fusedTemplate?.GetRetailSourceName();
        var fusedGroup=fusedTemplate?.GetItemGroup();int? fusedMaxEnchant=fusedTemplate?.GetMaxEnchantLevel();
        string? fusedEnchantName=fusedTemplate?.GetEnchantName();
        var random=item.GetBonusStatsEffect();var fusionRandom=item.GetFusionedItemBonusStatsEffect();
        bool hasStones=item.HasManaStones(),hasFusionStones=item.HasFusionStones();var idian=item.GetIdianStone();
        RetailNpcCurseArmorSource.Candidate? candidate=null;byte? permit=null;bool? present=null;
        RetailNpcCurseWeaponHpSource.Candidate? weaponCandidate=null;
        RetailNpcCurseOtherEquipmentHpSource.Candidate? otherCandidate=null;
        Status status;
        // The native loop walks bits 0..20, except its inactive weapon slots
        // 17 and 18. A slot key must agree with the item's own occupied mask.
        const long retailSlots=((1L<<21)-1)&~((1L<<17)|(1L<<18));
        var weapon=occupiedSlot==ItemSlot.SUB_HAND.GetSlotIdMask()&&offhand is not null
            ?offhand.Lookup(id,template):RetailNpcCurseOffhandSource.Failure.UnknownItem;
        var weaponBase=(occupiedSlot==ItemSlot.MAIN_HAND.GetSlotIdMask()
            ||occupiedSlot==ItemSlot.SUB_HAND.GetSlotIdMask() &&weapon==RetailNpcCurseOffhandSource.Failure.None) &&weaponHp is not null
            ?weaponHp.Lookup(id,template,enchant,out weaponCandidate):RetailNpcCurseWeaponHpSource.Failure.UnknownItem;
        var otherBase=otherHp is not null?otherHp.Lookup(id,template,enchant,occupiedSlot,out otherCandidate)
            :RetailNpcCurseOtherEquipmentHpSource.Failure.UnknownItem;
        if(!equipped)status=Status.ItemNotEquipped;
        else if(occupiedSlot<=0||(occupiedSlot&(occupiedSlot-1))!=0||(slot&occupiedSlot)==0)status=Status.SlotMismatch;
        else if((occupiedSlot&retailSlots)==0)status=Status.OutsideRetailStatLoop;
        else if(weapon!=RetailNpcCurseOffhandSource.Failure.UnknownItem)
            status=weapon switch
            {
                RetailNpcCurseOffhandSource.Failure.None=>Status.SkippedRetailOffhandWeapon,
                RetailNpcCurseOffhandSource.Failure.IdentityMismatch=>Status.IdentityMismatch,
                RetailNpcCurseOffhandSource.Failure.HostTemplateDrift=>Status.HostTemplateDrift,
                _=>throw new InvalidOperationException("Unknown offhand weapon lookup result")
            };
        else if(weaponBase!=RetailNpcCurseWeaponHpSource.Failure.UnknownItem)
            status=weaponBase switch
            {
                RetailNpcCurseWeaponHpSource.Failure.None=>Status.ReviewedWeaponHpOnly,
                RetailNpcCurseWeaponHpSource.Failure.IdentityMismatch=>Status.IdentityMismatch,
                RetailNpcCurseWeaponHpSource.Failure.HostTemplateDrift=>Status.HostTemplateDrift,
                RetailNpcCurseWeaponHpSource.Failure.UnreviewedEnchantLevel=>Status.UnreviewedEnchantLevel,
                _=>throw new InvalidOperationException("Unknown weapon HP lookup result")
            };
        else if(otherBase!=RetailNpcCurseOtherEquipmentHpSource.Failure.UnknownItem)
            status=otherBase switch
            {
                RetailNpcCurseOtherEquipmentHpSource.Failure.None=>Status.ReviewedOtherEquipmentHpOnly,
                RetailNpcCurseOtherEquipmentHpSource.Failure.IdentityMismatch=>Status.IdentityMismatch,
                RetailNpcCurseOtherEquipmentHpSource.Failure.HostTemplateDrift=>Status.HostTemplateDrift,
                RetailNpcCurseOtherEquipmentHpSource.Failure.UnreviewedEnchantLevel=>Status.UnreviewedEnchantLevel,
                RetailNpcCurseOtherEquipmentHpSource.Failure.SlotMismatch=>Status.SlotMismatch,
                _=>throw new InvalidOperationException("Unknown equipment HP lookup result")
            };
        else if(source is null)status=Status.SourceUnavailable;
        else
        {
            var result=source.Lookup(id,name,enchant,out candidate);
            status=result switch
            {
                RetailNpcCurseArmorSource.Failure.None=>Status.ReviewedInputsOnly,
                RetailNpcCurseArmorSource.Failure.UnknownItem=>Status.UnknownItem,
                RetailNpcCurseArmorSource.Failure.IdentityMismatch=>Status.IdentityMismatch,
                RetailNpcCurseArmorSource.Failure.UnreviewedEnchantLevel=>Status.UnreviewedEnchantLevel,
                _=>throw new InvalidOperationException("Unknown curse armor lookup result")
            };
            if(candidate is not null)
            {
                string group=candidate.Table.EndsWith("_gloves",StringComparison.Ordinal)
                    ?candidate.Table[..^1]:candidate.Table;
                long expectedSlot=ExpectedSlot(candidate.Table);
                if(!string.Equals(template.GetItemGroup().ToString(),group,StringComparison.OrdinalIgnoreCase)
                    ||template.GetEnchantName() is not null||template.GetMaxEnchantLevel()!=candidate.HostMaxEnchant)
                    status=Status.HostTemplateDrift;
                else if(expectedSlot==0||occupiedSlot!=expectedSlot||slot!=expectedSlot)status=Status.SlotMismatch;
                if(candidate.TryGetClassPermit(nativeClass,out byte raw,out bool wasPresent))
                { permit=raw;present=wasPresent; }
            }
        }
        // Some item fields can be written outside the equipment monitor. This
        // catches changes during the read, not changes after returning.
        if(item.GetEquipmentSlot()!=slot||item.GetEnchantLevel()!=enchant||item.GetEnchantBonus()!=bonus||item.GetTempering()!=tempering||item.IsEquipped()!=equipped
            ||!ReferenceEquals(item.GetConditioningInfo(),conditioning)||(conditioning?.GetChargePoints()??0)!=charge
            ||!ReferenceEquals(item.GetFusionedItemTemplate(),fusedTemplate)
            ||fusedTemplate is not null && (fusedTemplate.GetTemplateId()!=fusedId ||fusedTemplate.GetRetailSourceName()!=fusedName
                ||fusedTemplate.GetItemGroup()!=fusedGroup ||fusedTemplate.GetMaxEnchantLevel()!=fusedMaxEnchant
                ||fusedTemplate.GetEnchantName()!=fusedEnchantName)
            ||!ReferenceEquals(item.GetBonusStatsEffect(),random)||!ReferenceEquals(item.GetFusionedItemBonusStatsEffect(),fusionRandom)
            ||item.HasManaStones()!=hasStones||item.HasFusionStones()!=hasFusionStones||!ReferenceEquals(item.GetIdianStone(),idian))
            status=Status.ItemChanged;
        if(status!=Status.ReviewedInputsOnly){candidate=null;permit=null;present=null;}
        if(status is not (Status.ReviewedWeaponHpOnly or Status.SkippedRetailOffhandWeapon))weaponCandidate=null;
        if(status!=Status.ReviewedOtherEquipmentHpOnly)otherCandidate=null;
        RetailNpcCurseAuthorizationHpSource.Failure? authorizationStatus=null;int? authorizedHp=null;
        if(otherCandidate is not null||weaponCandidate is not null||candidate is not null)
        {
            if(authorizationHp is null)authorizationStatus=RetailNpcCurseAuthorizationHpSource.Failure.SourceUnavailable;
            else
            {
                int value;
                if(otherCandidate is not null)authorizationStatus=authorizationHp.Lookup(otherCandidate,tempering,out value);
                else if(weaponCandidate is not null)authorizationStatus=authorizationHp.Lookup(weaponCandidate,tempering,out value);
                else authorizationStatus=authorizationHp.Lookup(candidate!,tempering,out value);
                if(authorizationStatus==RetailNpcCurseAuthorizationHpSource.Failure.None)authorizedHp=value;
            }
        }
        RetailNpcCurseItemBonusSource.Failure? bonusStatus=null;RetailNpcCurseItemBonusSource.Candidate? bonusCandidate=null;
        if(otherCandidate is not null||weaponCandidate is not null||candidate is not null)
        {
            if(itemBonus is null)bonusStatus=RetailNpcCurseItemBonusSource.Failure.SourceUnavailable;
            else if(charge is <0 or >1000000)bonusStatus=RetailNpcCurseItemBonusSource.Failure.UnreviewedCharge;
            else if(random is not null||fusionRandom is not null)bonusStatus=RetailNpcCurseItemBonusSource.Failure.UnreviewedItemState;
            else if(otherCandidate is not null)bonusStatus=itemBonus.Lookup(otherCandidate,out bonusCandidate);
            else if(weaponCandidate is not null)bonusStatus=itemBonus.Lookup(weaponCandidate,out bonusCandidate);
            else bonusStatus=itemBonus.Lookup(candidate!,out bonusCandidate);
        }
        FusionObservation? fusedObservation=null;
        if(fusedTemplate is not null)
        {
            RetailNpcCurseWeaponHpSource.Candidate? fusedWeapon=null;
            RetailNpcCurseWeaponHpSource.Failure? weaponStatus=weaponHp?.Lookup(fusedId,fusedTemplate,0,out fusedWeapon);
            RetailNpcCurseItemBonusSource.Candidate? fusedBonus=null;
            RetailNpcCurseItemBonusSource.Failure? fusedBonusStatus=fusedWeapon is null?null
                :itemBonus is null?RetailNpcCurseItemBonusSource.Failure.SourceUnavailable:itemBonus.Lookup(fusedWeapon,out fusedBonus);
            fusedObservation=new(fusedId,fusedName,weaponStatus,fusedWeapon,fusedBonusStatus,fusedBonus);
            if(!ReferenceEquals(item.GetFusionedItemTemplate(),fusedTemplate)
                ||fusedTemplate.GetTemplateId()!=fusedId ||fusedTemplate.GetRetailSourceName()!=fusedName
                ||fusedTemplate.GetItemGroup()!=fusedGroup ||fusedTemplate.GetMaxEnchantLevel()!=fusedMaxEnchant
                ||fusedTemplate.GetEnchantName()!=fusedEnchantName)status=Status.ItemChanged;
        }
        return new(objectId,id,name,slot,occupiedSlot,enchant,bonus,tempering,status,candidate,weaponCandidate,otherCandidate,permit,present,authorizationStatus,authorizedHp,
            charge,fusion,random is not null||fusionRandom is not null,hasStones||hasFusionStones||idian is not null,bonusStatus,bonusCandidate,fusedObservation);
    }

    private static long ExpectedSlot(string table)
    {
        if(table.EndsWith("_torso",StringComparison.Ordinal))return ItemSlot.TORSO.GetSlotIdMask();
        if(table.EndsWith("_gloves",StringComparison.Ordinal))return ItemSlot.GLOVES.GetSlotIdMask();
        if(table.EndsWith("_shoulder",StringComparison.Ordinal))return ItemSlot.SHOULDER.GetSlotIdMask();
        if(table.EndsWith("_pants",StringComparison.Ordinal))return ItemSlot.PANTS.GetSlotIdMask();
        if(table.EndsWith("_shoes",StringComparison.Ordinal))return ItemSlot.BOOTS.GetSlotIdMask();
        return 0;
    }
}
