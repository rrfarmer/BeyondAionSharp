using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Source-pinned retail item HP/MP bonus lists for offline curse
/// analysis. It does not establish live application, item/fusion eligibility,
/// revision flags, other bonuses or complete player resource state.</summary>
internal sealed class RetailNpcCurseItemBonusSource
{
    internal const string ReviewedSourceSha="e5afc36026295acd35d91e6d57e139a915b73c2b827882dcba057f4cbb8ba6a6";
    internal const string ReviewedAuditSha="72607d020e1b7a511631eca0692bfc7a3b9ccbd32dced9fe0e7cde16b813f4b2";
    internal enum Failure { None, SourceUnavailable, UnknownItem, IdentityMismatch, UnreviewedApplication, UnreviewedCharge, UnreviewedItemState, UnreviewedRevisionContext }
    // Explicit native-stage state. Null means the caller has not established
    // that input; this never guesses the live player's high-level flag.
    internal readonly record struct RevisionContext(int? SpecialServerType,bool? ActorPresent,int? ActorType,
        bool? HighLevelFlag,int? BonusFlags);
    // Explicit values from the native main and fused Item::AdjustClassPermit
    // paths. Raw permits are needed when a native zero argument falls back.
    internal readonly record struct FusionLevels(ushort MainAdjusted,ushort SecondaryAdjusted,byte MainRaw,byte SecondaryRaw);

    // User::User initializes the flag from an explicit loaded threshold. Later
    // level changes and external state must still be observed separately.
    internal static bool InitialHighLevelFlag(ushort level,ushort threshold)=>level>=threshold;

    /// <summary>ItemBonusAttr2::CheckAndApply, 0x14046c84b..89c.
    /// True selects the revision owner, false selects the ordinary apply owner,
    /// and null means the supplied state cannot decide. This does not establish
    /// that an item or its bonus is eligible for a live player.</summary>
    internal static bool? SelectRevision(RevisionContext context)
    {
        if(context.BonusFlags is not { } flags)return null;
        if((flags&0x805)!=0)return false;
        if(context.SpecialServerType is not (0 or 1 or 10))return null;
        if(context.SpecialServerType!=0)return true;
        if(context.ActorPresent is not { } present)return null;
        if(!present)return false;
        if(context.ActorType is not { } kind)return null;
        if(kind!=1)return false;
        return context.HighLevelFlag;
    }
    internal sealed record Operand(int Value,bool Percent,string Tag);
    internal sealed record Field(ImmutableArray<Operand?> Values,string ComparisonStatus);
    internal sealed record Candidate(int ItemId,string Name,string HostGroup,string Kind,string? Apply,Field Hp,Field Mp)
    {
        /// <summary>Project the secondary item's normal and conditioning
        /// bonuses for the reviewed TwoHandWeapon path. The supplied levels,
        /// actor, linked-item state and source identity must be established by
        /// the caller; this does not authorize a live fusion or curse.</summary>
        internal Failure ResolveFusedSelected(RetailNpcCurseStatMath.Resource resource,int chargePoints,ushort playerLevel,
            FusionLevels levels,RevisionContext normal,RevisionContext charge,out RetailNpcCurseStatMath.ItemResourceBonus result)
        {
            result=default;
            if(Kind!="weapon")return Failure.UnreviewedItemState;
            if(normal.BonusFlags!=0x80 ||chargePoints>0 && charge.BonusFlags!=0x200)
                return Failure.UnreviewedRevisionContext;
            ushort normalLevel=Math.Min(levels.MainAdjusted,levels.SecondaryAdjusted);
            if(normalLevel==0)normalLevel=levels.MainRaw; // Native normal path receives the main item definition.
            ushort chargeLevel=levels.MainAdjusted!=0?levels.MainAdjusted:levels.SecondaryRaw;
            var status=ResolveSelected(resource,0,normalLevel,playerLevel,normal,default,out var normalBonus);
            if(status!=Failure.None)return status;
            if(chargePoints==0){result=normalBonus;return Failure.None;}
            status=ResolveSelected(resource,chargePoints,chargeLevel,playerLevel,normal,charge,out var withCharge);
            if(status!=Failure.None)return status;
            status=ResolveSelected(resource,0,chargeLevel,playerLevel,normal,default,out var normalAtChargeLevel);
            if(status!=Failure.None)return status;
            result=new(unchecked(normalBonus.Points+withCharge.Points-normalAtChargeLevel.Points),
                unchecked(normalBonus.Percent+withCharge.Percent-normalAtChargeLevel.Percent));
            return Failure.None;
        }

