using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantParameters;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcPhysicalInstantParametersTests
{
    private static readonly Lazy<JsonDocument> Evidence=new(()=>
    {
        using var input=new MemoryStream(Convert.FromBase64String(FrozenEvidence));
        using var gzip=new GZipStream(input,CompressionMode.Decompress); using var output=new MemoryStream();
        gzip.CopyTo(output); byte[] bytes=output.ToArray();
        Assert.Equal("41fe10eb19d400c9856835c0b45fc04c42749471aebbcd6b555a903d647e191e",Convert.ToHexStringLower(SHA256.HashData(bytes)));
        return JsonDocument.Parse(bytes);
    });
    internal static JsonElement Case(int skill)=>Evidence.Value.RootElement.GetProperty("rows").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==skill);
    internal static byte[] SourceBytes(int skill)=>Convert.FromBase64String(Case(skill).GetProperty("raw_base64").GetString()!);
    internal static P.NpcSkillBinding Binding(int skill,int npc)
    {
        var row=Case(skill); var b=row.GetProperty("bindings").EnumerateArray().Single(x=>x.GetProperty("npc_id").GetInt32()==npc);
        int level=int.Parse(b.GetProperty("ordered_fields").EnumerateArray().Single(x=>x[0].GetString()=="skill_level")[1].GetString()!,System.Globalization.CultureInfo.InvariantCulture);
        return new(npc,b.GetProperty("npc_devname").GetString()!,b.GetProperty("ordinal").GetInt32(),level,row.GetProperty("name").GetString()!);
    }
    internal static P.SelectedPhysical Select(int skill,int npc=212776)=>P.ProjectSelectedPhysical(SourceBytes(skill),Binding(skill,npc),P.RequiredPins);

    [Theory]
    [InlineData(16556,212776)] [InlineData(16556,212781)] [InlineData(16794,212776)] [InlineData(16794,212781)]
    public void CompletePinnedRecordsConstructTheIndependentWordAndDescriptorCases(int skill,int npc)
    {
        var p=Select(skill,npc); var expected=Case(skill);
        Assert.Equal(expected.GetProperty("source").GetProperty("raw_sha256").GetString(),p.RawSha256);
        Assert.Equal(skill,p.Id); Assert.Equal(expected.GetProperty("name").GetString(),p.Name);
        Assert.Equal(Binding(skill,npc),p.Binding); Assert.Equal((short)p.Binding.SkillLevel,p.SignedSourceLevel); Assert.Equal(0,p.EffectTypeIndex);
        var words=expected.GetProperty("constructed_raw_words").EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.GetInt64());
        Assert.Equal(words.OrderBy(x=>x.Key),p.Constructed.OrderBy(x=>x.Key));
        Assert.Equal(expected.GetProperty("condition480_hex").GetString(),Convert.ToHexStringLower(p.Condition480.AsSpan()));
        Assert.Equal(48,p.Condition480.Length); Assert.Equal(JsonValueKind.Null,expected.GetProperty("current_global_race_mask").ValueKind);
        Assert.Equal(SourceBytes(skill),p.RawBytes.ToArray());
        var original=expected.GetProperty("ordered_fields").EnumerateArray().Select(x=>(x[0].GetString(),x[1].GetString())).ToArray();
        Assert.Equal(original,p.OrderedFields.Select(x=>((string?)x.Name,(string?)x.Text)).ToArray());
        var b=expected.GetProperty("bindings").EnumerateArray().Single(x=>x.GetProperty("npc_id").GetInt32()==npc);
        Assert.Equal(b.GetProperty("ordered_fields").EnumerateArray().Select(x=>(x[0].GetString(),x[1].GetString())),
            p.OrderedBindingFields.Select(x=>((string?)x.Name,(string?)x.Text)));
        Assert.False(p.ActivationReady); Assert.False(p.IsCurrentActorObservation);
    }
    [Fact]
    public void ConstructorZeroExplicitZeroAndCheckedChanceDefaultRemainDistinct()
    {
        var back=Select(16556); var front=Select(16794);
        Assert.Empty(back.Occurrences("effect1_reserved3")); Assert.Equal("0",Assert.Single(back.Occurrences("effect1_reserved11")).Text);
        Assert.Empty(back.Occurrences("effect1_noresist")); Assert.Equal((byte)1,back.OpaqueReserved7At47c); Assert.Equal(0,back.NoResist438);
        Assert.Empty(back.Occurrences("effect1_reserved17")); Assert.Equal("0",Assert.Single(front.Occurrences("effect1_reserved17")).Text);
        Assert.Equal("100",Assert.Single(front.Occurrences("effect1_reserved18")).Text);
        Assert.True(back.PairRecipes[3].DefaultApplied); Assert.False(front.PairRecipes[3].DefaultApplied);
        Assert.Equal(100,back.Chance4bc); Assert.Equal(100,front.Chance4bc);
        Assert.Equal(0,back.Flat460); Assert.Equal(0,back.Flat464); Assert.False(back.PairRecipes[1].DefaultApplied);
        Assert.Equal(0,back.AdditionalEffect4b0); Assert.Equal(0,back.AdditionalEffect4b4);
        Assert.Equal(3,front.AdditionalEffect4b0); Assert.Equal(1,front.AdditionalEffect4b4);
        Assert.Empty(back.Occurrences("effect1_acc_mod1")); Assert.Empty(back.Occurrences("effect1_acc_mod2"));
        Assert.Equal(0,back.Accuracy43c); Assert.Equal(0,back.Accuracy440);
    }
    [Theory]
    [InlineData("id")] [InlineData("name")] [InlineData("ordinal")] [InlineData("level")] [InlineData("skill")] [InlineData("other-record")]
    public void SourceOrderAndSupportedBindingCannotBeReplaced(string invalid)
    {
        var b=Binding(16556,212776);
        b=invalid switch { "id"=>b with{NpcId=885027},"name"=>b with{NpcDevname="different"},
            "ordinal"=>b with{ListOrdinal=0},"level"=>b with{SkillLevel=23},"skill"=>b with{SkillName="NAS_Spin_Front"},_=>b };
        byte[] raw=SourceBytes(invalid=="other-record"?16794:16556);
        Assert.Throws<P.ContractError>(()=>P.ProjectSelectedPhysical(raw,b,P.RequiredPins));
    }
    [Theory]
    [InlineData("main")] [InlineData("package")] [InlineData("source")] [InlineData("caller")]
    public void EveryNativeSourceContextPinIsRequired(string invalid)
    {
        var pin=P.RequiredPins;
        pin=invalid switch {"main"=>pin with{MainSha256="wrong"},"package"=>pin with{PackageContext="wrong"},
            "source"=>pin with{SourceEvidenceSha256="wrong"},_=>pin with{CallerReviewSha256="wrong"}};
        Assert.Throws<P.ContractError>(()=>P.ProjectSelectedPhysical(SourceBytes(16556),Binding(16556,212776),pin));
    }
    [Theory]
    [InlineData("empty")] [InlineData("odd")] [InlineData("large")] [InlineData("changed")] [InlineData("appended")]
    public void OnlyCompleteBoundedOriginalSpansAreAccepted(string invalid)
    {
        byte[] raw=SourceBytes(16556);
        if(invalid=="empty")raw=[];
        if(invalid=="odd")raw=raw[..^1];
        if(invalid=="large")raw=new byte[131074];
        if(invalid=="changed")raw[100]^=1;
        if(invalid=="appended")raw=[..raw,0,0];
        Assert.Throws<P.ContractError>(()=>P.ProjectSelectedPhysical(raw,Binding(16556,212776),P.RequiredPins));
    }
    [Theory]
    [InlineData((int)P.PairKind.Hit,1,-1,1,-1,false)]
    [InlineData((int)P.PairKind.Hit,-1,1,0,1,true)]
    [InlineData((int)P.PairKind.Hit,int.MaxValue,1,0,1,true)]
    [InlineData((int)P.PairKind.Flat,0,0,0,0,false)]
    [InlineData((int)P.PairKind.Flat,1,-2,0,1,true)]
    [InlineData((int)P.PairKind.Additional,1,-2,0,0,true)]
    [InlineData((int)P.PairKind.Chance,0,0,0,100,true)]
    [InlineData((int)P.PairKind.Chance,0,100,0,100,false)]
    [InlineData((int)P.PairKind.Chance,1,-1,1,-1,false)]
    public void NativePairPredicatesUseFirstAndWrappedSum(int kind,int a,int b,int first,int second,bool fallback)
    {
        var p=P.ConstructPair(a,b,(P.PairKind)kind);
        Assert.Equal(a,p.RawFirst); Assert.Equal(b,p.RawSecond); Assert.Equal(first,p.First); Assert.Equal(second,p.Second); Assert.Equal(fallback,p.DefaultApplied);
    }
    [Fact]
    public void AdmittedBytesAndConstructedViewsCannotBeChangedThroughCallerStorage()
    {
        byte[] bytes=SourceBytes(16794); byte[] original=(byte[])bytes.Clone();
        var p=P.ProjectSelectedPhysical(bytes,Binding(16794,212776),P.RequiredPins); Array.Fill(bytes,(byte)0);
        Assert.Equal(original,p.RawBytes.ToArray());
        var changed=p.Condition480.SetItem(0x2c,99); Assert.Equal((byte)2,p.Condition480[0x2c]); Assert.Equal((byte)99,changed[0x2c]);
        Assert.Equal(60,p.Additional478); Assert.Equal(99,p.Constructed.SetItem("478",99)["478"]); Assert.Equal(60,p.Constructed["478"]);
        Assert.DoesNotContain(typeof(P.SelectedPhysical).GetConstructors(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic),x=>!x.IsPrivate);
    }
    private const string FrozenEvidence="""
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
/tP/AkqustcpkgAA
""";
}
