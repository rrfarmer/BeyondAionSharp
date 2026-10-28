using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Enchants;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Build a source-based curse delta around the host player's current
/// maximum resources. The caller must supply reviewed modifier and item-context
/// witnesses; creating a plan does not admit a skill into gameplay.</summary>
internal static class RetailNpcCurseResourcePlanBuilder
{
    internal enum Failure { None, WrongEffect, PlayerRetired, PrimaryInputUnavailable, AlreadyCursed, EquipmentUnsupported,
        ModifierInputsUnavailable, ResourceOutOfRange, SnapshotChanged }
    internal sealed record ModifierInputs(int HpPercent,int MpPercent,Func<Player,bool> Current);

    // A narrow real-player input source for the plain, unequipped case. Do
    // not infer zero from an unknown modifier: anything other than the host's
    // predefined HP/MP base functions and this curse's own checked additions
    // makes the plan unavailable. The existing stat witness validates the
    // curse-owned additions and detects collection mutations across the plan.
    internal static bool HasUnmodifiedResourceInputs(Player target,Effect curse)
        =>HasUnmodifiedResourceInputs(target,curse,null);

    private static bool HasUnmodifiedResourceInputs(Player target,Effect curse,IReadOnlySet<Item>? flatItems,
        IReadOnlySet<ManaStone>? stones=null)
    {
        if(!ReferenceEquals(curse.GetEffected(),target)||!target.IsSpawned()||target.IsDead())return false;
        var stats=target.GetGameStats();
        if(!stats.TryCaptureRetailStatMutation(out long stamp))return false;
        // The host enchant owner is separate from its Item. Compare its exact
        // HP/MP functions to the host table before allowing that owner, while
        // the selected 5.8 armor source supplies the retail percentage basis.
        var enchantExpected=new Dictionary<EnchantEffect,(int[] Hp,int[] Mp)>(ReferenceEqualityComparer.Instance);
        if(flatItems is not null)
        foreach(var item in flatItems)
        {
            int level=item.GetEnchantLevel();var enchantOwner=item.GetEnchantEffect();
            if(level==0){if(enchantOwner is not null)return false;continue;}
            if(level is <1 or >15||enchantOwner is null||enchantExpected.ContainsKey(enchantOwner))return false;
            var table=DataManager.ENCHANT_DATA.GetTemplates(item.GetItemTemplate());
            if(table is null||table.Count==0||level>=table.Keys.Max()||!table.TryGetValue(level,out var values))return false;
            enchantExpected.Add(enchantOwner,(values.Where(x=>x.GetStat()==StatEnum.MAXHP).Select(x=>x.GetValue()).Order().ToArray(),
                values.Where(x=>x.GetStat()==StatEnum.MAXMP).Select(x=>x.GetValue()).Order().ToArray()));
        }
        bool Check(StatEnum name,Type baseType)
        {
            int baseCount=0;var seenStones=new HashSet<IStatFunction>(ReferenceEqualityComparer.Instance);
            var enchantValues=new Dictionary<EnchantEffect,List<int>>(ReferenceEqualityComparer.Instance);
            foreach(var function in stats.GetStatsSorted(name))
            {
                IStatFunction inner=function is StatFunctionProxy proxy ? proxy.GetProxiedFunction() : function;
                if(ReferenceEquals(function.GetOwner(),curse))
                {
                    if(inner.GetType()!=typeof(StatAddFunction)||!function.IsBonus()||function.HasConditions())return false;
                }
                else if(function.GetOwner() is Item item && flatItems?.Contains(item)==true
                    &&inner.GetType()==typeof(StatAddFunction)&&!function.HasConditions()
                    &&item.GetCurrentModifiers().Any(x=>ReferenceEquals(x,inner))) { }
                else if(function.GetOwner() is ManaStone stone && stones?.Contains(stone)==true
                    &&inner.GetType()==typeof(StatAddFunction)&&function.IsBonus()&&!function.HasConditions()
                    &&stone.GetModifiers().Any(x=>ReferenceEquals(x,inner))&&seenStones.Add(inner)) { }
                else if(function.GetOwner() is EnchantEffect owner &&enchantExpected.ContainsKey(owner)
                    &&inner.GetType()==typeof(StatAddFunction)&&!function.IsBonus()&&!function.HasConditions())
                {
                    if(!enchantValues.TryGetValue(owner,out var list))enchantValues[owner]=list=[];
                    list.Add(function.GetValue());
                }
                else if(function.GetOwner() is null && inner.GetType()==baseType && !function.HasConditions())baseCount++;
                else return false;
            }
            foreach(var pair in enchantExpected)
            {
                int[] expected=name==StatEnum.MAXHP?pair.Value.Hp:pair.Value.Mp;
                int[] actual=enchantValues.TryGetValue(pair.Key,out var list)?list.Order().ToArray():[];
                if(!expected.SequenceEqual(actual))return false;
            }
            if(stones is not null)
                foreach(var stone in stones)
                    foreach(var function in stone.GetModifiers().Where(x=>x.GetName()==name))
                        if(!seenStones.Contains(function))return false;
            return baseCount==1;
        }
        return Check(StatEnum.MAXHP,typeof(MaxHpFunction)) && Check(StatEnum.MAXMP,typeof(MaxMpFunction))
            &&ReferenceEquals(stats,target.GetGameStats())&&stats.RetailStatMutationUnchanged(stamp);
    }