        /// <summary>Resolve the ordinary armor wear path using the matching
        /// retail class permit and an explicitly supplied native adjustment.
        /// This does not establish equipped state, a live adjustment, set or
        /// fusion bonuses, or that a particular virtual override was selected.</summary>
        internal Failure ResolveArmorSelected(RetailNpcCurseArmorSource.Candidate armor,int nativeClass,
            sbyte? levelAdjustment,ushort? highLevelFloor,RetailNpcCurseStatMath.Resource resource,int chargePoints,
            ushort playerLevel,RevisionContext normal,RevisionContext charge,out RetailNpcCurseStatMath.ItemResourceBonus result)
        {
            result=default;
            ArgumentNullException.ThrowIfNull(armor);
            string group=armor.Table.ToUpperInvariant();
            if(group.EndsWith("_GLOVES",StringComparison.Ordinal))group=group[..^1];
            if(Kind!="armor" || ItemId!=armor.ItemId || !string.Equals(Name,armor.Name,StringComparison.OrdinalIgnoreCase)
                ||HostGroup!=group)return Failure.IdentityMismatch;
            // Mode 2 is the independent item-set level branch, not a normal
            // armor or conditioning bonus. Its input is not modeled here.
            if(normal.BonusFlags is not { } flags || (flags&2)!=0
                ||chargePoints>0 && (charge.BonusFlags is not { } chargeFlags || (chargeFlags&2)!=0))
                return Failure.UnreviewedRevisionContext;
            if(!armor.TryGetClassPermit(nativeClass,out byte permit,out _))return Failure.UnreviewedRevisionContext;
            int required=permit;
            if(permit!=0)
            {
                if(levelAdjustment is not { } adjustment)return Failure.UnreviewedRevisionContext;
                if(adjustment!=0 && highLevelFloor is null)return Failure.UnreviewedRevisionContext;
                required=RetailNpcCurseArmorSource.AdjustClassPermit(permit,adjustment,highLevelFloor.GetValueOrDefault());
            }
            if(required is <0 or >ushort.MaxValue)return Failure.UnreviewedRevisionContext;
            return ResolveSelected(resource,chargePoints,(ushort)required,playerLevel,normal,charge,out result);
        }

        /// <summary>Resolve only explicit, coherent User revision contexts.
        /// The item-level input and current actor/equipment state remain the
        /// caller's responsibility; no gameplay effect is enabled here.</summary>
        internal Failure ResolveSelected(RetailNpcCurseStatMath.Resource resource,int chargePoints,ushort itemLevel,ushort playerLevel,
            RevisionContext normal,RevisionContext charge,out RetailNpcCurseStatMath.ItemResourceBonus result)
        {
            result=default;
            if(Apply!="equip")return Failure.UnreviewedApplication;
            if(chargePoints is <0 or >1000000)return Failure.UnreviewedCharge;
            if(normal.ActorPresent!=true ||normal.ActorType!=1)return Failure.UnreviewedRevisionContext;
            bool? normalRevision=SelectRevision(normal);
            if(normalRevision is null)return Failure.UnreviewedRevisionContext;
            bool chargeRevision=false; // Unused unless the charge list is active.
            if(chargePoints>0)
            {
                if(charge.SpecialServerType!=normal.SpecialServerType ||charge.ActorPresent!=normal.ActorPresent
                    ||charge.ActorType!=normal.ActorType ||charge.HighLevelFlag!=normal.HighLevelFlag)
                    return Failure.UnreviewedRevisionContext;
                bool? selected=SelectRevision(charge);
                if(selected is null)return Failure.UnreviewedRevisionContext;
                chargeRevision=selected.Value;
            }
            return Resolve(resource,chargePoints,itemLevel,playerLevel,normalRevision.Value,chargeRevision,out result);
        }

