using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Aion.GameServer.Dataholders.RetailNpc;
using U = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive privately admitted ordered source/current-entry preparation.
/// Strict source token translation is not a native XML/default/locale emulator.</summary>
internal static class RetailNpcNormalAttackPreparation
{
    internal const string ParentProof = "3888a15db3fcc5baec8a81e3f5e50811f1c86cb87c130ebe09f39675fad1f348";
    internal const string TypeTableProof = "f6e03ff09dda1268930c0b2f0b72007d1ff05075efc29090aa4674d37fa7003e";
    internal const string NormalBodyProof = "a3054dffaf932520f4853a1785e4a765d65753af6c59e5c5e8d755aae2fc17bf";
    internal sealed record Context(U.Context Common, string ParentEvidence, string NormalEvidence,
        string TypeTableEvidence, string DatasetId, string ManifestSha256, string Region);
    internal sealed record CurrentEntry(ulong SourceActor, ulong SourceTemplate, string SourceLife,
        string TemplateLife, string VectorLife, string EntryLife);
    internal sealed record Admission(SelectedNormal? Value, Failure? Failure)
    { [MemberNotNullWhen(true, nameof(Value))] internal bool Success => Value is not null && Failure is null; }
    internal sealed class SelectedNormal
    {
        internal Context Context { get; }
        internal RetailNpcTemplateInputs Source { get; }
        internal CurrentEntry Current { get; }
        internal int Ordinal { get; }
        internal int Element { get; }
        internal int Modifier { get; }
        internal int Weight { get; }
        internal uint RangeBits { get; }
        internal ulong VectorAddress { get; }
        internal ulong EntryAddress { get; }
        internal int NpcId => Source.NpcId;
        internal RetailNpcTemplateSourceReceipt Receipt => Source.Receipt;
        private SelectedNormal(Context context, RetailNpcTemplateInputs source, CurrentEntry current,
            int ordinal, int element, int modifier, int weight, uint rangeBits, ulong vector, ulong entry)
        { Context=context; Source=source; Current=current; Ordinal=ordinal; Element=element; Modifier=modifier;
            Weight=weight; RangeBits=rangeBits; VectorAddress=vector; EntryAddress=entry; }
        internal static Admission Admit(Context context, RetailNpcTemplateInputs source, int ordinal,
            Memory memory, CurrentEntry current)
        {
            try
            {
                ValidateContext(context);
                Need(source is not null && current is not null && memory is not null, "input", "source", 0, "Missing projected source or current bindings.");
                var receipt=source.Receipt;
                Need(receipt.DatasetId==context.DatasetId && receipt.OutputManifestSha256==context.ManifestSha256 && receipt.Region==context.Region &&
                    receipt.MainSha256==RetailNpcTemplateProjection.MainBinarySha256 && receipt.NpcSha256==RetailNpcTemplateProjection.NpcBinarySha256 &&
                    source.Source.NpcId==source.NpcId && source.Source.DatasetId==context.DatasetId && source.Source.Devname==receipt.Devname,
                    "source", "dataset", 0, "Projected source/manifest/region/current cohort differs.");
                HashText(receipt.RawRecordSha256); HashText(receipt.SemanticRecordSha256); HashText(receipt.SourceFileSha256);
                Need(source.NpcId>0 && receipt.ByteStart>=0 && receipt.ByteEndExclusive>receipt.ByteStart && receipt.ByteEndExclusive<=receipt.SourceFileBytes &&
                    receipt.Line>0 && receipt.RecordIndex>=0 && !string.IsNullOrWhiteSpace(receipt.SourcePath),
                    "source", "record-span", 0, "Missing selected source span/identity.");
                Need(!source.Attacks.Entries.IsDefault && source.Attacks.Presence==RetailNpcSourcePresence.ExplicitValue &&
                    ordinal>=0 && ordinal<source.Attacks.Entries.Length && ordinal<=255, "source", "ordinal", 0, "Actual ordered normal index required.");
                for(int i=0;i<source.Attacks.Entries.Length;i++)Need(source.Attacks.Entries[i].Ordinal==i,"source","order",0,"Source entries reordered or duplicated.");
                var item=source.Attacks.Entries[ordinal];
                Need(item.Modifier.ParsedValue.HasValue && item.Weight.ParsedValue.HasValue && item.Range.ParsedValue.HasValue &&
                    item.Type.Value is not null && item.Type.Lexemes.Length==1 && item.Type.Lexemes[0]==item.Type.Value &&
                    item.Modifier.Lexemes.Length==1 && item.Weight.Lexemes.Length==1 && item.Range.Lexemes.Length==1,
                    "source", "explicit-entry", 0, "Every selected type/modifier/range/weight must be explicit; no native default inferred.");
                int element=Translate(item.Type.Value);
                uint bits=BitConverter.SingleToUInt32Bits(Normal(item.Range.ParsedValue.Value));
                Need(source.Integers.TryGetValue("hit_count",out var hits) && hits.ParsedValue==1,
                    "incomplete-child", "source.hit_count", 0, "This complete record domain requires explicit source hit_count1.",true);
                var c=new Cursor(context.Common,memory,10_000);
                ProtectSource(c,current);ValidateCore(c,current,source.NpcId);
                ulong vector=c.U64(current.SourceTemplate+0x70,0x140b1fa04,"NpcClass.normal_vector_begin70");
                ulong end=c.U64(current.SourceTemplate+0x78,0x140b1f9ba,"NpcClass.normal_vector_end78");
                Need(vector!=0 && end>=vector && (end-vector)%8==0 && (end-vector)/8<=65_536 && (ulong)ordinal<(end-vector)/8,
                    "binding", "current-vector", 0, "Invalid/unbounded actual Main list; adapter rejection is not a native return.");
                // The native combined list can include skills after normals; only the reached pointer is read.
                ulong cell=checked(vector+(ulong)ordinal*8);
                c.External(cell,8,"selected-normal-vector-cell");
                Need(c.BindingAt(cell,8).Generation==current.VectorLife,"generation","vector",0,"Current vector lifetime differs.");
                ulong entry=c.U64(cell,0x140b1fa13,"current.selected_normal_pointer"); c.External(entry,0x30,"selected-normal-entry");
                Need(c.BindingAt(entry,0x30).Generation==current.EntryLife,"generation","entry",0,"Current selected entry lifetime differs.");
                var selected=new SelectedNormal(context,source,current,ordinal,element,item.Modifier.ParsedValue.Value,
                    item.Weight.ParsedValue.Value,bits,vector,entry);
                selected.ValidateCurrent(c); return new(selected,null);
            }
            catch(Domain e){return new(null,e.Failure);}
            catch(ArithmeticException e){return new(null,new("arithmetic","admission",0,e.Message,true));}
        }
        internal void ValidateCurrent(Cursor c)
        {
            ValidateCore(c,Current,NpcId);
            ulong vector=c.U64(Current.SourceTemplate+0x70,0x140b1fa04,"NpcClass.normal_vector_begin70");
            ulong end=c.U64(Current.SourceTemplate+0x78,0x140b1f9ba,"NpcClass.normal_vector_end78");
            Need(vector==VectorAddress && end>=vector && (end-vector)%8==0 && (end-vector)/8<=65_536 && (ulong)Ordinal<(end-vector)/8,
                "stale-entry","current-vector",0,"Selected ordered Main vector changed.");
            ulong cell=checked(vector+(ulong)Ordinal*8); c.External(cell,8,"selected-normal-vector-cell");
            Need(c.BindingAt(cell,8).Generation==Current.VectorLife,"generation","vector",0,"Current vector lifetime differs.");
            Need(c.U64(cell,0x140b1fa13,"current.selected_normal_pointer")==EntryAddress,"stale-entry","pointer",0,"Selected current pointer changed.");
            c.External(EntryAddress,0x30,"selected-normal-entry");
            Need(c.BindingAt(EntryAddress,0x30).Generation==Current.EntryLife,"generation","entry",0,"Current entry lifetime differs.");
            Need(c.U64(EntryAddress+0x28,0x140b1faaa,"normal_entry.Skill28")==0,"incomplete-child","selected.Skill",0,"Selected entry is skill-backed, not a normal attack.",true);
            // Stronger adapter source admission, distinct from the native per-field consume sites.
            Need(c.I32(EntryAddress,0,"source-admission.weight")==Weight && c.U32(EntryAddress+4,0,"source-admission.range_bits")==RangeBits &&
                c.I32(EntryAddress+0x10,0x140b1fac6,"current_entry.type10")==Element &&
                c.I32(EntryAddress+0x14,0x14035a3c0,"current_entry.modifier14")==Modifier,
                "stale-entry","source-join",0,"Current selected fields differ from the privately admitted ordered source entry.");
        }
    }
    internal sealed record Prepared(int Element,int Modifier,uint RangeBits,Memory CalculatedMemory,ImmutableArray<Step> Steps)
    { internal bool CurrentActorObserved=>false; internal bool HostEffectsExecuted=>false; internal bool ActivationReady=>false; }
    internal sealed record PreparationOutcome(Prepared? Value,Failure? Failure)
    { [MemberNotNullWhen(true,nameof(Value))] internal bool Success=>Value is not null&&Failure is null; }
    internal static PreparationOutcome Prepare(SelectedNormal selected,Memory memory,int maxSteps=10_000)
    {
        ArgumentNullException.ThrowIfNull(selected);
        try {var c=new Cursor(selected.Context.Common,memory,maxSteps);ProtectSource(c,selected.Current);
            Apply(c,selected);return new(new(selected.Element,selected.Modifier,c.U32(selected.Current.SourceActor+0x910,0,"calculated.range910"),c.Snapshot(),c.Steps),null);}
        catch(Domain e){return new(null,e.Failure);}catch(ArithmeticException e){return new(null,new("arithmetic","preparation",0,e.Message,true));}
    }
    internal static void Apply(Cursor c,SelectedNormal selected)
    {
        selected.ValidateCurrent(c);ulong a=selected.Current.SourceActor,e=selected.EntryAddress;
        for(int hand=0;hand<2;hand++)
        {
            int element=c.I32(e+0x10,hand==0?0x140b1fac6ul:0x140b1fad4ul,"entry.element for SetType");
            c.W32(a+0x8f4+(ulong)hand*4,unchecked((uint)element),0x140b14358,"Npc.element.hand"+hand);
            c.W32(a+0x8e4+(ulong)hand*4,element==0?1u:0u,0x140b14364,"Npc.physical.hand"+hand);
        }
        c.W32(a+0x14d0,0,0x14035a3b9,"Npc.clear_both_flat_words14d0");
        float modifier=Mul(FromInt(c.I32(e+0x14,0x14035a3c0,"entry.modifier14.first")),BitConverter.UInt32BitsToSingle(0x3c23d70a));
        c.W32(a+0x8d4,BitConverter.SingleToUInt32Bits(modifier),0x14035a3cc,"Npc.modifier_rate8d4");
        modifier=Mul(FromInt(c.I32(e+0x14,0x14035a3d4,"entry.modifier14.second")),BitConverter.UInt32BitsToSingle(0x3c23d70a));
        c.W32(a+0x8d8,BitConverter.SingleToUInt32Bits(modifier),0x14035a3e0,"Npc.modifier_rate8d8");
        bool physical=c.I32(a+0x8e4,0x14035a3e8,"Npc.physical8e4")!=0;
        for(int i=0;i<2;i++)
        {
            ulong read=physical?(i==0?0x14035a3f1ul:0x14035a3fful):(i==0?0x14035a42ful:0x14035a43dul);
            ulong write=physical?(i==0?0x14035a3f8ul:0x14035a406ul):(i==0?0x14035a436ul:0x14035a444ul);
            ushort v=c.U16(a+(physical?0x14bcul:0x14c0ul)+(ulong)i*2,read,"Npc.selected_critical_pair");
            byte[] bytes=new byte[2];BinaryPrimitives.WriteUInt16LittleEndian(bytes,v);
            c.Write(a+0x14b8+(ulong)i*2,bytes,write,"Npc.current_critical_pair");
        }
        ulong row=a+0x920+(physical?0x19ul:0x1bul)*8;
        float rate=Div(FromInt(c.I32(row,0x14035a452,"Npc.modifier_row.rate")),100f);
        c.W32(a+0x8e0,BitConverter.SingleToUInt32Bits(rate),0x14035a466,"Npc.additive_rate8e0");
        c.W32(a+0x8dc,BitConverter.SingleToUInt32Bits(rate),0x14035a46e,"Npc.additive_rate8dc");
        uint flat=c.U32(row+4,0x14035a476,"Npc.modifier_row.flat");
        c.W32(a+0x14d8,flat,0x14035a47d,"Npc.flat14d8");c.W32(a+0x14d4,flat,0x14035a483,"Npc.flat14d4");
        uint kind=c.U32(a+0x1940,0x14035a489,"Npc.kind1940");
        Need((kind is 1 or 8) && c.U64(a+0x1850,0x14035a536,"Npc.virtual1850")==0,
            "incomplete-child","preparation.range",0,"Active virtual/scaled range child has no supplied result.",true);
        float range=c.F32(e+4,0x14035a561,"entry.range4");
        c.W32(a+0x910,BitConverter.SingleToUInt32Bits(range),0x14035a566,"Npc.range_before_modifiers");
        range=Add(Add(Mul(Mul(FromInt(c.I32(a+0xa50,0x14035a56e,"Npc.range_percentA50")),range),BitConverter.UInt32BitsToSingle(0x3c23d70a)),
            FromInt(c.I32(a+0xa54,0x14035a576,"Npc.range_flatA54"))),range);
        c.W32(a+0x910,BitConverter.SingleToUInt32Bits(range),0x14035a59a,"Npc.range_modified");
        if(range<0f)c.W32(a+0x910,0,0x14035a5a4,"Npc.range_lower_clamp");
        else if(range>255f)c.W32(a+0x910,0x437f0000,0x14035a5b6,"Npc.range_upper_clamp");
    }
    internal static PreparationOutcome InitializeConstructedFloorTables(Context context,Memory memory,int maxSteps=1000)
    {
        try
        {
            ValidateContext(context);Need(context.Common.Origin==Origin.ConstructedFixture,"source","floor.initialization",0,"This constructs startup tables; it cannot assert current BSS initialization.");
            var c=new Cursor(context.Common,memory,maxSteps);c.External(0x145a28930,404,"constructed-floor-table");c.External(0x145a28ad0,404,"constructed-companion-table");
            for(int level=0;level<=100;level++)
            {
                c.W32(0x145a28930ul+(ulong)level*4,unchecked((uint)(level<=50?-1000:-500-level*10)),level<=50?0x140ddbe99ul:0x140ddbeb3ul,"constructed.floor"+level);
                c.W32(0x145a28ad0ul+(ulong)level*4,(uint)(level<=50?1000:500+level*10),level<=50?0x140ddbea5ul:0x140ddbebbul,"constructed.companion"+level);
            }
            return new(new(0,0,0,c.Snapshot(),c.Steps),null);
        }
        catch(Domain e){return new(null,e.Failure);}
    }
    internal static void ValidateContext(Context context)
    {
        Need(context is not null,"input","context",0,"Required normal source context missing.");CheckContext(context.Common);
        Need(context.ParentEvidence==ParentProof && context.NormalEvidence==NormalBodyProof && context.TypeTableEvidence==TypeTableProof,
            "source","normal-proof",0,"Exact Main normal/preparation/type-table source cohort required.");
        HashText(context.DatasetId);HashText(context.ManifestSha256);Text(context.Region);
    }
    internal static void ProtectSource(Cursor c,CurrentEntry current)
    {
        Disjoint(current.SourceActor,0x2000,current.SourceTemplate,0x500);
        c.Protect(current.SourceActor,0x2000);c.Protect(current.SourceTemplate,0x500);
    }
    private static void ValidateCore(Cursor c,CurrentEntry current,int id)
    {
        Text(current.SourceLife);Text(current.TemplateLife);Text(current.VectorLife);Text(current.EntryLife);
        Need(c.BindingAt(current.SourceActor,0x2000).Generation==current.SourceLife && c.BindingAt(current.SourceTemplate,0x500).Generation==current.TemplateLife,
            "generation","source/template",0,"Current source/template storage generation differs.");
        Need(c.U64(current.SourceActor,0,"Npc.vtable")==NpcVtable && c.I32(current.SourceActor+0x144,0,"Npc.type144")==2 &&
            c.I32(current.SourceActor+0x100,0,"Npc.id100")==id && c.U64(current.SourceActor+0x1848,0,"Npc.template1848")==current.SourceTemplate,
            "binding","source",0,"Actual source NPC/current template identity required.");
    }
    internal static void Disjoint(ulong a,int al,ulong b,int bl)=>Need(a!=0&&b!=0&&a<=ulong.MaxValue-(ulong)al&&b<=ulong.MaxValue-(ulong)bl&&
        (a+(ulong)al<=b||b+(ulong)bl<=a),"alias","owner-ranges",0,"Distinct owned ranges overlap or wrap.");
    private static void HashText(string? text)=>Need(text is not null && text.Length==64 && text.All(x=>x is >= '0' and <= '9' or >= 'a' and <= 'f'),"source","hash",0,"Explicit lowercase SHA256 required.");
    private static int Translate(string value)=>value.ToLowerInvariant() switch
    {"physical"=>0,"magical_water"=>1,"magical_air"=>2,"magical_earth"=>3,"magical_fire"=>4,"magical_light"=>5,"magical_dark"=>6,
        _=>throw Error("source","normal.type",0,"Unsupported strict source type token; no enum default.")};
}
