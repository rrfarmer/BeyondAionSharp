using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Tests;

/// <summary>Detached source/templates/effects only: no actors, world, network or SIM.</summary>
public sealed class RetailNpcStatPhysicalTextMetadataTests
{
    private static string Recipe(string text,string extra="")=>"<skill_base><id>910015</id><name>PhysicalTextWitness</name>"
        +"<effect1_type>SkillATK_Instant</effect1_type><effect1_reserved2>55</effect1_reserved2>"
        +"<effect1_reserved10>"+text+"</effect1_reserved10>"+extra+"</skill_base>";
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] raw=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(raw,Convert.ToHexStringLower(SHA256.HashData(raw)));
    }
    [Theory]
    [InlineData("Fire",SkillElement.FIRE)]
    [InlineData("fIrE",SkillElement.FIRE)]
    [InlineData("Water",SkillElement.WATER)]
    [InlineData("Air",SkillElement.WIND)]
    [InlineData("Earth",SkillElement.EARTH)]
    [InlineData("Light",SkillElement.LIGHT)]
    [InlineData("Dark",SkillElement.DARK)]
    public void PhysicalTextConstructsZeroAdditionalDamageAndNeverAnElementalDamageChannel(string text,SkillElement metadata)
    {
        var zero=Parse(Recipe("0"));var source=Parse(Recipe(text));
        Assert.True(source.Supported,source.UnsupportedReason);Assert.Equal(SkillElement.NONE,source.Element);
        Assert.Equal(0,source.AdditionalFlat);Assert.Equal(metadata,source.PhysicalR10MetadataElement);
        Assert.Equal(zero,source with {Sha256=zero.Sha256,PhysicalR10MetadataElement=SkillElement.NONE});
        Assert.True(RetailNpcStatSkillCombat.PhysicalHostMatches(source,new SkillAttackInstantEffect {Position=1,Element=metadata}));
        Assert.False(RetailNpcStatSkillCombat.PhysicalHostMatches(zero,new SkillAttackInstantEffect {Position=1,Element=metadata}));
    }
    [Theory]
    [InlineData("Inferno","")]
    [InlineData("Fire ","")]
    [InlineData("Fire","<effect1_reserved9>1</effect1_reserved9>")]
    [InlineData("Fire","<effect1_reserved7>2</effect1_reserved7>")]
    [InlineData("Fire","<effect1_reserved19>1</effect1_reserved19>")]
    [InlineData("Fire","<effect1_reserved12>All </effect1_reserved12>")]
    [InlineData("Fire","<effect1_reserved10>0</effect1_reserved10>")]
    public void KnownTextIsNotAGenericUnknownDataFallback(string text,string extra)
        =>Assert.False(Parse(Recipe(text,extra)).Supported);
    [Fact]
    public void HostFamilySlotAndLiteralDriftCannotSelectThisPhysicalProgram()
    {
        var source=Parse(Recipe("Fire"));
        Assert.False(RetailNpcStatSkillCombat.PhysicalHostMatches(source,new SpellAttackInstantEffect {Position=1,Element=SkillElement.FIRE}));
        Assert.False(RetailNpcStatSkillCombat.PhysicalHostMatches(source,new SkillAttackInstantEffect {Position=2,Element=SkillElement.FIRE}));
        Assert.False(RetailNpcStatSkillCombat.PhysicalHostMatches(source,new SkillAttackInstantEffect {Position=1,Element=SkillElement.WATER}));
    }
    [Fact]
    public void OnlyTheCalculatedEffectChangesClassificationAndTheSharedHostTemplateStaysUntouched()
    {
        var template=new SkillTemplate {skillId=910015,effects=new Effects {effects=[new SkillAttackInstantEffect {Position=1,Element=SkillElement.FIRE}]}};
        // Effect construction here reads only the detached template's dispel count.
        var retail=new Effect(null!,null!,template,1);var legacy=new Effect(null!,null!,template,1);
        Assert.False(retail.IsPhysicalEffect());Assert.False(legacy.IsPhysicalEffect());
        retail.MarkRetailNpcPhysicalPrimary();
        Assert.True(retail.IsPhysicalEffect());Assert.False(legacy.IsPhysicalEffect());
        Assert.Equal(SkillElement.FIRE,template.GetEffectTemplate(1)!.GetElement());
    }
    [SkippableFact]
    public void EveryActualTextRecordMatchesItsNumericZeroControlAndAllNineAltgardParentsBind()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? hosts=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOST_SKILLS");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(pin)||string.IsNullOrEmpty(hosts),"Requires pinned native/host sources");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(pin,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();int physical=0,spell=0;
        var host=XDocument.Load(hosts!).Root!.Elements("skill_template").ToDictionary(x=>(int)x.Attribute("skill_id")!);
        int[] turret=[21701,21702,21703,21704,21705,21706,21707,21708,21737];var bound=new List<int>();
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(Path.GetFullPath(asset.GetProperty("path").GetString()!,Path.GetDirectoryName(path!)!));
            var source=RetailNpcStatSkillCombat.ParseSource(bytes,asset.GetProperty("sha256").GetString()!);
            if(source.Supported&&source.Family==RetailNpcStatSkillCombat.Kind.PhysicalInstant)physical++;
            if(source.Supported&&source.Family==RetailNpcStatSkillCombat.Kind.SpellInstant)spell++;
            var xml=XElement.Parse(Encoding.Unicode.GetString(bytes));string? text=xml.Element("effect1_reserved10")?.Value;
            if(xml.Element("effect1_type")?.Value!="SkillATK_Instant"||text is null||int.TryParse(text,out _))continue;
            xml.SetElementValue("effect1_reserved10",0);var zero=Parse(xml.ToString(SaveOptions.DisableFormatting));
            Assert.Equal(zero,source with {Sha256=zero.Sha256,PhysicalR10MetadataElement=SkillElement.NONE});
            var part=host[source.Id].Element("effects")!.Elements().Single(x=>(int?)x.Attribute("e")==1);
            Assert.Equal("skillatk",part.Name.LocalName);
            Assert.True(RetailNpcStatSkillCombat.PhysicalHostMatches(source,new SkillAttackInstantEffect {Position=1,Element=Enum.Parse<SkillElement>((string)part.Attribute("element")!)}));
            if(turret.Contains(source.Id))
            {
                Assert.True(source.Supported,source.UnsupportedReason);
                bound.Add(source.Id);
            }
            rows.Add(new {id=source.Id,text,supported=source.Supported,reason=source.UnsupportedReason,source=source.Sha256,self_only=source.SelfOnlyPrimary});
        }
        Assert.Equal(13,rows.Count);Assert.Equal(turret,bound.Order().ToArray());Assert.Equal(RetailNpcStatReviewedSourceCensus.Spell,spell);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_PHYSICAL_TEXT_OUTPUT") is {Length:>0} output)
        {
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,new {physical,spell,rows});
        }
    }
}
