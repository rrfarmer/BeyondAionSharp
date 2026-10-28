using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>Offline source construction, native ignored fields and element spelling.</summary>
public sealed class RetailNpcStatSpellSourceFieldsTests
{
    private static string Recipe(string element="Fire",string extra="")=>
        "<skill_base><id>910014</id><name>NativeSpellFieldWitness</name><sub_type>Attack</sub_type>"
        +"<apply_magical_skill_boost_bonus>1</apply_magical_skill_boost_bonus><apply_magical_critical>0</apply_magical_critical>"
        +"<effect1_type>SpellATK_Instant</effect1_type><effect1_reserved2>120</effect1_reserved2>"
        +"<effect1_reserved6>1</effect1_reserved6><effect1_reserved10>"+element+"</effect1_reserved10>"+extra+"</skill_base>";
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] bytes=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("100")]
    [InlineData("-1")]
    [InlineData("2147483647")]
    [InlineData("-2147483648")]
    public void SpellR11DoesNotConstructPhysicalForceOrAnotherDamageCoefficient(string? value)
    {
        var baseline=Parse(Recipe());
        var source=Parse(Recipe(extra:value is null?"":"<effect1_reserved11>"+value+"</effect1_reserved11>"));
        Assert.True(source.Supported,source.UnsupportedReason);Assert.Equal(0,source.ForceThreshold);
        Assert.Equal(baseline,source with {Sha256=baseline.Sha256});
    }
    [Theory]
    [InlineData("Fire",SkillElement.FIRE)]
    [InlineData("fire",SkillElement.FIRE)]
    [InlineData("fIrE",SkillElement.FIRE)]
    [InlineData("WATER",SkillElement.WATER)]
    [InlineData("water",SkillElement.WATER)]
    [InlineData("aIr",SkillElement.WIND)]
    [InlineData("earth",SkillElement.EARTH)]
    public void NativeElementNamesUseAsciiCaseInsensitiveComparison(string text,SkillElement expected)
    {
        var source=Parse(Recipe(text));Assert.True(source.Supported,source.UnsupportedReason);Assert.Equal(expected,source.Element);
    }
    [Theory]
    [InlineData("fire ","")]
    [InlineData("wind","")]
    [InlineData("light","")]
    [InlineData("dark","")]
    [InlineData("Fire","<effect1_reserved11>100.0</effect1_reserved11>")]
    [InlineData("Fire","<effect1_reserved11>2147483648</effect1_reserved11>")]
    [InlineData("Fire","<effect1_reserved11>0</effect1_reserved11><effect1_reserved11>100</effect1_reserved11>")]
    [InlineData("Fire","<effect1_reserved9>1</effect1_reserved9><effect1_reserved16>unproved</effect1_reserved16>")]
    [InlineData("Fire","<effect1_reserved3>11</effect1_reserved3>")]
    [InlineData("Fire","<effect1_reserved20>1</effect1_reserved20>")]
    public void DistinctChannelsMalformedDataAndOtherNumericProgramsRemainGuarded(string element,string extra)
        =>Assert.False(Parse(Recipe(element,extra)).Supported);
    [Fact]
    public void MissingRetailRootFlagsEqualExplicitZeroAndMalformedFlagsRemainGuarded()
    {
        string explicitXml=Recipe().Replace("<apply_magical_skill_boost_bonus>1</apply_magical_skill_boost_bonus>",
            "<apply_magical_skill_boost_bonus>0</apply_magical_skill_boost_bonus>");
        string missing=explicitXml.Replace("<apply_magical_skill_boost_bonus>0</apply_magical_skill_boost_bonus>","")
            .Replace("<apply_magical_critical>0</apply_magical_critical>","");
        var expected=Parse(explicitXml);var actual=Parse(missing);
        Assert.True(actual.Supported,actual.UnsupportedReason);
        Assert.False(actual.ApplyBoost);Assert.False(actual.ApplyCritical);
        Assert.Equal(expected,actual with {Sha256=expected.Sha256});
        Assert.False(Parse(missing.Replace("<sub_type>","<apply_magical_critical>unknown</apply_magical_critical><sub_type>")).Supported);
    }
    [Fact]
    public void PhysicalR11StillOwnsItsIndependentForcedHitThreshold()
    {
        var source=Parse("<skill_base><id>910014</id><name>PhysicalForceWitness</name><effect1_type>SkillATK_Instant</effect1_type>"
            +"<effect1_reserved11>100</effect1_reserved11></skill_base>");
        Assert.True(source.Supported);Assert.Equal(100,source.ForceThreshold);
        Assert.False(Parse("<skill_base><id>910014</id><name>PhysicalForceWitness</name><effect1_type>SkillATK_Instant</effect1_type>"
            +"<effect1_reserved11>-1</effect1_reserved11></skill_base>").Supported);
    }
    [SkippableFact]
    public void EveryActualSpellMatchesItsCanonicalElementAndR11Control()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(pin),"Requires pinned native data");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(pin,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();int spells=0,physical=0;
        string? hostPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOST_SKILLS");
        Skip.If(string.IsNullOrEmpty(hostPath),"Requires pinned host templates");
        var hosts=XDocument.Load(hostPath!).Root!.Elements().ToDictionary(x=>(int)x.Attribute("skill_id")!);
        int[] newSources=[19184,19185,19295,20893,20894,20895,20902,20905,20906,20913,20914,20916,20921,20963,20964,21891,21892,21893];
        int matchedHosts=0;
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(Path.GetFullPath(asset.GetProperty("path").GetString()!,Path.GetDirectoryName(path!)!));
            var source=RetailNpcStatSkillCombat.ParseSource(bytes,asset.GetProperty("sha256").GetString()!);
            if(source.Supported&&source.Family==RetailNpcStatSkillCombat.Kind.SpellInstant)spells++;
            if(source.Supported&&source.Family==RetailNpcStatSkillCombat.Kind.PhysicalInstant)physical++;
            var xml=XElement.Parse(Encoding.Unicode.GetString(bytes));
            if(xml.Element("effect1_type")?.Value!="SpellATK_Instant")continue;
            if(newSources.Contains(source.Id))
            {
                Assert.True(source.Supported,source.UnsupportedReason);
                var first=hosts[source.Id].Element("effects")!.Elements().First();
                Assert.Equal("spellatkinstant",first.Name.LocalName);
                Assert.True(Enum.TryParse<SkillElement>((string?)first.Attribute("element"),out var element48));
                Assert.Equal(source.Element,element48);matchedHosts++;
            }
            if(xml.Element("apply_magical_skill_boost_bonus") is null||xml.Element("apply_magical_critical") is null)
            {
                var controlFlags=new XElement(xml);
                if(controlFlags.Element("apply_magical_skill_boost_bonus") is null)controlFlags.SetElementValue("apply_magical_skill_boost_bonus",0);
                if(controlFlags.Element("apply_magical_critical") is null)controlFlags.SetElementValue("apply_magical_critical",0);
                var controlFlagSource=Parse(controlFlags.ToString(SaveOptions.DisableFormatting));
                Assert.Equal(controlFlagSource,source with {Sha256=controlFlagSource.Sha256});
            }
            string? original=xml.Element("effect1_reserved11")?.Value,element=xml.Element("effect1_reserved10")?.Value;
            xml.SetElementValue("effect1_reserved11",100);
            string? canonical=element?.ToLowerInvariant() switch {"fire"=>"Fire","water"=>"Water","air"=>"Air","earth"=>"Earth",_=>element};
            if(canonical is not null)xml.SetElementValue("effect1_reserved10",canonical);
            var control=Parse(xml.ToString(SaveOptions.DisableFormatting));
            Assert.Equal(control,source with {Sha256=control.Sha256});
            rows.Add(new {id=source.Id,r11=original,element,supported=source.Supported,reason=source.UnsupportedReason,source=source.Sha256});
        }
        Assert.Equal(newSources.Length,matchedHosts);
        Assert.True(rows.Count>0);Assert.Equal(RetailNpcStatReviewedSourceCensus.Spell,spells);Assert.Equal(RetailNpcStatReviewedSourceCensus.Physical,physical);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SPELL_FIELD_OUTPUT") is {Length:>0} output)
        {
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,new {spells,physical,rows});
        }
    }
}
