using System.Runtime.CompilerServices;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Retail;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using F=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

internal sealed partial class RetailNpcStatEquipmentResponses
{
    internal sealed record Definition(string Name,string Tag,bool NormalPercent,int NormalFlat,int NormalRate,
        bool SkillPercent,int SkillFlat,int SkillRate,ushort Immune)
    {
        internal static Definition From(IReadOnlyDictionary<string,string> fields)
        {
            static (bool Percent,int Flat,int Rate) Value(string? text)
            {
                if(text is null)return(false,0,0);
                bool percent=text.EndsWith('%');
                int value=int.Parse(percent?text[..^1]:text,System.Globalization.CultureInfo.InvariantCulture);
                return(percent,percent?0:value,percent?value:0);
            }
            var normal=Value(fields.GetValueOrDefault("damage_reduce_normal"));
            var skill=Value(fields.GetValueOrDefault("damage_reduce_skill"));
            return new(fields["name"],fields["tag"],normal.Percent,normal.Flat,normal.Rate,skill.Percent,skill.Flat,skill.Rate,
                unchecked((ushort)int.Parse(fields.GetValueOrDefault("status_effect_immune","0"),System.Globalization.CultureInfo.InvariantCulture)));
        }
    }
    internal readonly record struct Response(string Tag,bool Matched,bool NormalPercent,int NormalFlat,int NormalRate,
        bool SkillPercent,int SkillFlat,int SkillRate,ushort Immune)
    {
        internal static Response Missing(string tag)=>new(tag,false,false,0,0,false,0,0,0);
        internal static Response First(Definition d)=>new(d.Tag,true,d.NormalPercent,d.NormalFlat,d.NormalRate,d.SkillPercent,d.SkillFlat,d.SkillRate,d.Immune);
        internal Response Adjust(Definition d,int sign)=>this with
        {
            NormalFlat=unchecked(NormalFlat+sign*d.NormalFlat),NormalRate=unchecked(NormalRate+sign*d.NormalRate),
            SkillFlat=unchecked(SkillFlat+sign*d.SkillFlat),SkillRate=unchecked(SkillRate+sign*d.SkillRate),
            Immune=unchecked((ushort)(Immune+sign*d.Immune))
        };
        internal float Normal(float amount)=>!Matched?amount:FloatStage(amount,NormalPercent,NormalFlat,NormalRate);
        internal float PhysicalValue(int amount)=>FloatStage(F.FromInt(amount),SkillPercent,SkillFlat,SkillRate);
        internal int Physical(int amount)=>!Matched?amount:F.Cvtt(PhysicalValue(amount));
        internal int Spell(int amount)=>!Matched?amount:Math.Max(1,unchecked(amount-(SkillPercent?unchecked(SkillRate*amount)/100:SkillFlat)));
        private static float FloatStage(float amount,bool percent,int flat,int rate)
        {
            float reduction=percent?F.Div(F.Mul(F.FromInt(rate),amount),100f):F.FromInt(flat);
            return Math.Max(1f,F.Sub(amount,reduction));
        }
    }