    // Zero raw armor HP is invariant across revision branches. A separately
    // verified 5.8 enchant HP contribution can still change the curse basis.
    internal static bool IsInvariantFlatArmor(RetailNpcCurseEquipmentObservation.ItemObservation row,
        int nativeClass,RetailNpcCurseClassPermitSource permits)
        =>IsOrdinaryArmor(row,nativeClass,null,permits);

    // 4.8 items have no 5.8 level-down state. Map an ordinary 4.8 item to
    // the native fresh-item adjustment of zero; do not claim that an
    // independently level-reduced 5.8 item has the same state. When the
    // source class requirement is met, both native armor-revision branches
    // retain raw HP regardless of their runtime switch.
    internal static bool IsOrdinaryArmor(RetailNpcCurseEquipmentObservation.ItemObservation row,
        int nativeClass,int? playerLevel,RetailNpcCurseClassPermitSource permits,bool allowReviewedStones=false)
    {
        if(row.Status!=RetailNpcCurseEquipmentObservation.Status.ReviewedInputsOnly
            ||row.Source is not { RawMaxHp:>=0,EnchantMaxHp:>=0 } armor
            ||row.ItemBonus is not {Kind:"armor",Apply:"equip"} bonus
            ||row.ObjectId<=0||row.ItemId<=0||armor.ItemId!=row.ItemId||bonus.ItemId!=row.ItemId
            ||!string.Equals(row.HostSourceName,armor.Name,StringComparison.OrdinalIgnoreCase)
            ||!string.Equals(bonus.Name,armor.Name,StringComparison.OrdinalIgnoreCase)
            ||row.ItemBonusStatus!=RetailNpcCurseItemBonusSource.Failure.None
            ||row.AuthorizationStatus!=RetailNpcCurseAuthorizationHpSource.Failure.None||row.AuthorizationHp!=0
            ||row.EnchantLevel is <0 or >15||row.EnchantLevel==0&&armor.EnchantMaxHp!=0
            ||row.EnchantBonus!=0||row.Tempering!=0||row.ChargePoints!=0
            ||row.HasFusion||row.HasRandomBonus||row.HasSocketOrIdian&&!allowReviewedStones
            ||bonus.Hp.ComparisonStatus!="fields_match"||bonus.Mp.ComparisonStatus!="fields_match"
            ||bonus.Hp.Values.IsDefault||bonus.Mp.Values.IsDefault
            ||bonus.Hp.Values.Length!=3||bonus.Mp.Values.Length!=3
            ||bonus.Hp.Values.Any(x=>x is {Percent:true,Value:not 0})
            ||bonus.Mp.Values.Any(x=>x is {Percent:true,Value:not 0})
            ||!RetailNpcCurseEquipmentObservation.TryOrdinaryWearBonusFlags(row.OccupiedSlot,row.HostSlot,false,out _)
            ||!armor.TryGetClassPermit(nativeClass,out byte permit,out _)
            ||row.RawClassPermit!=permit||!permits.TryLookup(bonus,nativeClass,out byte catalogPermit,out _)||catalogPermit!=permit)
            return false;
        if(armor.RawMaxHp!=0 && (playerLevel is not (>=1 and <=65)||permit==0||permit>playerLevel))return false;
        return true;
    }

