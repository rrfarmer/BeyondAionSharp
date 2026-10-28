using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Inactive source construction for the two complete pilot SkillATK_Instant records.
/// The 17-body PE-equal recipe and original name pointers are pinned by the external
/// physical parameter-construction evidence. This does not observe loaded/current Skill
/// objects, RunInfo, race globals, actors, RNG, damage, delayed application or HP.
/// </summary>
internal static class RetailNpcPhysicalInstantParameters
{
    internal const string MainSha256 = "da1ee5633d65a7ebb436b29d15ca24e46ab4f267449384d177bf20d2d26eb8f1";
    internal const string PackageContext = "frozen-USA-base-physical2-pilot25;loaded-context-unavailable";
    internal const string SourceEvidenceSha256 = "41fe10eb19d400c9856835c0b45fc04c42749471aebbcd6b555a903d647e191e";
    internal const string CallerReviewSha256 = "c586ae4e92dbd6eb8ae70869ee8702493b5b32cec7cb4243154449b09fe752db";
    internal sealed record Pins(string MainSha256, string PackageContext, string SourceEvidenceSha256, string CallerReviewSha256);
    internal static Pins RequiredPins => new(MainSha256, PackageContext, SourceEvidenceSha256, CallerReviewSha256);
    internal sealed record NpcSkillBinding(int NpcId, string NpcDevname, int ListOrdinal, int SkillLevel, string SkillName);
    internal sealed record BindingField(string Name, string Text);
    internal sealed record SourceField(string Name, string Text, string NormalizedXml);
    internal sealed record Pair(int RawFirst, int RawSecond, int First, int Second, bool DefaultApplied, string Recipe);
    internal enum PairKind { Hit, Flat, Additional, Chance }
    private sealed record SourceIdentity(int Id, string Name, string Sha256, int ListOrdinal);
    private static readonly ImmutableArray<SourceIdentity> Sources =
    [
        new(16556,"NAS_MoveBehind_Nr","2b783edc57b0e5f3b2da0475ef2bc793a97d7f7dec501a0b34da316e5e8e999b",2),
        new(16794,"NAS_Spin_Front","df401cd076191fc7ef7209556aa62333c876e86bfd2b263e87855d2deb0a0ec6",0)
    ];

    internal sealed class SelectedPhysical
    {
        internal int Id { get; }
        internal string Name { get; }
        internal string RawSha256 { get; }
        internal NpcSkillBinding Binding { get; }
        internal short SignedSourceLevel => checked((short)Binding.SkillLevel);
        internal int EffectTypeIndex => 0; // Actual class index, never an absent-effect sentinel.
        internal ImmutableArray<byte> RawBytes { get; }
        internal ImmutableArray<SourceField> OrderedFields { get; }
        internal ImmutableArray<BindingField> OrderedBindingFields { get; }
        internal ImmutableArray<Pair> PairRecipes { get; }
        internal ImmutableDictionary<string,long> Constructed { get; }
        internal int NoResist438 => 0;
        internal int Accuracy43c => 0;
        internal int Accuracy440 => 0;
        internal uint CriticalProbability44cBits => 0;
        internal uint CriticalProbability450Bits => 0x3f800000;
        internal int HitRate458 => PairRecipes[0].First;
        internal int HitRate45c => PairRecipes[0].Second;
        internal int Flat460 => PairRecipes[1].First;
        internal int Flat464 => PairRecipes[1].Second;
        internal int ForceThreshold468 { get; }
        internal int RandomType46c { get; }
        internal int RandomMask470 { get; }
        internal int Additional474 => PairRecipes[2].First;
        internal int Additional478 => PairRecipes[2].Second;
        internal byte OpaqueReserved7At47c { get; }
        internal ImmutableArray<byte> Condition480 { get; }
        internal int AdditionalEffect4b0 { get; }
        internal int AdditionalEffect4b4 { get; }
        internal int Chance4b8 => PairRecipes[3].First;
        internal int Chance4bc => PairRecipes[3].Second;
        internal bool IsCurrentActorObservation => false;
        internal bool ActivationReady => false;

