using System.Collections.Immutable;
using System.Globalization;
using System.Xml.Linq;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserStatSourceCatalog;
using R = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserStatSourceRecipes;
using GI = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifeInputs;
using GG = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserOwnedStatGraph;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Pure, whole seven-read owned refresh. A source catalog never manufactures a current
/// equipped account, sibling registration, class/permit array or once-only lease. No HP is applied.</summary>
internal static class RetailNpcUserStatSourceMaterialization
{
    internal enum Status { Computed, UnsupportedPrecondition, IncompleteReachedProgram }
    internal sealed record Item(GI.Binding Current, C.Catalog.Record Source, long EquipmentMask,
        bool? Equipped, R.OwnerType OwnerType, GI.Known Fusion, C.Catalog.Record? FusionSource = null,
        int? InventoryVariant = null, int? FusionInventoryVariant = null,
        C.Catalog.Record? IdianActionSource = null, int? IdianVariant = null,
        int? EnchantLevel = null, int? TemperingLevel = null, int? PlumeRandom = null);
    internal sealed record StatOrder(string Stat, ImmutableArray<string> Entries);
    internal sealed record RegistryInput(C.Catalog Catalog, GI.Binding Current, string Policy,
        bool? CompleteAllOwnersAndSiblings, bool? CompleteAllStatLists, bool? ActualPerStatOrderKnown,
        bool? CompleteAllItemsAndAliases, bool? PredefinedNullOwnerKnown,
        ImmutableArray<R.Registration> Registrations, ImmutableArray<Item> Items,
        ImmutableArray<StatOrder> StatLists, GI.Floating Floating);
    internal sealed record ProjectionResult(Status Status, Projection? Candidate, string? Failure)
    { internal bool ActivationReady=>false; internal bool RuntimeVerified=>false; }
    internal sealed record Derived(int Health,int Will,int MaxHp,int MaxMp,int BaseHp,int BaseMp,
        int HealthAdditional,int WillAdditional,int Speed,int Delay,
        ImmutableDictionary<string,GG.StatValue> Stats,ImmutableArray<R.Function> FullRegistry,
        ImmutableArray<R.Function> RetainedNonReadPrograms);
    internal sealed record ReadReceipt(string Stat,ImmutableArray<string> CurrentEntries,
        ImmutableArray<string> ExecutedEntries,ImmutableArray<string> PredicateFalseEntries);
    internal sealed record RefreshResult(Status Status,Derived? Candidate,ImmutableArray<ReadReceipt> Reads,string? Failure)
    { internal bool ActivationReady=>false;internal bool RuntimeVerified=>false; }
    private sealed record Built(RegistryInput Input,ImmutableArray<R.Function> Functions,
        ImmutableDictionary<string,ImmutableArray<string>> Lists,ImmutableDictionary<string,Item> Items);
    internal sealed class Projection
    {
        private readonly Built state;
        internal C.Catalog Catalog=>state.Input.Catalog;
        internal GI.Binding Current=>state.Input.Current;
        internal ImmutableArray<R.Function> Functions=>state.Functions;
        internal ImmutableArray<R.Registration> RegistrationOccurrences=>state.Input.Registrations;
        internal bool ActivationReady=>false;
        private Projection(Built state){this.state=state;}
        internal static Projection Admit(RegistryInput input)=>new(Build(input));
        internal ImmutableArray<R.Function> Stat(string name)=>state.Lists.TryGetValue(name,out var entries)
            ? entries.Select(e=>state.Functions.First(f=>f.Entry==e)).ToImmutableArray():[];
        internal ImmutableArray<Item> Items=>state.Input.Items;
        internal Item? ItemFor(string owner)=>state.Items.GetValueOrDefault(owner);
        internal static Projection Remove(Projection original,ImmutableArray<GI.Binding> owners)
        {
            GI.Require(original is not null && !owners.IsDefault && owners.All(x=>x is not null)
                && owners.Select(x=>x.Owner).Distinct().Count()==owners.Length,"exact removal owner set");
            var removed=new HashSet<string>(StringComparer.Ordinal);
            foreach(GI.Binding owner in owners)
            {
                owner.Validate(original.Current.Operation,original.Current.Epoch);
                GI.Require(original.state.Input.Registrations.Any(r=>r.Owner.Owner==owner.Owner && r.Owner.Generation==owner.Generation),"current exact removal owner/generation");
                removed.Add(owner.Owner);
            }
            var input=original.state.Input with{Registrations=original.state.Input.Registrations.Where(r=>!removed.Contains(r.Owner.Owner)).ToImmutableArray()};
            var functions=original.state.Functions.Where(f=>!removed.Contains(f.Owner.Owner)).ToImmutableArray();
            var keep=functions.Select(f=>f.Entry).ToHashSet(StringComparer.Ordinal);
            var lists=original.state.Lists.ToImmutableDictionary(p=>p.Key,p=>p.Value.Where(keep.Contains).ToImmutableArray(),StringComparer.Ordinal);
            // Every same-owner sibling/non-read program is removed; no registration callback or
            // legacy nested refresh is executed. Existing physical item/charge identity survives.
            return new(new(input,functions,lists,original.state.Items));
        }
    }
    internal static ProjectionResult Project(RegistryInput input)
    {
        try{return new(Status.Computed,Projection.Admit(input),null);}
        catch(GI.Domain ex){return new(Status.UnsupportedPrecondition,null,ex.Message);}
        catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {return new(Status.UnsupportedPrecondition,null,"source/current shape: "+ex.GetType().Name);}
    }
    internal static ProjectionResult RemoveOwners(Projection projection,ImmutableArray<GI.Binding> owners)
    {
        try{return new(Status.Computed,Projection.Remove(projection,owners),null);}
        catch(GI.Domain ex){return new(Status.UnsupportedPrecondition,null,ex.Message);}
    }
    private static Built Build(RegistryInput input)
    {
        GI.Require(input is not null && input.Catalog is not null && input.Current is not null && input.Policy==R.Policy
            && input.CompleteAllOwnersAndSiblings==true && input.CompleteAllStatLists==true
            && input.ActualPerStatOrderKnown==true && input.CompleteAllItemsAndAliases==true
            && input.PredefinedNullOwnerKnown==true,"whole current registry/sibling/item/order declarations");
        input.Current.Validate(input.Current.Operation,input.Current.Epoch); Floating(input.Floating);
        GI.Require(!input.Registrations.IsDefault && input.Registrations.Length is >0 and <=4096
            && !input.Items.IsDefault && input.Items.Length<=1024 && !input.StatLists.IsDefault && input.StatLists.Length<=512,"bounded initialized registry");
        GI.Require(input.Registrations.All(x=>x is not null && x.Selection is not null && x.Owner is not null
                && !x.ActualFunctions.IsDefault && x.ActualFunctions.All(f=>f is not null && f.Owner is not null))
            && input.Items.All(x=>x is not null && x.Current is not null && x.Source is not null)
            && input.StatLists.All(x=>x is not null && x.Stat is not null),"non-null typed current members");
        GI.Require(input.Registrations.Select(x=>x.Occurrence).Distinct().Count()==input.Registrations.Length
            && input.Registrations.Select(x=>x.Sequence).Distinct().Count()==input.Registrations.Length,"registration occurrence/order distinct from owner");
        GI.Require(input.Registrations.Count(x=>x.Selection.Kind==R.Kind.Predefined)==1,"one current predefined registration");
        var items=ImmutableDictionary.CreateBuilder<string,Item>(StringComparer.Ordinal);
        foreach(Item item in input.Items)
        {
            item.Current.Validate(input.Current.Operation,input.Current.Epoch);
            Source(input.Catalog,item.Source,C.Family.Item);
            GI.Require(item.Equipped.HasValue && item.OwnerType is R.OwnerType.Item or R.OwnerType.Stone
                && item.Fusion is GI.Known.Null or GI.Known.Present && items.TryAdd(item.Current.Owner,item),"actual unique item/stone/alias snapshot");
            if(item.Fusion==GI.Known.Present)Source(input.Catalog,item.FusionSource,C.Family.Item);
            else GI.Require(item.FusionSource is null,"known null fusion reference");
        }
        var ownerGeneration=items.ToDictionary(x=>x.Key,x=>x.Value.Current.Generation,StringComparer.Ordinal);
        var ownerTypes=items.ToDictionary(x=>x.Key,x=>x.Value.OwnerType,StringComparer.Ordinal);
        var functions=ImmutableArray.CreateBuilder<R.Function>();
        foreach(R.Registration registration in input.Registrations.OrderBy(x=>x.Sequence))
        {
            registration.Owner.Validate(input.Current.Operation,input.Current.Epoch);
            if(ownerGeneration.TryGetValue(registration.Owner.Owner,out string? previous))
                GI.Require(previous==registration.Owner.Generation,"all retained same-owner generations");
            else ownerGeneration.Add(registration.Owner.Owner,registration.Owner.Generation);
            if(ownerTypes.TryGetValue(registration.Owner.Owner,out var physicalType))
                GI.Require(physicalType==registration.OwnerType,"same physical owner has conflicting runtime types");
            else ownerTypes.Add(registration.Owner.Owner,registration.OwnerType);
            JoinItem(input,registration,items);
            if(registration.Selection.Kind==R.Kind.SetBonus)
            {
                Source(input.Catalog,registration.Source,C.Family.Set);
                string[] ids=input.Items.Where(i=>i.OwnerType==R.OwnerType.Item && i.Equipped==true
                    && (i.EquipmentMask&0x60000)==0
                    && input.Catalog.SetForItem(C.Authority.CSharp,i.Source.Metadata.Key.Primary)==registration.Source!.Metadata)
                    .Select(i=>i.Source.Metadata.Key.Primary).Distinct(StringComparer.Ordinal).ToArray();
                GI.Require(ids.Length==registration.Selection.EquippedParts,"computed current distinct equipped set parts, excluding alternate slots");
            }
            functions.AddRange(R.Construct(input.Catalog,registration,input.Current.Operation,input.Current.Epoch));
        }
        GI.Require(functions.Count<=32768 && functions.Select(f=>f.Entry).Distinct().Count()==functions.Count,"bounded unique actual function occurrences");
        var byEntry=functions.ToImmutableDictionary(x=>x.Entry,StringComparer.Ordinal);
        var lists=ImmutableDictionary.CreateBuilder<string,ImmutableArray<string>>(StringComparer.Ordinal);
        var allEntries=new HashSet<string>(StringComparer.Ordinal);
        foreach(StatOrder list in input.StatLists)
        {
            GI.Require(!list.Entries.IsDefault && lists.TryAdd(list.Stat,list.Entries),"one known current stat list");
            int priority=int.MinValue;
            foreach(string entry in list.Entries)
            {
                GI.Require(allEntries.Add(entry),"unique current list entry");
                GI.Require(byEntry.TryGetValue(entry,out var function) && function.Stat==list.Stat,"complete current list-to-function identity");
                GI.Require(function.Priority>=priority,"current list priority order");priority=function.Priority;
            }
        }
        GI.Require(allEntries.SetEquals(byEntry.Keys),"no omitted current stat list/non-read sibling");
        return new(input,functions.ToImmutable(),lists.ToImmutable(),items.ToImmutable());
    }
    private static void JoinItem(RegistryInput input,R.Registration r,ImmutableDictionary<string,Item>.Builder items)
    {
        if(r.Selection.Kind==R.Kind.Item)
        {
            GI.Require(items.TryGetValue(r.Owner.Owner,out var item) && item.Current.Generation==r.Owner.Generation
                && item.Source.Identity==r.Source?.Identity && item.OwnerType==r.OwnerType
                && item.EquipmentMask==r.Selection.EquipmentMask,"actual item/stone registration/source/slot");
        }
        if(r.Selection.Kind is R.Kind.Fusion or R.Kind.Random or R.Kind.Enchant or R.Kind.Tempering)
        {
            GI.Require(r.ItemOwner is not null,"constructor current item owner");
            GI.Require(items.TryGetValue(r.ItemOwner.Owner,out var item)
                && item.Current.Generation==r.ItemOwner.Generation,"constructor current item membership");
            bool fusion=r.FusionRandom;
            GI.Require(fusion ? item.Fusion==GI.Known.Present && item.FusionSource?.Identity==r.ItemSource?.Identity
                : item.Source.Identity==r.ItemSource?.Identity,"constructor original item/fusion source");
            if(r.Selection.Kind==R.Kind.Fusion)
            {
                // The extra fusion attack uses base-item attack_type, not the fusion template's.
                GI.Require(item.FusionSource?.Identity==r.Source?.Identity && r.ItemSource?.Identity==item.Source.Identity
                    && r.Owner.Owner==item.Current.Owner && r.Owner.Generation==item.Current.Generation
                    && r.Selection.EquipmentMask==item.EquipmentMask,"fusion source/base attack owner");
            }
            if(r.Selection.Kind==R.Kind.Random)
            {
                if(r.Source?.Metadata.Key.Secondary=="INVENTORY")
                    GI.Require((r.FusionRandom?item.FusionInventoryVariant:item.InventoryVariant)==r.Selection.Variant,"current persisted inventory/fusion variant");
                else GI.Require(item.IdianVariant==r.Selection.Variant && item.IdianActionSource?.Identity==r.ActionSource?.Identity,"current Idian action/variant");
            }
            if(r.Selection.Kind==R.Kind.Enchant)
                GI.Require(item.EnchantLevel==r.Selection.Level && item.EquipmentMask==r.Selection.EquipmentMask,"current enchant tier/slot");
            if(r.Selection.Kind==R.Kind.Tempering)
            {
                GI.Require(item.TemperingLevel==r.Selection.Level,"current tempering tier");
                if(item.Source.Read().Attribute("item_group")?.Value=="PLUME")
                    GI.Require(item.PlumeRandom==r.Selection.PlumeRandom,"current persisted PLUME random value");
            }
        }
    }
    internal static RefreshResult Refresh(Projection projection,GI.GeneralStatFrame frame)
    {
        try
        {
            GI.Require(projection is not null && frame is not null && frame.Current is not null,"general read projection/frame");
            frame.Current.Validate(frame.Operation,frame.Epoch);Floating(frame.Floating);
            GI.Require(frame.IsStaff==false,"explicit known nonstaff frame required for ordinary movement caps");
            GI.Require(frame.Operation==projection.Current.Operation && frame.Epoch==projection.Current.Epoch
                && frame.Current.Owner==projection.Current.Owner && frame.Current.Generation==projection.Current.Generation,"current source/read operation epoch generation");
            GI.Require(frame.Flying.HasValue && frame.StateFlying.HasValue && frame.Resting.HasValue && frame.Walking.HasValue
                && frame.Riding==false && frame.Sprinting==false,"known supported movement-state getter context");
            GG.Bases bases=GG.ClassBases(frame.ClassId,frame.Level,frame.ClassSourceSha256);
            // Read class Health/Will bases through the explicit source class table, not an enum cast.
            var classRow=ClassParameters(frame.ClassId);
            var values=ImmutableDictionary.CreateBuilder<string,GG.StatValue>(StringComparer.Ordinal);
            var reads=ImmutableArray.CreateBuilder<ReadReceipt>();
            GG.StatValue Read(string stat,int initial,int intrinsic=0)
            {
                var operations=ImmutableArray.CreateBuilder<GG.GeneralOperation>();
                var entered=ImmutableArray.CreateBuilder<string>();var executed=ImmutableArray.CreateBuilder<string>();var skipped=ImmutableArray.CreateBuilder<string>();
                long ordinal=0;
                foreach(R.Function function in projection.Stat(stat))
                {
                    entered.Add(function.Entry);
                    if(function.Program==R.Program.DuplicateDelay)
                    {
                        var selected=DuplicateDelay(projection,frame);
                        if(selected is null){operations.Add(new(function,ordinal++,true,intrinsic));executed.Add(function.Entry);}
                        else{operations.Add(new(selected,ordinal++,true,0));executed.Add(function.Entry+"=>"+selected.RawIdentity);}
                        continue;
                    }
                    bool valid=Valid(function,projection,frame);
                    if(valid)executed.Add(function.Entry);else skipped.Add(function.Entry);
                    GI.Require(function.Program!=R.Program.RetainedPredefined,"unknown reached retained getter");
                    operations.Add(new(function,ordinal++,valid,intrinsic));
                }
                GG.StatValue value=GG.EvaluateGeneralOrdered(stat,initial,operations.ToImmutable());
                reads.Add(new(stat,entered.ToImmutable(),executed.ToImmutable(),skipped.ToImmutable()));values.Add(stat,value);return value;
            }
            GG.StatValue health=Read("HEALTH",classRow.Health),will=Read("WILL",classRow.Will);
            int ah=GG.JavaInt(GG.Mul(GG.Div(unchecked(health.Current-100),100f),classRow.HpMultiplier));
            int am=GG.JavaInt(GG.Mul(GG.Div(unchecked(will.Current-100),100f),classRow.MpMultiplier));
            GG.StatValue hp=Read("MAXHP",bases.Hp,ah),mp=Read("MAXMP",bases.Mp,am);
            string speedStat=frame.Flying.Value?"FLY_SPEED":"SPEED";
            int speedBase=frame.Flying.Value?9000:frame.StateFlying.Value && !frame.Resting.Value?12000:frame.Walking.Value?3000:6000;
            GG.StatValue speed=Read(speedStat,speedBase),delay=Read("ATTACK_SPEED",BaseDelay(projection));
            return new(Status.Computed,new(health.Current,will.Current,hp.Current,mp.Current,bases.Hp,bases.Mp,ah,am,
                speed.Current,delay.Current,values.ToImmutable(),projection.Functions,
                projection.Functions.Where(f=>!R.ReadStats.Contains(f.Stat)).ToImmutableArray()),reads.ToImmutable(),null);
        }
        catch(GI.Domain ex){return new(Status.IncompleteReachedProgram,null,[],ex.Message);}
        catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {return new(Status.IncompleteReachedProgram,null,[],"reached source/current shape: "+ex.GetType().Name);}
    }
    private static (int Health,int Will,int HpMultiplier,int MpMultiplier) ClassParameters(int id)
    {
        (int,int,int,int)[] rows=[(110,90,400,400),(115,90,440,400),(100,105,460,400),(100,90,360,400),
            (100,90,360,400),(100,90,280,400),(90,115,260,600),(90,110,260,600),(90,115,280,600),
            (95,100,360,600),(110,110,320,600),(105,110,360,600),(100,90,360,400),(100,105,420,480),
            (105,100,360,400),(95,105,320,600),(100,110,320,520)];
        GI.Require(id is >=0 and <17,"explicit source class index");return rows[id];
    }
    private static bool Valid(R.Function function,Projection projection,GI.GeneralStatFrame frame)
    {
        foreach(R.Condition condition in function.Conditions)
        {
            switch(condition.Kind)
            {
                case R.Predicate.OnFly:if(frame.Flying!=true)return false;break;
                case R.Predicate.Weapon:
                    GI.Require(!condition.Weapons.IsDefault && condition.Weapons.All(x=>R.GroupToken(x)==x),"weapon predicate source list");
                    if(!condition.Weapons.Contains(MainGroup(projection),StringComparer.Ordinal))return false;break;
                case R.Predicate.Charge:
                    // Java instanceof Item is false for stone/effect/random owners, without reading charge.
                    if(function.OwnerType!=R.OwnerType.Item)return false;
                    GI.Require(!frame.Charge.IsDefault && frame.Charge.All(x=>x is not null && x.Current is not null),"reached original item charge");
                    GI.GeneralCharge[] charge=frame.Charge.Where(x=>x.Current.Owner==function.Owner.Owner).ToArray();
                    GI.Require(charge.Length==1,"selected item charge identity");charge[0].Current.Validate(frame.Operation,frame.Epoch);
                    GI.Require(charge[0].Current.Generation==function.Owner.Generation && charge[0].Points is >=0 and <=1000000,"current item charge generation/bounds");
                    if(GG.Tier(charge[0].Points)<condition.Value)return false;break;
                default:throw new GI.Domain("unknown reached condition");
            }
        }
        if(function.Program==R.Program.ShieldMastery)
        {GI.Require(frame.ShieldEquipped.HasValue,"reached current shield predicate");return frame.ShieldEquipped.Value;}
        if(function.Program==R.Program.WeaponMastery)
        {
            string? group=function.Stat=="OFF_HAND_POWER"?OffGroup(projection):MainGroup(projection);
            return group==function.Group;
        }
        return true;
    }
    private static Item? MainWeapon(Projection projection)
    {
        Item[] rows=projection.Items.Where(x=>x.OwnerType==R.OwnerType.Item && x.Equipped==true && (x.EquipmentMask&1)!=0
            && x.Source.Read().Element("weapon_stats") is not null).ToArray();
        GI.Require(rows.Length<=1,"one actual main-hand weapon/alias");return rows.FirstOrDefault();
    }
    private static Item? Off(Projection projection)
    {
        Item? main=MainWeapon(projection);
        Item[] rows=projection.Items.Where(x=>x.OwnerType==R.OwnerType.Item && x.Equipped==true && (x.EquipmentMask&2)!=0
            && x.Source.Read().Element("weapon_stats") is not null && x.Current.Owner!=main?.Current.Owner).ToArray();
        GI.Require(rows.Length<=1,"one actual offhand weapon excluding main alias");return rows.FirstOrDefault();
    }
    private static string MainGroup(Projection projection)=>MainWeapon(projection) is Item item?R.ItemGroup(item.Source.Read()):"NOWEAPON";
    private static string? OffGroup(Projection projection)=>Off(projection) is Item item?R.ItemGroup(item.Source.Read()):null;
    private static int BaseDelay(Projection projection)
    {
        Item? main=MainWeapon(projection);if(main is null)return 1500;
        int value=GI.Integer(R.Single(main.Source.Read(),"weapon_stats",true)!,"attack_speed",0);
        if(Off(projection) is Item off)value=unchecked(value+GI.Integer(R.Single(off.Source.Read(),"weapon_stats",true)!,"attack_speed",0)/4);
        return value;
    }
    private static R.Function? DuplicateDelay(Projection projection,GI.GeneralStatFrame frame)
    {
        Item? main=MainWeapon(projection);if(main is null)return null;
        var functions=new List<R.Function>();R.Function? firstMain=null,firstFusion=null;
        ImmutableArray<R.Function> Raw(Item owner,C.Catalog.Record source)
        {
            var rows=ImmutableArray.CreateBuilder<R.Function>();int ordinal=0;
            foreach(XElement node in R.Single(source.Read(),"modifiers",false)?.Elements() ?? [])
            {
                int index=ordinal++;if(node.Attribute("name")?.Value!="ATTACK_SPEED")continue;
                GI.FunctionKind kind=R.ParseKind(node.Name.LocalName);bool bonus=GI.Boolean(node,"bonus");
                var fn=new R.Function("duplicate/raw/"+owner.Current.Owner+"/"+source.Identity+"/"+index,"ATTACK_SPEED",node.Name.LocalName,
                    GI.Integer(node,"value",0),bonus,R.Program.Basic,kind,R.Conditions(node),owner.Current,R.OwnerType.Item,
                    source.Identity+"/modifiers/"+index,source.Identity,"predefined-duplicate",index,60,
                    OriginalObjectIdentity:source.Identity+"/raw-template-function/"+index);
                if(Valid(fn,projection,frame))rows.Add(fn);
            }
            return rows.ToImmutable();
        }
        var mainFunctions=Raw(main,main.Source);if(!mainFunctions.IsEmpty){firstMain=mainFunctions[0];functions.AddRange(mainFunctions);}
        if(main.Fusion==GI.Known.Present)
        {
            var fusion=Raw(main,main.FusionSource!);if(!fusion.IsEmpty){firstFusion=fusion[0];functions.AddRange(fusion);}
        }
        else if(Off(projection) is Item off)functions.AddRange(Raw(off,off.Source));
        if(firstMain is not null && firstFusion is not null)
        {
            static int Abs(int x)=>x<0?unchecked(-x):x;
            R.Function remove=Abs(firstMain.Value)>=Abs(firstFusion.Value)?firstFusion:firstMain;
            int index=functions.FindIndex(x=>x.OriginalObjectIdentity==remove.OriginalObjectIdentity);
            GI.Require(index>=0,"actual duplicate raw-reference removal");functions.RemoveAt(index);
        }
        // Java Stream.max keeps the first on an equal comparator result.
        R.Function? selected=null;foreach(var function in functions)if(selected is null || function.Value>selected.Value)selected=function;
        return selected;
    }
    private static void Source(C.Catalog catalog,C.Catalog.Record? source,C.Family family)
    {GI.Require(source is not null && ReferenceEquals(source.Owner,catalog) && source.Metadata.Key.Authority==C.Authority.CSharp
        && source.Metadata.Key.Family==family,"catalog/current source family");catalog.ValidateRecord(source);}
    private static void Floating(GI.Floating floating)=>GI.Require(floating is {RoundNearestEven:true,MaskedExceptions:true,
        FlushToZero:false,DenormalsAreZero:false,NormalReturn:true},"explicit source binary32 context");
}