    internal static bool IsInvariantFlatItem(RetailNpcCurseEquipmentObservation.ItemObservation row,
        int nativeClass,RetailNpcCurseClassPermitSource permits,bool twoHanded,int? playerLevel=null,bool allowReviewedStones=false)
    {
        if(row.Status==RetailNpcCurseEquipmentObservation.Status.ReviewedInputsOnly)
            return !twoHanded && IsOrdinaryArmor(row,nativeClass,playerLevel,permits,allowReviewedStones);
        if(row.ItemBonus is not {Apply:"equip"} bonus||row.ObjectId<=0||row.ItemId<=0||bonus.ItemId!=row.ItemId
            ||row.ItemBonusStatus!=RetailNpcCurseItemBonusSource.Failure.None
            ||row.AuthorizationStatus!=RetailNpcCurseAuthorizationHpSource.Failure.None||row.AuthorizationHp!=0
            ||row.EnchantLevel is <0 or >15||row.EnchantBonus!=0||row.Tempering!=0||row.ChargePoints!=0
            ||row.HasFusion||row.HasRandomBonus||row.HasSocketOrIdian&&!allowReviewedStones
            ||bonus.Hp.ComparisonStatus!="fields_match"||bonus.Mp.ComparisonStatus!="fields_match"
            ||bonus.Hp.Values.IsDefault||bonus.Mp.Values.IsDefault||bonus.Hp.Values.Length!=3||bonus.Mp.Values.Length!=3
            ||bonus.Hp.Values.Any(x=>x is {Percent:true,Value:not 0})||bonus.Mp.Values.Any(x=>x is {Percent:true,Value:not 0})
            ||!RetailNpcCurseEquipmentObservation.TryOrdinaryWearBonusFlags(row.OccupiedSlot,row.HostSlot,twoHanded,out _)
            ||!permits.TryLookup(bonus,nativeClass,out _,out _))return false;
        if(row.Status is RetailNpcCurseEquipmentObservation.Status.ReviewedWeaponHpOnly
            or RetailNpcCurseEquipmentObservation.Status.SkippedRetailOffhandWeapon)
        {
            if(row.WeaponHp is not {RawMaxHp:0,EnchantMaxHp:0} weapon||bonus.Kind!="weapon"
                ||weapon.ItemId!=row.ItemId||weapon.HostGroup!=bonus.HostGroup||weapon.IsRetailTwoHanded!=twoHanded
                ||!string.Equals(weapon.Name,bonus.Name,StringComparison.OrdinalIgnoreCase)
                ||!string.Equals(weapon.Name,row.HostSourceName,StringComparison.OrdinalIgnoreCase))return false;
            return true;
        }
        if(row.Status==RetailNpcCurseEquipmentObservation.Status.ReviewedOtherEquipmentHpOnly)
        {
            if(twoHanded||row.OtherHp is not {RawMaxHp:0,EnchantMaxHp:0} other||bonus.Kind!="other"
                ||other.ItemId!=row.ItemId||other.HostGroup!=bonus.HostGroup
                ||!string.Equals(other.Name,bonus.Name,StringComparison.OrdinalIgnoreCase)
                ||!string.Equals(other.Name,row.HostSourceName,StringComparison.OrdinalIgnoreCase))return false;
            return true;
        }
        return false;
    }