        private SelectedPhysical(int id, string name, string sha, NpcSkillBinding binding,
            byte[] raw, ImmutableArray<SourceField> fields)
        {
            Id=id; Name=name; RawSha256=sha; Binding=binding;
            RawBytes=raw.ToImmutableArray(); OrderedFields=fields;
            OrderedBindingFields=[new("skill_name",name),new("skill_level",binding.SkillLevel.ToString(CultureInfo.InvariantCulture)),
                new("skill_rate","0"),new("skill_count","1")];
            // Absence here is the proved fresh Skill constructor zero, followed by the
            // selected Parse/Check helpers. It is not a dataset-wide missing-field policy.
            int N(int ordinal) => SourceInteger(fields,"effect1_reserved"+ordinal,0);
            PairRecipes=[ConstructPair(N(1),N(2),PairKind.Hit),ConstructPair(N(3),N(4),PairKind.Flat),
                ConstructPair(N(9),N(10),PairKind.Additional),ConstructPair(N(17),N(18),PairKind.Chance)];
            ForceThreshold468=N(11); RandomType46c=N(8);
            Need(RandomType46c is >=0 and <=10,"physical_random_type","/effect1_reserved8","Selected parser/Check random range differs");
            OpaqueReserved7At47c=unchecked((byte)N(7));
            Need(One(fields,"effect1_reserved12")=="BasicDMG","physical_mask","/effect1_reserved12","Pinned BasicDMG parser requires mask1");
            RandomMask470=1;
            byte direction=One(fields,"effect1_reserved16") switch
            { "_Back"=>1,"_Front"=>2,_=>throw new ContractError("physical_condition","/effect1_reserved16","Outside the pinned direction recipes") };
            byte[] condition=new byte[48];
            // bb4160 writes both 24-byte halves. The mutable global race mask is separate.
            condition[0x14]=condition[0x2c]=direction; condition[0x15]=condition[0x2d]=1;
            Condition480=condition.ToImmutableArray();
            var addType=fields.Where(x=>x.Name=="effect1_reserved14").ToArray();
            AdditionalEffect4b0=addType.Length==0?0:addType[0].Text=="Spin"?3:
                throw new ContractError("physical_add_effect","/effect1_reserved14","Outside the pinned Spin parser recipe");
            if(AdditionalEffect4b0!=0)
            {
                int level=N(15);
                AdditionalEffect4b4=unchecked((uint)(level-1))<255u?level:1;
            }
            var words=new Dictionary<string,long>(StringComparer.Ordinal)
            {
                ["438"]=NoResist438,["43c"]=Accuracy43c,["440"]=Accuracy440,["44c"]=CriticalProbability44cBits,["450"]=CriticalProbability450Bits,
                ["458"]=HitRate458,["45c"]=HitRate45c,["460"]=Flat460,["464"]=Flat464,["468"]=ForceThreshold468,
                ["46c"]=RandomType46c,["470"]=RandomMask470,["474"]=Additional474,["478"]=Additional478,["47c"]=OpaqueReserved7At47c,
                ["4b0"]=AdditionalEffect4b0,["4b4"]=AdditionalEffect4b4,["4b8"]=Chance4b8,["4bc"]=Chance4bc
            };
            Constructed=words.ToImmutableDictionary(StringComparer.Ordinal);
        }
        internal ImmutableArray<SourceField> Occurrences(string name) => OrderedFields.Where(x=>x.Name==name).ToImmutableArray();

