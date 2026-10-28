using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserStatSourceCatalog;
using O = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipObservation;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Inactive selected4.8 presentation source and original managed-field snapshots.
/// Fixture/declaration admission is not a live actor, loaded registry, or host capture.</summary>
internal static class RetailNpc48PresentationInputs
{
    internal const string Policy = "NpcRetailComputedSpawnWithRetained48PresentationV1";
    internal const string BundleSha256 = "1658969d79d228bf6a1e95d358d304b9813ba0cbfc793c366477d30e2a440549";
    internal const string CurrentProgramsSha256 = "e06f04bdbc890bc0ec030909034053c4cfc40177516a2ad247d582e5b8a61aa9";
    internal const int MapId = 320050000;
    internal enum Origin { Unknown, ConstructedSourceFixture, DeclaredManagedSnapshot }
    internal enum Reference { Unknown, Null, Present }
    internal enum EquipmentKind { Unknown, NoOverride, OriginalOrderedOverride }
    internal enum SpawnKind { Unknown, Absent, OrdinarySpawnTemplate }
    internal enum ZoneProducer { Unknown, XmlZoneTemplate, MaterialZoneTemplate }
    internal sealed record SourceFile(string Name, ImmutableArray<byte> Bytes);
    internal sealed record Evidence(Origin Origin, string Policy, string RetailPackageContext, string ProgramsSha256,
        string Operation, string Epoch, string Receipt);
    internal sealed record Binding(uint ObjectId, string Lifetime, string World, string WorldGeneration,
        int MapId, int InstanceId, string SourceGeneration);
    internal sealed record FieldString(bool Known, string? Units);
    internal sealed record OptionalType(Reference Reference, string? OriginalEnumToken);
    internal sealed record Equipment(EquipmentKind Kind, string Owner, string Generation,
        string ConstructorProgram, ImmutableArray<C.Catalog.Record> OriginalOrderedTemplates);
    internal sealed record Spawn(SpawnKind Kind, string Owner, string Generation, string RuntimeType,
        int? OriginalStaticIdField);
    internal sealed record Npc(Binding Binding, string? RuntimeType, int? OriginalTemplateId,
        int? OriginalStateField, int? OriginalVisualStateField, int? OriginalCreatorIdField,
        FieldString? OriginalMasterNameField, Reference OriginalTransformModel, OptionalType? OriginalOverriddenType,
        long? OriginalLastSpawnMillis, bool? OriginalPositionSpawnedField, Equipment? OriginalOverrideEquipment, Spawn? OriginalSpawn);
    internal sealed record Player(Binding Binding, string? RuntimeType, string? OriginalRaceToken,
        int? OriginalCustomStateField, Reference OriginalTransformModel, Reference OriginalPanesterraFaction);
    internal sealed record ObjectBinding(uint ObjectId, string Lifetime, string RuntimeType, string World,
        string WorldGeneration, int MapId, int InstanceId, bool? Disposed);
    internal sealed record ZoneRegistration(string Owner, string Generation, ZoneProducer Producer,
        string? RuntimeTemplateType, string? OriginalName, int? OriginalMapId,
        string? ConstructorSourceGeneration, string? CurrentTemplateGeneration, int? OriginalTownIdField,
        string? MaterialGeometryOwner, string? MaterialGeometryGeneration);
    internal sealed record World(string Identity, string Generation, int MapId, int InstanceId,
        string RegistryProgram, string RegistryGeneration, string SourceGeneration,
        ImmutableArray<ZoneRegistration> OriginalCompleteRegistrations);
    internal sealed record Clock(uint RecipientId, string RecipientLifetime, string ConnectionGeneration,
        uint ObjectId, string ObjectLifetime, long UnixMillis, string Operation, string Epoch, string Receipt);
    internal sealed record Raw(Evidence Evidence, Npc Npc, ImmutableArray<Player> Players,
        ImmutableArray<ObjectBinding> OriginalTargetObjects, World World, ImmutableArray<Clock> OriginalBirthClocks);
    internal sealed record Failure(string Code, string Detail);
    internal sealed class Fault(string code,string detail):Exception(detail)
    { internal string Code { get; }=code; }
    internal static void Need([DoesNotReturnIf(false)] bool condition,string code,string detail)
    { if(!condition)throw new Fault(code,detail); }
    internal static void Text(string? value,string field)=>Need(!string.IsNullOrWhiteSpace(value)&&value.Length<=4096,"identity",field);
    internal static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static bool Digest(string? value)=>value is {Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static readonly UTF8Encoding Utf8 = new(false,true);
    private static readonly uint[] Ids = [212761,212762,212763,212764,212765,212766,212767,212768,
        212769,212770,212771,212772,212773,212774,212775,212776,212777,212778,212779,212780,
        212781,212782,212833,212835,212876];

    internal sealed record NpcContent(uint Id,string Name,uint NameId,uint TitleId,string Tribe,
        string? CurrentAi,string? JavaAi,string CurrentRawSha256,string JavaRawSha256);
    private sealed record Tribe(string Name,string Base,ImmutableDictionary<string,ImmutableHashSet<string>> Lists);
    private sealed record SourceDraft(ImmutableDictionary<uint,NpcContent> Npcs,
        ImmutableDictionary<string,Tribe> Tribes,ImmutableHashSet<string> ZoneNames,
        ImmutableArray<string> AiDifferences,string SnapshotSha256);

    internal sealed class Sources
    {
        private readonly SourceDraft value;
        private Sources(SourceDraft value)=>this.value=value;
        internal string SnapshotSha256=>value.SnapshotSha256;
        internal string CurrentPrograms=>CurrentProgramsSha256;
        internal ImmutableArray<string> AiDifferences=>value.AiDifferences;
        internal ImmutableArray<uint> SelectedNpcIds=>[..value.Npcs.Keys.Order()];
        internal ImmutableHashSet<string> CurrentXmlZoneNames=>value.ZoneNames;
        internal bool LoadedRegistryObserved=>false;
        internal bool ActivationReady=>false;
        internal static Sources Admit(ImmutableArray<SourceFile> originalFiles)=>new(ReadSources(originalFiles));
        internal NpcContent Npc(uint id)
        {Need(value.Npcs.TryGetValue(id,out var npc),"source","Outside the supplied25 ordinary mob declarations.");return npc;}
        private Tribe Get(string name)
        {Need(value.Tribes.TryGetValue(name,out var tribe),"source","Unknown consumed tribe token.");return tribe;}
        internal string Base(string name)=>Get(name).Base;
        internal bool Relation(string list,string first,string second)
        {
            Tribe a=Get(first),b=Get(second);
            var aa=a.Lists[list];var bb=b.Lists[list];
            return aa.Contains(b.Base)||aa.Contains(b.Name)||bb.Contains(a.Base)||bb.Contains(a.Name);
        }
    }

    /// <summary>Private whole original-field ownership. None of its fields is a desired packet,
    /// TownService answer, relationship result or future actor state.</summary>
    internal sealed class Capability
    {
        private Capability(Sources source,Raw raw,string hash){Source=source;Original=raw;SnapshotSha256=hash;}
        internal Sources Source { get; }
        internal Raw Original { get; }
        internal string SnapshotSha256 { get; }
        internal bool CurrentHostObserved=>false;
        internal bool ActivationReady=>false;
        internal static Capability Admit(Sources source,Raw original)
        {
            Need(source is not null&&original is not null&&original.Evidence is not null&&original.Npc is not null
                &&original.Npc.Binding is not null&&original.World is not null,"input","Missing source/current presentation roots.");
            Evidence e=original.Evidence;
            Need(e.Origin is Origin.ConstructedSourceFixture or Origin.DeclaredManagedSnapshot&&e.Policy==Policy
                &&e.RetailPackageContext==O.PackageContext&&e.ProgramsSha256==CurrentProgramsSha256,
                "context","Exact presentation policy, source programs, explicit origin and frozen unloadedUSA context required.");
            Text(e.Operation,"operation");Text(e.Epoch,"epoch");Text(e.Receipt,"source/current receipt");
            Need(!original.Players.IsDefault&&!original.OriginalTargetObjects.IsDefault&&!original.OriginalBirthClocks.IsDefault,
                "unknown","Unknown original collections are not empty.");
            Need(original.Players.Length<=65536&&original.OriginalTargetObjects.Length<=65536&&original.OriginalBirthClocks.Length<=65536,
                "budget","Original presentation root count exceeds the finite bound.");
            Need((original.World.OriginalCompleteRegistrations.IsDefault||original.World.OriginalCompleteRegistrations.Length<=65536)
                &&(original.Npc.OriginalMasterNameField?.Units?.Length??0)<=65536
                &&(original.Npc.OriginalOverrideEquipment is not { } boundedGear||boundedGear.OriginalOrderedTemplates.IsDefault||boundedGear.OriginalOrderedTemplates.Length<=1024),
                "budget","Original registry/string/equipment construction bound.");
            Need(original.Players.All(x=>x is not null&&x.Binding is not null)&&original.OriginalTargetObjects.All(x=>x is not null),
                "input","Null player/target root.");
            Need(original.Players.Select(x=>x.Binding.ObjectId).Distinct().Count()==original.Players.Length
                &&original.OriginalTargetObjects.Select(x=>x.ObjectId).Distinct().Count()==original.OriginalTargetObjects.Length,
                "alias","Duplicate physical player or target identity.");
            // Records and strings are immutable. Copy each collection root; no supplied array is retained as mutable state.
            Npc npc=original.Npc;
            if(npc.OriginalOverrideEquipment is { } gear)npc=npc with {OriginalOverrideEquipment=gear with {
                OriginalOrderedTemplates=gear.OriginalOrderedTemplates.IsDefault?default:[..gear.OriginalOrderedTemplates]}};
            Raw copy=original with {Npc=npc,Players=[..original.Players],OriginalTargetObjects=[..original.OriginalTargetObjects],
                OriginalBirthClocks=[..original.OriginalBirthClocks],World=original.World with {
                    OriginalCompleteRegistrations=original.World.OriginalCompleteRegistrations.IsDefault?default:[..original.World.OriginalCompleteRegistrations]}};
            return new(source,copy,SnapshotHash(source,copy));
        }
    }

    private static string SnapshotHash(Sources source,Raw raw)
    {
        Npc n=raw.Npc;World w=raw.World;Equipment? gear=n.OriginalOverrideEquipment;
        object? items=gear is null||gear.OriginalOrderedTemplates.IsDefault?null:gear.OriginalOrderedTemplates.Select(r=>r is null?null:new object[] {
            r.Identity,r.Owner.DatasetId,r.Owner.ManifestSha256,r.Owner.SourceOrigin,r.Metadata,Hash(r.Raw.AsSpan())}).ToArray();
        return RetailMainApplyEvidence.HashObject(HashUnits(new object?[] {source.SnapshotSha256,raw.Evidence,
            new object?[]{n.Binding,n.RuntimeType,n.OriginalTemplateId,n.OriginalStateField,n.OriginalVisualStateField,n.OriginalCreatorIdField,
                n.OriginalMasterNameField,n.OriginalTransformModel,n.OriginalOverriddenType,n.OriginalLastSpawnMillis,n.OriginalPositionSpawnedField,
                gear is null?null:new object?[]{gear.Kind,gear.Owner,gear.Generation,gear.ConstructorProgram,
                    gear.OriginalOrderedTemplates.IsDefault,items},n.OriginalSpawn},raw.Players,raw.OriginalTargetObjects,
            new object?[]{w.Identity,w.Generation,w.MapId,w.InstanceId,w.RegistryProgram,w.RegistryGeneration,w.SourceGeneration,
                w.OriginalCompleteRegistrations.IsDefault,w.OriginalCompleteRegistrations.IsDefault?null:w.OriginalCompleteRegistrations.ToArray()},
            raw.OriginalBirthClocks})!);
    }

    // JSON's string encoder substitutes invalid surrogate units. Presentation WriteS
    // deliberately preserves them, so hash every original string as its exact UTF16 units.
    private static object? HashUnits(object? value)
    {
        if(value is null)return null;
        if(value is string text)return new object[]{"utf16",text.Select(c=>(int)c).ToArray()};
        Type type=value.GetType();
        if(type.IsPrimitive||value is decimal)return value;
        if(type.IsEnum)return new object[]{type.FullName!,Convert.ToInt64(value,CultureInfo.InvariantCulture)};
        if(value is IEnumerable list)return list.Cast<object?>().Select(HashUnits).ToArray();
        return type.GetProperties(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public)
            .OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new object?[]{p.Name,HashUnits(p.GetValue(value))}).ToArray();
    }