    private static bool TryCanonicalFlatItems(RetailNpcCurseEquipmentObservation.Snapshot snapshot,
        RetailNpcCurseClassPermitSource permits,bool armorOnly,bool allowReviewedStones,out List<RetailNpcCurseEquipmentObservation.ItemObservation> selected)
    {
        selected=[];
        foreach(var group in snapshot.Items.GroupBy(x=>x.ObjectId))
        {
            var rows=group.ToArray();var row=rows[0];bool twoHanded=false;
            if(rows.Length==2)
            {
                long main=ItemSlot.MAIN_HAND.GetSlotIdMask(),sub=ItemSlot.SUB_HAND.GetSlotIdMask();
                var primary=rows.SingleOrDefault(x=>x.OccupiedSlot==main);
                var secondary=rows.SingleOrDefault(x=>x.OccupiedSlot==sub);
                if(primary is null||secondary is null||primary.Status!=RetailNpcCurseEquipmentObservation.Status.ReviewedWeaponHpOnly
                    ||primary.WeaponHp?.IsRetailTwoHanded!=true||primary.HostSlot!=(main|sub)||secondary.HostSlot!=(main|sub)
                    ||secondary.Status is not (RetailNpcCurseEquipmentObservation.Status.UnknownItem or RetailNpcCurseEquipmentObservation.Status.SourceUnavailable
                        or RetailNpcCurseEquipmentObservation.Status.SkippedRetailOffhandWeapon)
                    ||primary.ItemId!=secondary.ItemId||primary.HostSourceName!=secondary.HostSourceName
                    ||primary.EnchantLevel!=secondary.EnchantLevel||primary.EnchantBonus!=secondary.EnchantBonus
                    ||primary.Tempering!=secondary.Tempering||primary.ChargePoints!=secondary.ChargePoints
                    ||primary.HasFusion!=secondary.HasFusion||primary.HasRandomBonus!=secondary.HasRandomBonus
                    ||primary.HasSocketOrIdian!=secondary.HasSocketOrIdian
                    ||secondary.Status==RetailNpcCurseEquipmentObservation.Status.SkippedRetailOffhandWeapon
                        &&(secondary.WeaponHp!=primary.WeaponHp||secondary.ItemBonus!=primary.ItemBonus
                            ||secondary.ItemBonusStatus!=primary.ItemBonusStatus||secondary.AuthorizationStatus!=primary.AuthorizationStatus
                            ||secondary.AuthorizationHp!=primary.AuthorizationHp))return false;
                row=primary;twoHanded=true;
            }
            else if(rows.Length!=1||row.WeaponHp?.IsRetailTwoHanded==true)return false;
            if(armorOnly ? !IsInvariantFlatArmor(row,snapshot.NativeClass,permits)
                : !IsInvariantFlatItem(row,snapshot.NativeClass,permits,twoHanded,snapshot.PlayerLevel,allowReviewedStones))return false;
            selected.Add(row);
        }
        foreach(var row in selected.Where(x=>x.Status==RetailNpcCurseEquipmentObservation.Status.SkippedRetailOffhandWeapon))
            if(!selected.Any(x=>x.Status==RetailNpcCurseEquipmentObservation.Status.ReviewedWeaponHpOnly
                &&x.OccupiedSlot==ItemSlot.MAIN_HAND.GetSlotIdMask()&&x.ObjectId!=row.ObjectId))return false;
        return selected.Count>0;
    }

    // Host equipment mutation is not universally locked. Capture exact stone
    // object and slot identities and revalidate them at every existing witness
    // boundary; the source and stat-owner checks reject unsupported states.
    private static bool TryCaptureStones(IEnumerable<Item> items,RetailNpcCurseManastoneSource? source,
        out Dictionary<ManaStone,(int ItemObjectId,int Slot)> stones)
    {
        stones=new(ReferenceEqualityComparer.Instance);
        try
        {
            foreach(var item in items)
            {
                if(item.HasFusionStones()||item.GetIdianStone() is not null)return false;
                if(!item.HasManaStones())continue;
                if(source is null)return false;
                var selected=item.GetItemStones().ToArray();
                if(selected.Length==0||selected.Length>6||selected.Select(x=>x.GetSlot()).Distinct().Count()!=selected.Length)return false;
                foreach(var stone in selected)
                    if(!source.Matches(stone,item.GetObjectId())||stone.GetSlot()>=item.GetSockets(false)
                        ||!stones.TryAdd(stone,(item.GetObjectId(),stone.GetSlot())))return false;
            }
        }
        catch(InvalidOperationException){return false;}
        return true;
    }

    internal static bool HasReviewedResourceManastones(Item item,RetailNpcCurseManastoneSource? source)
        =>TryCaptureStones([item],source,out _);

