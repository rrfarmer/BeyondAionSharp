using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Offline 5.8 item physical-defense bonus inputs. No item or
/// conditioning eligibility, player state, other modifiers or live skill is implied.</summary>
internal sealed class RetailNpcItemDefenseBonusSource
{
    internal enum Failure { None, UnknownItem, IdentityMismatch, UnreviewedApplication, UnreviewedCharge, UnreviewedRevision }
    internal sealed record Operand(int Value,string Tag);
    internal sealed record Candidate(int Id,string Name,string Group,string Kind,string? Apply,ImmutableArray<Operand?> Values)
    {
        internal Failure ResolveFused(RetailNpcCurseItemBonusSource.Candidate identity,int chargePoints,ushort playerLevel,
            RetailNpcCurseItemBonusSource.FusionLevels levels,RetailNpcCurseItemBonusSource.RevisionContext normal,
            RetailNpcCurseItemBonusSource.RevisionContext charge,out int points)
        {
            points=0;
            if(Kind!="weapon"||normal.BonusFlags!=0x80||chargePoints>0&&charge.BonusFlags!=0x200)
                return Failure.UnreviewedRevision;
            ushort normalLevel=Math.Min(levels.MainAdjusted,levels.SecondaryAdjusted);
            if(normalLevel==0)normalLevel=levels.MainRaw;
            ushort chargeLevel=levels.MainAdjusted!=0?levels.MainAdjusted:levels.SecondaryRaw;
            var status=Resolve(identity,0,normalLevel,playerLevel,normal,default,out int normalPoints);
            if(status!=Failure.None)return status;
            if(chargePoints==0){points=normalPoints;return Failure.None;}
            status=Resolve(identity,chargePoints,chargeLevel,playerLevel,normal,charge,out int withCharge);
            if(status!=Failure.None)return status;
            status=Resolve(identity,0,chargeLevel,playerLevel,normal,default,out int normalAtCharge);
            if(status!=Failure.None)return status;
            points=unchecked(normalPoints+withCharge-normalAtCharge);return Failure.None;
        }
        internal Failure Resolve(RetailNpcCurseItemBonusSource.Candidate identity,int chargePoints,ushort itemLevel,ushort playerLevel,
            RetailNpcCurseItemBonusSource.RevisionContext normal,RetailNpcCurseItemBonusSource.RevisionContext charge,out int points)
        {
            points=0;ArgumentNullException.ThrowIfNull(identity);
            if(identity.ItemId!=Id||!string.Equals(identity.Name,Name,StringComparison.OrdinalIgnoreCase)
                ||identity.HostGroup!=Group||identity.Kind!=Kind||identity.Apply!=Apply)return Failure.IdentityMismatch;
            if(Apply!="equip")return Failure.UnreviewedApplication;
            if(chargePoints is <0 or >1000000)return Failure.UnreviewedCharge;
            bool? normalRevision=RetailNpcCurseItemBonusSource.SelectRevision(normal);
            if(normalRevision is null)return Failure.UnreviewedRevision;
            bool chargeRevision=false;
            if(chargePoints>0)
            {
                if(normal.SpecialServerType!=charge.SpecialServerType||normal.ActorPresent!=charge.ActorPresent
                    ||normal.ActorType!=charge.ActorType||normal.HighLevelFlag!=charge.HighLevelFlag)
                    return Failure.UnreviewedRevision;
                bool? selected=RetailNpcCurseItemBonusSource.SelectRevision(charge);
                if(selected is null)return Failure.UnreviewedRevision;
                chargeRevision=selected.Value;
            }
            int active=chargePoints==0?0:chargePoints<=500000?1:2;
            for(int i=0;i<=active;i++)
            {
                if(Values[i] is not { } value)continue;
                // The native revision helper applies the same binary32 formula
                // to physicalDefend attribute 26 as to HP and MP.
                int revised=RetailNpcCurseStatMath.ReviseItemBonus(RetailNpcCurseStatMath.Resource.Hp,
                    new(value.Value,0),itemLevel,playerLevel,i==0?normalRevision.Value:chargeRevision).Points;
                points=unchecked(points+revised);
            }
            return Failure.None;
        }
    }
    private readonly FrozenDictionary<int,Candidate> items;
    private RetailNpcItemDefenseBonusSource(Dictionary<int,Candidate> items)=>this.items=items.ToFrozenDictionary();
    internal int ItemCount=>items.Count;
    internal static RetailNpcItemDefenseBonusSource ParseForOfflineFixture(byte[] bytes,string sha)
    {
        if(sha!="0bc098ba47c2f8d063b0afb47e1d4451d28abc594c6a4a2f5f510ade769ebcdd")
            throw new InvalidDataException("Unreviewed physical-defense item bonuses");
        RetailNpcStatCatalog.CheckHash(bytes,sha,"physical-defense item bonus source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=12});var root=doc.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1||root.GetProperty("purpose").GetString()!="offline-reviewed-physical-defense-item-bonus"
            ||root.GetProperty("region").GetString()!="USA")throw new InvalidDataException("Unreviewed item defense bonus identity");
        var provenance=root.GetProperty("provenance");
        foreach(var (key,value) in new[]{
            ("identity_sha256","e5afc36026295acd35d91e6d57e139a915b73c2b827882dcba057f4cbb8ba6a6"),
            ("armors_sha256","11167024df7400bf6ff051f84bcf6ef1dffdec1949b33ff409f42e39e3e9716a"),
            ("accessories_sha256","7ce2457a34b8082826146804a1af8223e9e8f36d9e4c1a41603baea56f2d1470"),
            ("weapons_sha256","f10df47b68c161ba1d76aa1f4bd2853634a57e114dc61c50f63e89d2e657344f")})
            if(provenance.GetProperty(key).GetString()!=value)throw new InvalidDataException("Unreviewed item bonus provenance: "+key);
        if(provenance.GetProperty("selection").GetString()!="USA missing; base fallback")throw new InvalidDataException("Unreviewed item bonus region");
        var items=new Dictionary<int,Candidate>();int[] counts=new int[3];
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32();string? name=row.GetProperty("name").GetString();
            string? group=row.GetProperty("host_group").GetString(),kind=row.GetProperty("kind").GetString();
            var mode=row.GetProperty("retail_apply");string? apply=mode.ValueKind==JsonValueKind.Null?null:mode.GetString();
            var values=row.GetProperty("values");if(values.GetArrayLength()!=3)throw new InvalidDataException("Invalid defense bonus lists");
            var result=ImmutableArray.CreateBuilder<Operand?>(3);int i=0;
            foreach(var x in values.EnumerateArray())
            {
                if(x.ValueKind==JsonValueKind.Null){result.Add(null);i++;continue;}
                int value=x.GetProperty("value").GetInt32();string? tag=x.GetProperty("tag").GetString();
                string prefix=i==0?"bonus_attr":i==1?"bonus_attr_a":"bonus_attr_b";
                if(tag is null||!tag.StartsWith(prefix,StringComparison.Ordinal)||!int.TryParse(tag.AsSpan(prefix.Length),out int n)
                    ||n<1||n>(i==0?12:4)||apply!="equip")throw new InvalidDataException("Invalid defense bonus operand");
                counts[i]++;result.Add(new(value,tag));i++;
            }
            if(id<=0||string.IsNullOrEmpty(name)||name.Any(c=>c>0x7f)||string.IsNullOrEmpty(group)
                ||kind is not ("armor" or "weapon" or "other")||apply is not (null or "equip" or "inventory")
                ||!items.TryAdd(id,new(id,name,group,kind,apply,result.MoveToImmutable())))
                throw new InvalidDataException("Invalid item defense bonus row");
        }
        if(items.Count!=54153||counts is not [10562,179,0])throw new InvalidDataException("Unexpected item defense bonus coverage");
        return new(items);
    }
    internal Failure Lookup(RetailNpcCurseItemBonusSource.Candidate identity,out Candidate? candidate)
    {
        ArgumentNullException.ThrowIfNull(identity);candidate=null;
        if(!items.TryGetValue(identity.ItemId,out var item))return Failure.UnknownItem;
        if(!string.Equals(identity.Name,item.Name,StringComparison.OrdinalIgnoreCase)||identity.HostGroup!=item.Group
            ||identity.Kind!=item.Kind||identity.Apply!=item.Apply)return Failure.IdentityMismatch;
        candidate=item;return Failure.None;
    }
}