    // Logical item owners, not a current-set recalculation. Native map entries
    // and first-writer flags persist even when every scalar returns to zero.
    internal sealed class Ledger(RetailNpcStatEquipmentResponses context)
    {
        internal readonly RetailNpcStatEquipmentResponses Context=context;
        internal readonly Dictionary<int,(int Item,long Slot)> Owners=[];
        internal readonly Dictionary<string,Response> Entries=new(StringComparer.OrdinalIgnoreCase);
        internal readonly List<(int Owner,int Item,long Slot)> Arrival=[];
        internal bool Restored;
        internal string? Invalid;
        internal void Equip(int owner,int item,long slot=0)
        {
            if(!Context.known.Contains(item)){Invalid??="source-exclusive-unknown-equipped-item-"+item;return;}
            if(!Context.itemResponses.TryGetValue(item,out var parts))return;
            if(Owners.TryGetValue(owner,out var prior))
            {
                if(prior!=(item,slot))Invalid??="source-exclusive-owner-identity-drift";
                return; // Bridge registration is once per logical physical item.
            }
            Owners.Add(owner,(item,slot));
            foreach(var d in parts)Entries[d.Tag]=Entries.TryGetValue(d.Tag,out var old)?old.Adjust(d,1):Response.First(d);
        }
        internal void Unequip(int owner,int item)
        {
            if(!Context.known.Contains(item)){Invalid??="source-exclusive-unknown-equipped-item-"+item;return;}
            if(!Context.itemResponses.TryGetValue(item,out var parts))return;
            if(!Owners.Remove(owner,out var prior)||prior.Item!=item){Invalid??="source-exclusive-unbalanced-owner-removal";return;}
            foreach(var d in parts)
            {
                if(!Entries.TryGetValue(d.Tag,out var old)){Invalid??="source-exclusive-missing-tag-history";continue;}
                Entries[d.Tag]=old.Adjust(d,-1); // Never erase/reseed the flags.
            }
        }
        internal Response Get(string tag)=>Entries.TryGetValue(tag,out var value)?value:Response.Missing(tag);
    }
    private static readonly ConditionalWeakTable<Player,Ledger> Actors=new();
    private static readonly ILogger Log=AionLog.For(nameof(RetailNpcStatEquipmentResponses));
    internal static void BeginActor(Player player)
    {
        if(RetailNpcStatCatalog.EquipmentResponses is { } context)Actors.Add(player,new(context));
    }
    internal static void Loaded(Player player,Item item)
    {
        if(!Actors.TryGetValue(player,out var state))return;
        lock(state)state.Arrival.Add((item.GetObjectId(),item.GetItemId(),item.GetEquipmentSlot()));
    }
    internal static void RestoreLoaded(Player player,IEnumerable<Item> equipped)
    {
        if(!Actors.TryGetValue(player,out var state))return;
        var actual=equipped.ToDictionary(x=>x.GetObjectId(),x=>x);
        lock(state)
        {
            if(state.Restored)return;
            // Native fresh Armor kind0 appends; LoadItems restores armor in
            // inventory/DB-arrival order. SQL promises no slot sorting. Retain
            // our actual4.8 input order separately from ordinary stat listeners.
            foreach(var row in state.Arrival)
                if(actual.TryGetValue(row.Owner,out var item)&&item.GetItemId()==row.Item&&item.GetEquipmentSlot()==row.Slot)
                    state.Equip(row.Owner,row.Item,row.Slot);
            state.Arrival.Clear();state.Restored=true;
        }
    }
    internal static void Equipped(Player player,Item item)
    {
        if(Actors.TryGetValue(player,out var state))lock(state)state.Equip(item.GetObjectId(),item.GetItemId(),item.GetEquipmentSlot());
    }
    internal static void Unequipped(Player player,Item item)
    {
        if(Actors.TryGetValue(player,out var state))lock(state)state.Unequip(item.GetObjectId(),item.GetItemId());
    }
    internal sealed class Handle(RetailNpcStatEquipmentResponses context,Player target,string tag,int npc)
    {
        internal Response Current()
        {
            if(!context.TryCurrent(target,tag,out var value,out string reason))
                throw new InvalidOperationException("Exclusive response changed to unsupported input during calculation: "+reason);
            return value;
        }
        internal float Normal(float amount,string hand)
        {
            var value=Current();float result=value.Normal(amount);Trace(value,"normal-"+hand,amount,result);return result;
        }
        internal int Physical(int amount)
        {
            var value=Current();int result=value.Physical(amount);Trace(value,"physical",amount,result,amount,result);return result;
        }
        internal int Spell(int amount)
        {
            var value=Current();int result=value.Spell(amount);Trace(value,"spell",amount,result,amount,result);return result;
        }
        private void Trace(Response value,string stage,float before,float after,int? beforeInt=null,int? afterInt=null)=>Log.LogInformation(
            "RETAIL_STAT_EQUIPMENT_APPLY npc={Npc} target={Target} stage={Stage} tag={Tag} matched={Matched} normalFlag={NormalFlag} normalFlat={NormalFlat} normalPercent={NormalPercent} skillFlag={SkillFlag} skillFlat={SkillFlat} skillPercent={SkillPercent} immune={Immune} beforeBits={BeforeBits} afterBits={AfterBits} beforeInt={BeforeInt} afterInt={AfterInt} context={Context}",
            npc,target.GetObjectId(),stage,value.Tag,value.Matched,value.NormalPercent,value.NormalFlat,value.NormalRate,
            value.SkillPercent,value.SkillFlat,value.SkillRate,value.Immune,BitConverter.SingleToUInt32Bits(before),BitConverter.SingleToUInt32Bits(after),beforeInt,afterInt,ReviewedContextSha256);
    }
    internal static bool TryPrepare(SkyTempleNpcProfile profile,Player target,out Handle handle,out string reason)
    {
        handle=null!;
        if(RetailNpcStatCatalog.EquipmentResponses is not { } context){reason="source-offensive-exclusive-attribute-unverified";return false;}
        var names=profile.Source.Source.GetRawRootField("exclusive_attribute").RawValues;
        if(names.Length!=1||!context.definitions.TryGetValue(names[0],out var definition)){reason="source-exclusive-npc-definition-unknown";return false;}
        if(!context.TryCurrent(target,definition.Tag,out var value,out reason))return false;
        handle=new(context,target,definition.Tag,profile.NpcId);
        Log.LogInformation("RETAIL_STAT_EQUIPMENT_RESPONSE npc={Npc} target={Target} empty={Empty} context={Context} equipped={Items}",
            profile.NpcId,target.GetObjectId(),!value.Matched,ReviewedContextSha256,string.Join(',',target.GetEquipment().GetEquippedItemIds().Order()));
        return true;
    }
    private bool TryCurrent(Player target,string tag,out Response value,out string reason)
    {
        var result=target.GetEquipment().InspectEquippedItems(items=>
        {
            var missing=Response.Missing(tag);
            foreach(var item in items)if(!known.Contains(item.GetItemId()))return(false,missing,"source-exclusive-unknown-equipped-item-"+item.GetItemId());
            if(!Actors.TryGetValue(target,out var state)||!ReferenceEquals(state.Context,this))
            {
                if(items.Any(x=>responders.Contains(x.GetItemId())))return(false,missing,"source-exclusive-missing-actor-history");
                return(true,missing,"source-equipment-response-empty");
            }
            lock(state)
            {
                if(state.Invalid is { } invalid)return(false,missing,invalid);
                var actual=items.Where(x=>responders.Contains(x.GetItemId())).ToDictionary(x=>x.GetObjectId(),x=>(x.GetItemId(),x.GetEquipmentSlot()));
                if(actual.Count!=state.Owners.Count||actual.Any(x=>!state.Owners.TryGetValue(x.Key,out var owner)||owner!=x.Value))
                    return(false,missing,"source-exclusive-current-owner-drift");
                return(true,state.Get(tag),"source-equipment-response-current");
            }
        });
        value=result.Item2;reason=result.Item3;
        return result.Item1;
    }
    internal Ledger NewLedger()=>new(this);
    internal Definition GetDefinition(string name)=>definitions[name];
}