    private static SourceDraft ReadSources(ImmutableArray<SourceFile> original)
    {
        Need(!original.IsDefault&&original.Length==256,"source","Complete pinned selected presentation source bundle required.");
        Need(original.All(x=>x is not null&&!string.IsNullOrWhiteSpace(x.Name)&&x.Name.Length<=512&&x.Name.All(c=>c is >= ' ' and <= '~')&&!x.Bytes.IsDefault&&x.Bytes.Length>0
            &&x.Bytes.Length<=16*1024*1024),"source","Malformed source span.");
        Need(original.Sum(x=>(long)x.Bytes.Length)<=32*1024*1024,"budget","Source byte bound.");
        Need(original.Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()==original.Length,"source","Duplicate source key.");
        var owned=original.Select(x=>new SourceFile(x.Name,[..x.Bytes.AsSpan().ToArray()])).OrderBy(x=>x.Name,StringComparer.Ordinal).ToArray();
        string digest=Bundle(owned);
        Need(digest==BundleSha256&&Bundle(owned.Where(x=>x.Name.StartsWith("program/current/",StringComparison.Ordinal)))==CurrentProgramsSha256,
            "source","Complete original source bytes differ from the pinned retained4.8 bundle.");
        var files=owned.ToDictionary(x=>x.Name,x=>x.Bytes,StringComparer.Ordinal);
        XElement Xml(string name)=>Parse(files[name].AsSpan());
        var npcs=ImmutableDictionary.CreateBuilder<uint,NpcContent>();var differences=ImmutableArray.CreateBuilder<string>();
        foreach(uint id in Ids)
        {
            string key=id.ToString(CultureInfo.InvariantCulture);XElement a=Xml("npc/current/"+key+".xml"),b=Xml("npc/java/"+key+".xml");
            Need(a.Name.LocalName=="npc_template"&&b.Name.LocalName=="npc_template"&&Int(a,"npc_id")==id&&Int(b,"npc_id")==id,
                "source","Actual selected NPC record identity.");
            string? aa=(string?)a.Attribute("ai"),bb=(string?)b.Attribute("ai");
            XElement ac=new(a),bc=new(b);ac.Attribute("ai")?.Remove();bc.Attribute("ai")?.Remove();
            Need(Canonical(ac)==Canonical(bc),"source","Unreviewed Java/current NPC presentation difference.");
            if(aa!=bb)differences.Add(key+":current="+aa+";Java="+bb);
            Need(a.Element("equipment") is null&&a.Attribute("type") is null&&a.Attribute("title_id") is null,
                "source","This finite roster has proved absent equipment/type/title declarations.");
            string tribe=(string?)a.Attribute("tribe")??throw new Fault("source","Missing actual tribe source.");
            npcs.Add(id,new(id,(string?)a.Attribute("name")??throw new Fault("source","Missing name source."),
                unchecked((uint)Int(a,"name_id")),0,tribe,aa,bb,Hash(files["npc/current/"+key+".xml"].AsSpan()),Hash(files["npc/java/"+key+".xml"].AsSpan())));
        }
        Need(npcs.Count==25&&differences.Count==21,"source","Exact25 source records and21 existing AI-only differences required.");
        var tribes=ImmutableDictionary.CreateBuilder<string,Tribe>(StringComparer.Ordinal);
        foreach(XElement t in Xml("data/tribe/tribe_relations.xml").Elements("tribe"))
        {
            string name=(string?)t.Attribute("name")??throw new Fault("source","Unnamed tribe.");
            string baseName=(string?)t.Attribute("base")??"NONE";if(baseName=="NONE")baseName=name;
            var lists=ImmutableDictionary.CreateBuilder<string,ImmutableHashSet<string>>(StringComparer.Ordinal);
            foreach(string list in new[]{"aggro","hostile","neutral","friend","support","none"})
                lists[list]=((string?)t.Element(list)??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).ToImmutableHashSet(StringComparer.Ordinal);
            tribes[name]=new(name,baseName,lists.ToImmutable()); // Java map replaces later definitions.
        }
        foreach(NpcContent n in npcs.Values)Need(tribes.TryGetValue(n.Tribe,out var t)&&t.Base=="MONSTER","source","Finite MONSTER-base source family.");
        Need(tribes.TryGetValue("PC",out var pc)&&pc.Base=="PC"&&tribes.TryGetValue("PC_DARK",out var dark)&&dark.Base=="PC_DARK",
            "source","Actual ordinary player tribe bases.");
        var zoneNames=ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);int zoneFiles=0,townFiles=0;
        foreach(var f in owned)
        {
            if(f.Name.StartsWith("data/zones/",StringComparison.Ordinal))
            {
                zoneFiles++;foreach(XElement z in Parse(f.Bytes.AsSpan()).Elements("zone"))if(Int(z,"mapid")==MapId)
                {Need(Int(z,"town_id",0)==0,"source","Selected map has a town source zone.");Need(zoneNames.Add((string?)z.Attribute("name")??""),"source","Duplicate zone source name.");}
            }
            if(f.Name.StartsWith("data/town_spawns/",StringComparison.Ordinal))
            {townFiles++;Need(!Parse(f.Bytes.AsSpan()).Elements("spawn_map").Any(x=>Int(x,"map_id")==MapId),"source","Selected map has town spawn sources.");}
        }
        Need(zoneFiles==149&&townFiles==2&&zoneNames.Count==6,"source","Complete imported zone/town inventory required.");
        XElement imports=Xml("data/static_data.xml");
        foreach(string name in new[]{"zones","town_spawns","tribe/tribe_relations.xml"})
            Need(imports.Elements("import").Count(x=>(string?)x.Attribute("file")==name)==1,"source","Actual source import binding.");
        return new(npcs.ToImmutable(),tribes.ToImmutable(),zoneNames.ToImmutable(),differences.ToImmutable(),digest);
    }
    private static string Bundle(IEnumerable<SourceFile> files)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> word=stackalloc byte[4];
        foreach(var file in files.OrderBy(x=>x.Name,StringComparer.Ordinal))
        {
            byte[] name=Utf8.GetBytes(file.Name);BinaryPrimitives.WriteInt32LittleEndian(word,name.Length);hash.AppendData(word);hash.AppendData(name);
            BinaryPrimitives.WriteInt32LittleEndian(word,file.Bytes.Length);hash.AppendData(word);hash.AppendData(SHA256.HashData(file.Bytes.AsSpan()));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private static XElement Parse(ReadOnlySpan<byte> bytes)
    {
        try
        {
            using var input=new MemoryStream(bytes.ToArray(),false);
            using var reader=XmlReader.Create(input,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024});
            return XElement.Load(reader,LoadOptions.None);
        }
        catch(Exception error) when(error is XmlException or DecoderFallbackException or InvalidOperationException)
        {throw new Fault("source","Malformed pinned source: "+error.GetType().Name);}
    }
    private static int Int(XElement node,string attr,int? absent=null)
    {
        string? value=(string?)node.Attribute(attr);
        if(value is null){Need(absent.HasValue,"source","Missing source attribute "+attr);return absent.GetValueOrDefault();}
        Need(int.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int result),"source","Bad integer source "+attr);return result;
    }
    private static string Canonical(XElement e)
    {
        string Atom(string value)=>value.Length.ToString(CultureInfo.InvariantCulture)+":"+value;
        return Atom(e.Name.ToString())+string.Concat(e.Attributes().OrderBy(x=>x.Name.ToString(),StringComparer.Ordinal).Select(x=>Atom(x.Name.ToString())+Atom(x.Value)))
            +"["+Atom(string.Concat(e.Nodes().OfType<XText>().Select(x=>x.Value)).Trim())+string.Concat(e.Elements().Select(Canonical))+"]";
    }
}
