using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Point-in-time item-specific resistance input for offline fixtures.
/// Item eligibility and a snapshot through combat remain separate gates.</summary>
internal static class RetailNpcPlayerCurseItemCapture
{
    internal sealed record Entry(int Id,string Name,string Group,int Flat);
    internal sealed record DirectEntry(int Id,string Name,string Group,string Kind,int? CurseFlat);
    internal sealed class DirectCatalog
    {
        private readonly Dictionary<int,DirectEntry> entries;
        private DirectCatalog(Dictionary<int,DirectEntry> entries)=>this.entries=entries;
        internal bool TryGet(int id,out DirectEntry entry)=>entries.TryGetValue(id,out entry!);

        internal static DirectCatalog LoadForOfflineFixture(ReadOnlySpan<byte> bytes,string expectedSha)
        {
            if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=expectedSha)
                throw new InvalidDataException("Zero curse item catalog hash differs");
            using var doc=JsonDocument.Parse(bytes.ToArray());var root=doc.RootElement;
            if(root.GetProperty("version").GetInt32()!=2
                ||root.GetProperty("purpose").GetString()!="offline-reviewed-direct-item-status-resistance-v2")
                throw new InvalidDataException("Direct status item catalog identity differs");
            var entries=new Dictionary<int,DirectEntry>();
            foreach(var row in root.GetProperty("records").EnumerateArray())
            {
                int id=row.GetProperty("id").GetInt32();string name=row.GetProperty("name").GetString()!;
                string group=row.GetProperty("group").GetString()!,kind=row.GetProperty("kind").GetString()!;
                var raw=row.GetProperty("curse_flat");int? flat=raw.ValueKind==JsonValueKind.Null?null:raw.GetInt32();
                if(id<=0||string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(group)
                    ||flat is <0||kind is not ("weapon" or "armor" or "other")||!entries.TryAdd(id,new(id,name,group,kind,flat)))
                    throw new InvalidDataException("Invalid direct status item binding");
            }
            if(entries.Count==0)throw new InvalidDataException("Empty direct status item catalog");
            return new(entries);
        }
    }

    internal sealed class Catalog
    {
        private readonly Dictionary<int,Entry> entries;
        private Catalog(Dictionary<int,Entry> entries)=>this.entries=entries;
        internal bool TryGet(int id,out Entry entry)=>entries.TryGetValue(id,out entry!);

        internal static Catalog LoadForOfflineFixture(ReadOnlySpan<byte> bytes,string expectedSha,string directory)
        {
            if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=expectedSha)
                throw new InvalidDataException("Player curse item catalog hash differs");
            using var doc=JsonDocument.Parse(bytes.ToArray());var root=doc.RootElement;
            if(root.GetProperty("version").GetInt32()!=1)throw new InvalidDataException("Curse item catalog version differs");
            var entries=new Dictionary<int,Entry>();
            foreach(var row in root.GetProperty("records").EnumerateArray())
            {
                int id=row.GetProperty("id").GetInt32(),flat=row.GetProperty("flat").GetInt32();
                string name=row.GetProperty("name").GetString()!,group=row.GetProperty("group").GetString()!;
                string sha=row.GetProperty("sha256").GetString()!,file=row.GetProperty("file").GetString()!;
                string field=row.GetProperty("retail_field").GetString()!,text=row.GetProperty("retail_text").GetString()!;
                if(id<=0||flat<0||string.IsNullOrWhiteSpace(name)||file!=$"item-{id}.utf16le.xml"
                    ||!field.StartsWith("bonus_attr",StringComparison.Ordinal))throw new InvalidDataException("Invalid curse item binding");
                byte[] raw=File.ReadAllBytes(Path.Combine(directory,file));
                if(raw.Length==0||raw.Length>131072||raw.Length%2!=0||Convert.ToHexStringLower(SHA256.HashData(raw))!=sha)
                    throw new InvalidDataException("Curse item source hash differs");
                using var reader=XmlReader.Create(new StringReader(new UnicodeEncoding(false,false,true).GetString(raw)),
                    new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536});
                var item=XDocument.Load(reader).Root;
                var parts=text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
                if(item is null||item.Name!="item"||item.Elements("id").SingleOrDefault()?.Value!=id.ToString()
                    ||item.Elements("name").SingleOrDefault()?.Value!=name
                    ||item.Elements(field).SingleOrDefault()?.Value!=text||parts.Length!=2
                    ||!parts[0].Equals("arCurse",StringComparison.OrdinalIgnoreCase)
                    ||!int.TryParse(parts[1],out int parsed)||parsed!=flat
                    ||!entries.TryAdd(id,new(id,name,group,flat)))throw new InvalidDataException("Curse item source differs");
            }
            if(entries.Count==0)throw new InvalidDataException("Empty curse item catalog");
            return new(entries);
        }
    }

    internal static bool TryCaptureForOfflineFixture(Player target,Catalog catalog,
        out RetailNpcStatSpellDrain.CurseModifiers modifier,DirectCatalog? direct=null,
        RetailNpcPlayerCurseAbsoluteCapture.Receipt? absolute=null)
    {
        modifier=default;
        if(!target.IsSpawned()||target.IsDead())return false;
        var stats=target.GetGameStats();var equipment=target.GetEquipment();
        var stoneSource=RetailNpcStatCatalog.CurseManastoneSource;
        lock(equipment)
        {
            if(!stats.TryCaptureRetailStatMutation(out long stamp)||absolute is not null&&!absolute.IsCurrent(target))return false;
            var slots=equipment.InspectEquippedSlots(s=>s.ToArray());
            var functions=stats.GetStatsSorted(StatEnum.CURSE_RESISTANCE);
            var seen=new HashSet<Item>(ReferenceEqualityComparer.Instance);int flat=0;
            foreach(var function in functions)
            {
                var leaf=function is StatFunctionProxy existing?existing.GetProxiedFunction():function;
                if(leaf is StatAbsFunction)
                {
                    if(absolute is null||!absolute.Covers(function))return false;
                    continue;
                }
                if(function is not StatFunctionProxy proxy||proxy.GetOwner() is not Item item||!seen.Add(item)
                    ||!item.IsEquipped()||!Unmodified(item,stoneSource)||!catalog.TryGet(item.GetItemId(),out var source)||source.Group!="RING"
                    ||!ReferenceEquals(item.GetItemTemplate(),DataManager.ITEM_DATA.GetItemTemplate(source.Id))
                    ||!string.Equals(item.GetItemTemplate().GetRetailSourceName(),source.Name,StringComparison.OrdinalIgnoreCase)
                    ||item.GetItemTemplate().GetItemGroup().ToString()!=source.Group
                    ||!slots.Any(s=>ReferenceEquals(s.Value,item)&&s.Key==item.GetEquipmentSlot()
                        && (s.Key==(long)ItemSlot.RING_LEFT||s.Key==(long)ItemSlot.RING_RIGHT)))return false;
                if(leaf.GetType()!=typeof(StatAddFunction)||!item.GetCurrentModifiers().Any(f=>ReferenceEquals(f,leaf))
                    ||proxy.HasConditions()||!proxy.IsBonus()||proxy.GetValue()!=source.Flat)return false;
                if(direct is not null&&(!direct.TryGet(item.GetItemId(),out var compared)
                    ||compared.CurseFlat!=source.Flat||compared.Group!=source.Group
                    ||!string.Equals(compared.Name,source.Name,StringComparison.OrdinalIgnoreCase)))return false;
                flat=unchecked(flat+source.Flat);
            }
            // Missing host functions do not imply zero. Independently admit
            // only source-matched items whose direct 5.8 and host fields are
            // both absent, and only in a bounded unmodified equipment state.
            const long retailSlots=((1L<<21)-1)&~((1L<<17)|(1L<<18));
            foreach(var slot in slots)
            {
                var item=slot.Value;
                if(seen.Contains(item))
                {
                    if(slot.Key!=item.GetEquipmentSlot()
                        ||(slot.Key!=(long)ItemSlot.RING_LEFT&&slot.Key!=(long)ItemSlot.RING_RIGHT))return false;
                    continue;
                }
                if(direct is null||!direct.TryGet(item.GetItemId(),out var source)||source.CurseFlat is not null||!item.IsEquipped()
                    ||slot.Key<=0||(slot.Key&(slot.Key-1))!=0||(slot.Key&retailSlots)==0
                    ||(item.GetEquipmentSlot()&slot.Key)!=slot.Key
                    ||!ReferenceEquals(item.GetItemTemplate(),DataManager.ITEM_DATA.GetItemTemplate(source.Id))
                    ||!string.Equals(item.GetItemTemplate().GetRetailSourceName(),source.Name,StringComparison.OrdinalIgnoreCase)
                    ||item.GetItemTemplate().GetItemGroup().ToString()!=source.Group||!Unmodified(item,stoneSource)
                    ||item.GetCurrentModifiers().Any(f=>f.GetName()==StatEnum.CURSE_RESISTANCE))return false;
            }
            if(!stats.RetailStatMutationUnchanged(stamp)||absolute is not null&&!absolute.IsCurrent(target)
                ||!ReferenceEquals(RetailNpcStatCatalog.CurseManastoneSource,stoneSource))return false;
            modifier=new(0,flat);return true;
        }
    }

    private static bool Unmodified(Item item,RetailNpcCurseManastoneSource? stoneSource)
        => item.GetEnchantLevel()==0&&item.GetTempering()==0&&!item.HasFusionStones()
            &&!item.HasFusionedItem()&&!item.HasIdianStone()&&!item.HasGodStone()&&item.GetBonusStatsId()==0
            &&item.GetBonusStatsEffect() is null&&item.GetFusionedItemBonusStatsEffect() is null
            &&item.GetEnchantEffect() is null&&item.GetTemperingEffect() is null
            &&item.GetConditioningInfo() is null&&item.GetBuffSkill()==0&&!item.GetItemTemplate().IsItemSet()
            &&RetailNpcCurseResourcePlanBuilder.HasReviewedResourceManastones(item,stoneSource);
}