        /// <summary>Apply the reviewed charge-list rule and explicitly supplied
        /// revision branches. This projects only item-template bonus operands.</summary>
        internal Failure Resolve(RetailNpcCurseStatMath.Resource resource,int chargePoints,ushort itemLevel,ushort playerLevel,
            bool normalRevision,bool chargeRevision,out RetailNpcCurseStatMath.ItemResourceBonus result)
        {
            result=default;
            if(Apply!="equip")return Failure.UnreviewedApplication;
            if(chargePoints is <0 or >1000000)return Failure.UnreviewedCharge;
            Field field=resource switch {RetailNpcCurseStatMath.Resource.Hp=>Hp,RetailNpcCurseStatMath.Resource.Mp=>Mp,
                _=>throw new ArgumentOutOfRangeException(nameof(resource))};
            int active=chargePoints==0?0:chargePoints<=500000?1:2;
            for(int i=0;i<=active;i++)
            {
                var value=field.Values[i];if(value is null)continue;
                var raw=value.Percent?new RetailNpcCurseStatMath.ItemResourceBonus(0,value.Value)
                    :new RetailNpcCurseStatMath.ItemResourceBonus(value.Value,0);
                var revised=RetailNpcCurseStatMath.ReviseItemBonus(resource,raw,itemLevel,playerLevel,i==0?normalRevision:chargeRevision);
                result=new(unchecked(result.Points+revised.Points),unchecked(result.Percent+revised.Percent));
            }
            return Failure.None;
        }
    }
    private readonly FrozenDictionary<int,Candidate> items;
    private RetailNpcCurseItemBonusSource(Dictionary<int,Candidate> items)=>this.items=items.ToFrozenDictionary();
    internal int ItemCount=>items.Count;
    internal static RetailNpcCurseItemBonusSource Parse(byte[] bytes,string sha)
    {
        if(sha!=ReviewedSourceSha)throw new InvalidDataException("Unreviewed item bonus source identity");
        RetailNpcStatCatalog.CheckHash(bytes,sha,"curse item bonus source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=16});var root=doc.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1||root.GetProperty("purpose").GetString()!="retail-item-resource-bonus-source-v1"
            ||root.GetProperty("audit_sha256").GetString()!=ReviewedAuditSha)throw new InvalidDataException("Unsupported item bonus source");
        var items=new Dictionary<int,Candidate>();
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32();string? name=row.GetProperty("name").GetString();
            string? group=row.GetProperty("host_group").GetString(),kind=row.GetProperty("kind").GetString();
            var applyNode=row.GetProperty("retail_apply");string? apply=applyNode.ValueKind==JsonValueKind.Null?null:applyNode.GetString();
            if(id<=0||string.IsNullOrEmpty(name)||name.Any(c=>c>0x7f)||string.IsNullOrEmpty(group)||group.Any(c=>c>0x7f)
                ||kind is not ("armor" or "weapon" or "other")||apply is not (null or "equip" or "inventory")
                ||!items.TryAdd(id,new(id,name,group,kind,apply,ParseField(row.GetProperty("hp")),ParseField(row.GetProperty("mp")))))
                throw new InvalidDataException("Invalid item bonus source identity");
        }
        if(items.Count!=54153)throw new InvalidDataException("Item bonus source coverage changed");
        return new(items);
    }
    private static Field ParseField(JsonElement field)
    {
        string? status=field.GetProperty("comparison_status").GetString();
        if(status is not ("fields_match" or "fields_differ" or "alternate_retail_list" or "complex_host_modifier" or "retail_percent" or "unreviewed_application"))
            throw new InvalidDataException("Unknown item bonus comparison status");
        var entries=field.GetProperty("values");if(entries.GetArrayLength()!=3)throw new InvalidDataException("Invalid bonus list count");
        var values=ImmutableArray.CreateBuilder<Operand?>(3);int index=0;
        foreach(var entry in entries.EnumerateArray())
        {
            if(entry.ValueKind==JsonValueKind.Null){values.Add(null);index++;continue;}
            string? tag=entry.GetProperty("tag").GetString();string prefix=index==0?"bonus_attr":index==1?"bonus_attr_a":"bonus_attr_b";
            if(tag is null||!tag.StartsWith(prefix,StringComparison.Ordinal)||!int.TryParse(tag.AsSpan(prefix.Length),out int n)
                ||n<1||n>(index==0?12:4))throw new InvalidDataException("Invalid item bonus source tag");
            values.Add(new(entry.GetProperty("value").GetInt32(),entry.GetProperty("percent").GetBoolean(),tag));index++;
        }
        return new(values.MoveToImmutable(),status);
    }
    internal Failure Lookup(RetailNpcCurseOtherEquipmentHpSource.Candidate candidate,out Candidate? result)
    {ArgumentNullException.ThrowIfNull(candidate);return Lookup(candidate.ItemId,candidate.Name,candidate.HostGroup,"other",out result);}
    internal Failure Lookup(RetailNpcCurseWeaponHpSource.Candidate candidate,out Candidate? result)
    {ArgumentNullException.ThrowIfNull(candidate);return Lookup(candidate.ItemId,candidate.Name,candidate.HostGroup,"weapon",out result);}
    internal Failure Lookup(RetailNpcCurseArmorSource.Candidate candidate,out Candidate? result)
    {
        ArgumentNullException.ThrowIfNull(candidate);string group=candidate.Table.ToUpperInvariant();
        if(group.EndsWith("_GLOVES",StringComparison.Ordinal))group=group[..^1];
        return Lookup(candidate.ItemId,candidate.Name,group,"armor",out result);
    }
    private Failure Lookup(int id,string name,string group,string kind,out Candidate? result)
    {
        result=null;if(!items.TryGetValue(id,out var found))return Failure.UnknownItem;
        if(!string.Equals(found.Name,name,StringComparison.OrdinalIgnoreCase)||found.HostGroup!=group||found.Kind!=kind)
            return Failure.IdentityMismatch;
        if(found.Apply!="equip")return Failure.UnreviewedApplication;
        result=found;return Failure.None;
    }
}