        internal static SelectedPhysical ProjectSelectedPhysical(ReadOnlySpan<byte> completeUtf16LeRecord, NpcSkillBinding binding, Pins pins)
        {
        Need(pins==RequiredPins,"physical_source_pins","/source","Exact source/native/caller context is required");
        Need(binding is not null,"physical_binding","/binding","Original ordered NPC skill binding is required");
        Need(completeUtf16LeRecord.Length is >0 and <=131072 && completeUtf16LeRecord.Length%2==0,
            "physical_source_bound","/source","Complete bounded UTF16LE record required before allocation");
        byte[] raw=completeUtf16LeRecord.ToArray(); string hash=Convert.ToHexStringLower(SHA256.HashData(raw));
        SourceIdentity? source=Sources.FirstOrDefault(x=>x.Sha256==hash);
        Need(source is not null,"physical_source_hash","/source","Only the two exact original selected record spans are admitted");
        var fields=Parse(raw);
        Need(One(fields,"id")==source.Id.ToString(CultureInfo.InvariantCulture) && One(fields,"name")==source.Name
            && One(fields,"effect1_type")=="SkillATK_Instant","physical_source_identity","/source","Selected original identity/type differs");
        string? npcName=binding.NpcId switch { 212776=>"ShadowstalkerD_30_Ae",212781=>"ShadowstalkerBigD_32_Ae",_=>null };
        int expectedLevel=binding.NpcId==212776?21:23;
        Need(npcName is not null && binding.NpcDevname==npcName && binding.SkillLevel==expectedLevel
            && binding.ListOrdinal==source.ListOrdinal && binding.SkillName==source.Name,
            "physical_source_binding","/binding","Exact supported ID/name/original ordinal/source level is required");
        Need(fields.Where(x=>x.Name.StartsWith("effect1_",StringComparison.Ordinal)).GroupBy(x=>x.Name).All(g=>g.Count()==1),
            "physical_source_singleton","/source","Repeated selected contract fields cannot be first-selected");
        foreach(string absent in new[]{"effect1_noresist","effect1_acc_mod1","effect1_acc_mod2","effect1_critical_prob_mod1",
            "effect1_critical_add_dmg_mod1","effect1_critical_add_dmg_mod2"})
            Need(!fields.Any(x=>x.Name==absent),"physical_constructor_origin","/source/"+absent,"The pinned constructor-only field origin differs");
        Need(One(fields,"effect1_critical_prob_mod2")=="100","physical_critical_recipe","/source","Pinned percent100 converts to exact1.0f");
            return new(source.Id,source.Name,hash,binding,raw,fields);
        }
    }

    internal static SelectedPhysical ProjectSelectedPhysical(ReadOnlySpan<byte> completeUtf16LeRecord, NpcSkillBinding binding, Pins pins) =>
        SelectedPhysical.ProjectSelectedPhysical(completeUtf16LeRecord,binding,pins);

    /// <summary>Actual wrapped-sum predicates of bbca20/bbcac0/bbcb70/bbccf0; preserves explicit zero.</summary>
    internal static Pair ConstructPair(int first,int second,PairKind kind)
    {
        Need(Enum.IsDefined(kind),"physical_pair_kind","/pair","Unknown native pair helper");
        bool invalid=first<0 || unchecked(first+second)<0;
        if(invalid)return new(first,second,0,kind is PairKind.Hit or PairKind.Flat?1:0,true,kind.ToString());
        if(kind==PairKind.Chance && first==0 && second==0)return new(first,second,0,100,true,kind.ToString());
        return new(first,second,first,second,false,kind.ToString());
    }
    private static int SourceInteger(ImmutableArray<SourceField> fields,string tag,int constructorZero)
    {
        var values=fields.Where(x=>x.Name==tag).ToArray();
        Need(values.Length<=1,"physical_source_singleton","/source/"+tag,"Duplicate scalar");
        if(values.Length==0)return constructorZero;
        Need(int.TryParse(values[0].Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int value),
            "physical_source_integer","/source/"+tag,"Selected int32 scalar is invalid");
        return value;
    }
    private static string One(ImmutableArray<SourceField> fields,string tag)
    {
        var values=fields.Where(x=>x.Name==tag).ToArray();
        Need(values.Length==1,"physical_source_singleton","/source/"+tag,"Exactly one original scalar is required");
        return values[0].Text;
    }
    private static ImmutableArray<SourceField> Parse(byte[] raw)
    {
        try
        {
            string xml=new UnicodeEncoding(false,false,true).GetString(raw);
            using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings
            { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536,MaxCharactersFromEntities=0 });
            XElement root=XDocument.Load(reader,LoadOptions.PreserveWhitespace).Root!;
            Need(root is not null && root.Name=="skill_base" && !root.HasAttributes && !root.Descendants().Any(x=>x.Ancestors().Count()>24),
                "physical_source_xml","/source","Original bounded skill_base root required");
            return root.Elements().Select(x=>new SourceField(x.Name.ToString(),x.Value,x.ToString(SaveOptions.DisableFormatting))).ToImmutableArray();
        }
        catch(Exception e) when(e is XmlException or DecoderFallbackException)
        { throw new ContractError("physical_source_xml","/source",e.Message); }
    }
    internal sealed class ContractError(string code,string path,string message):Exception(message)
    { internal string Code { get; }=code; internal string Path { get; }=path; }
    private static void Need([DoesNotReturnIf(false)] bool value,string code,string path,string message)
    { if(!value)throw new ContractError(code,path,message); }
}