    private static bool StonesCurrent(IEnumerable<Item> items,RetailNpcCurseManastoneSource? source,
        IReadOnlyDictionary<ManaStone,(int ItemObjectId,int Slot)> expected)
        =>TryCaptureStones(items,source,out var current)&&current.Count==expected.Count
            &&current.All(x=>expected.TryGetValue(x.Key,out var identity)&&identity==x.Value);

    internal static bool TryBuildFlatArmorFromCatalog(Player target,Effect effect,
        out RetailNpcCurseResourceOwner.Plan? plan,out Failure failure)
        =>TryBuildFlatEquipmentFromCatalog(target,effect,true,out plan,out failure,false);

    internal static bool TryBuildFlatEquipmentFromCatalog(Player target,Effect effect,
        out RetailNpcCurseResourceOwner.Plan? plan,out Failure failure)
        =>TryBuildFlatEquipmentFromCatalog(target,effect,false,out plan,out failure,false);

    private static bool TryBuildFlatEquipmentFromCatalog(Player target,Effect effect,bool armorOnly,
        out RetailNpcCurseResourceOwner.Plan? plan,out Failure failure,bool preflightOnly)
    {
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(effect);
        plan=null;failure=Failure.WrongEffect;
        if(!ReferenceEquals(effect.GetEffected(),target)||(!preflightOnly && !RetailNpcStatSpellDrain.HasPreparedCurse(effect))
            ||effect.GetEffectTemplates() is not {Count:2} templates||templates[1] is not CurseEffect curse
            ||!RetailNpcStatSpellDrain.CurseHostMatches(curse))return false;
        failure=Failure.PlayerRetired;
        if(!target.IsSpawned()||target.GetLifeStats().GetCurrentHp()<=0)return false;
        failure=Failure.PrimaryInputUnavailable;
        if(!RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(target,out var primary)||primary is null)return false;
        if(target.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE))
        {failure=Failure.AlreadyCursed;return false;}
        var permits=RetailNpcStatCatalog.CurseClassPermitSource;
        var snapshot=RetailNpcCurseEquipmentObservation.Capture(target);
        failure=Failure.EquipmentUnsupported;
        var stoneSource=RetailNpcStatCatalog.CurseManastoneSource;
        bool allowStones=!armorOnly && stoneSource is not null;
        if(permits is null||snapshot.Items.IsDefaultOrEmpty||!TryCanonicalFlatItems(snapshot,permits,armorOnly,allowStones,out var selected))return false;
        var items=target.GetEquipment().GetEquippedItems();
        if(items.Count!=selected.Count||items.Any(x=>x.GetItemTemplate().IsItemSet())
            ||items.Select(x=>x.GetObjectId()).Distinct().Count()!=items.Count
            ||!items.Select(x=>x.GetObjectId()).Order().SequenceEqual(selected.Select(x=>x.ObjectId).Order()))return false;
        var allowed=new HashSet<Item>(items,ReferenceEqualityComparer.Instance);
        if(!TryCaptureStones(items,stoneSource,out var stones))return false;
        var equipmentCurrent=RetailNpcCurseEquipmentObservation.EquipmentWitness(target,snapshot,RetailNpcCurseEquipmentObservation.Capture);
        bool InputsCurrent(Player p)=>ReferenceEquals(p,target)&&ReferenceEquals(RetailNpcStatCatalog.CurseClassPermitSource,permits)
            &&ReferenceEquals(RetailNpcStatCatalog.CurseManastoneSource,stoneSource)
            &&primary.HasEquivalentInputs(p)&&equipmentCurrent(p)&&StonesCurrent(items,stoneSource,stones)
            &&HasUnmodifiedResourceInputs(p,effect,allowed,new HashSet<ManaStone>(stones.Keys,ReferenceEqualityComparer.Instance));
        failure=Failure.ModifierInputsUnavailable;
        if(!InputsCurrent(target))return false;
        int hostHp=target.GetLifeStats().GetMaxHp(),hostMp=target.GetLifeStats().GetMaxMp();
        failure=Failure.ResourceOutOfRange;
        if(hostHp is <100 or >2_000_000_000||hostMp is <1 or >2_000_000_000)return false;
        long retailHpBasis=(long)primary.HpBasis+selected.Sum(x=>(long)(x.Source?.RawMaxHp??0)+(x.Source?.EnchantMaxHp??0));
        if(retailHpBasis is <0 or >int.MaxValue)return false;
        int newHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,(int)retailHpBasis,hostHp,-35);
        int newMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,primary.MpBasis,hostMp,-35);
        var witness=RetailNpcCurseStatWitness.Create(target,effect,InputsCurrent,newHp-hostHp,newMp-hostMp);
        failure=Failure.SnapshotChanged;
        if(!primary.IsCurrent(target)||!witness.Before(target)||target.GetLifeStats().GetMaxHp()!=hostHp
            ||target.GetLifeStats().GetMaxMp()!=hostMp)return false;
        plan=new((int)retailHpBasis,hostHp,0,primary.MpBasis,hostMp,0,witness.Before,witness.After,witness.CommitStamp);
        failure=Failure.None;return true;
    }

    internal static bool TryBuildPlainFromCatalog(Player target,Effect effect,
        out RetailNpcCurseResourceOwner.Plan? plan,out Failure failure,bool preflightOnly=false)
    {
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(effect);
        plan=null;failure=Failure.ModifierInputsUnavailable;
        if(!HasUnmodifiedResourceInputs(target,effect))return false;
        if(!RetailNpcCurseEquipmentObservation.Capture(target).Items.IsEmpty)
        {failure=Failure.EquipmentUnsupported;return false;}
        bool Current(Player p)=>ReferenceEquals(p,target)&&HasUnmodifiedResourceInputs(p,effect)
            &&RetailNpcCurseEquipmentObservation.Capture(p).Items.IsEmpty;
        return TryBuildFromCatalog(target,effect,new(0,0,Current),_=>null,out plan,out failure,out _,out _,preflightOnly:preflightOnly);
    }

    // Reuse the exact resource and equipment checks before the primary hit.
    // The resulting plan is deliberately discarded and must be rebuilt when
    // the curse starts; this does not prepare or apply an effect.
    internal static bool TryPreflightFromCatalog(Player target,Effect effect,out Failure failure)
        => target.GetEquipment().GetEquippedItems().Count==0
            ?TryBuildPlainFromCatalog(target,effect,out _,out failure,true)
            :TryBuildFlatEquipmentFromCatalog(target,effect,false,out _,out failure,true);

    internal static bool TryBuildFromCatalog(Player target,Effect effect,ModifierInputs modifiers,
        Func<RetailNpcCurseEquipmentObservation.ItemObservation,RetailNpcCurseEquipmentObservation.ArmorProjectionContext?> itemContext,
        out RetailNpcCurseResourceOwner.Plan? plan,out Failure failure,
        out RetailNpcCurseEquipmentObservation.ProjectionFailure equipmentFailure,out int failedItemId,
        Func<RetailNpcCurseEquipmentObservation.ItemObservation,RetailNpcCurseEquipmentObservation.FusionProjectionContext?>? fusionContext=null,
        IRetailNpcPlayerPrimaryReceipt? fixtureFood=null,bool preflightOnly=false)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(modifiers.Current);
        var permits=RetailNpcStatCatalog.CurseClassPermitSource;
        var guarded=modifiers with {Current=p=>ReferenceEquals(RetailNpcStatCatalog.CurseClassPermitSource,permits)
            && modifiers.Current(p)};
        return TryBuild(target,effect,guarded,permits,itemContext,out plan,out failure,out equipmentFailure,out failedItemId,fusionContext,fixtureFood,preflightOnly);
    }

    internal static bool TryBuild(Player target,Effect effect,ModifierInputs modifiers,
        RetailNpcCurseClassPermitSource? permits,
        Func<RetailNpcCurseEquipmentObservation.ItemObservation,RetailNpcCurseEquipmentObservation.ArmorProjectionContext?> itemContext,
        out RetailNpcCurseResourceOwner.Plan? plan,out Failure failure,
        out RetailNpcCurseEquipmentObservation.ProjectionFailure equipmentFailure,out int failedItemId,
        Func<RetailNpcCurseEquipmentObservation.ItemObservation,RetailNpcCurseEquipmentObservation.FusionProjectionContext?>? fusionContext=null,
        IRetailNpcPlayerPrimaryReceipt? fixtureFood=null,bool preflightOnly=false)
    {
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(modifiers);ArgumentNullException.ThrowIfNull(itemContext);
        plan=null;failure=Failure.WrongEffect;equipmentFailure=RetailNpcCurseEquipmentObservation.ProjectionFailure.None;failedItemId=0;
        if(!ReferenceEquals(effect.GetEffected(),target) ||(!preflightOnly && !RetailNpcStatSpellDrain.HasPreparedCurse(effect))
            ||effect.GetEffectTemplates() is not {Count:2} templates ||templates[1] is not CurseEffect curse
            ||!RetailNpcStatSpellDrain.CurseHostMatches(curse))return false;
        failure=Failure.PlayerRetired;
        if(!target.IsSpawned() ||target.GetLifeStats().GetCurrentHp()<=0)return false;
        failure=Failure.PrimaryInputUnavailable;
        bool primaryAvailable=fixtureFood is null
            ?RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(target,out var primary)
            :RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailWithFoodForOfflineFixture(target,fixtureFood,out primary);
        if(!primaryAvailable ||primary is null)return false;
        if(target.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE))
        {failure=Failure.AlreadyCursed;return false;}
        if(modifiers.Current is null ||!modifiers.Current(target))
        {failure=Failure.ModifierInputsUnavailable;return false;}
        var snapshot=RetailNpcCurseEquipmentObservation.Capture(target);
        var equipmentWitness=RetailNpcCurseEquipmentObservation.EquipmentWitness(target,snapshot,RetailNpcCurseEquipmentObservation.Capture);
        if(!RetailNpcCurseEquipmentObservation.TryProjectEquipment(snapshot,permits,itemContext,out var equipment,
            out equipmentFailure,out failedItemId,fusionContext))
        {failure=Failure.EquipmentUnsupported;return false;}
        failedItemId=0;
        long hpBasis=(long)primary.HpBasis+equipment.RevisedArmorHp;
        long hpPercent=(long)equipment.HpBonus.Percent+modifiers.HpPercent;
        long mpPercent=(long)equipment.MpBonus.Percent+modifiers.MpPercent;
        int hostHp=target.GetLifeStats().GetMaxHp(),hostMp=target.GetLifeStats().GetMaxMp();
        if(hpBasis is <0 or >int.MaxValue ||hpPercent is <-10000 or >10000 ||mpPercent is <-10000 or >10000
            ||hostHp is <100 or >2_000_000_000 ||hostMp is <1 or >2_000_000_000)
        {failure=Failure.ResourceOutOfRange;return false;}
        // The player keeps the 4.8 maximum before the curse. The percentage
        // change uses the selected 5.8 resource basis and reviewed modifiers.
        long hpUnscaled=hostHp-hpBasis*hpPercent/100;
        long mpUnscaled=hostMp-(long)primary.MpBasis*mpPercent/100;
        if(hpUnscaled is <0 or >int.MaxValue ||mpUnscaled is <0 or >int.MaxValue)
        {failure=Failure.ResourceOutOfRange;return false;}
        int newHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,(int)hpBasis,(int)hpUnscaled,(int)hpPercent-35);
        int newMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,primary.MpBasis,(int)mpUnscaled,(int)mpPercent-35);
        bool InputsCurrent(Player p)=>primary.HasEquivalentInputs(p) &&equipmentWitness(p) &&modifiers.Current(p);
        Func<IStatFunction,bool>? reviewedConditional=fixtureFood is null?null:fixtureFood.AllowsConditionalFunction;
        var witness=RetailNpcCurseStatWitness.Create(target,effect,InputsCurrent,newHp-hostHp,newMp-hostMp,reviewedConditional);
        if(!primary.IsCurrent(target) ||!witness.Before(target)
            ||target.GetLifeStats().GetMaxHp()!=hostHp ||target.GetLifeStats().GetMaxMp()!=hostMp)
        {failure=Failure.SnapshotChanged;return false;}
        plan=new((int)hpBasis,(int)hpUnscaled,(int)hpPercent,primary.MpBasis,(int)mpUnscaled,(int)mpPercent,
            witness.Before,witness.After,witness.CommitStamp);
        failure=Failure.None;return true;
    }
}
