using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

/// <summary>Pure source admission and arithmetic invariance; no actors or SIM.</summary>
public sealed class RetailNpcStatHateMetadataTests
{
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] raw=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(raw,Convert.ToHexStringLower(SHA256.HashData(raw)));
    }
    private const string Recipe="<skill_base><id>910012</id><name>HateMetadataWitness</name>"
        +"<effect1_type>SkillATK_Instant</effect1_type><effect1_reserved2>120</effect1_reserved2>"
        +"<effect1_reserved3>2</effect1_reserved3><effect1_reserved4>30</effect1_reserved4></skill_base>";
    [Theory]
    [InlineData(0,0)]
    [InlineData(60,60)]
    [InlineData(-1,int.MinValue)]
    [InlineData(int.MaxValue,int.MaxValue)]
    public void SeparateHateOperandsDoNotChangeTheDamageRecipe(int a,int b)
    {
        var baseline=Parse(Recipe);
        var source=Parse(Recipe.Replace("</skill_base>",$"<effect1_hop_type>SkillLv</effect1_hop_type><effect1_hop_a>{a}</effect1_hop_a><effect1_hop_b>{b}</effect1_hop_b></skill_base>"));
        Assert.True(source.Supported,source.UnsupportedReason);
        Assert.Equal(baseline,source with {Sha256=baseline.Sha256});
    }
    [Theory]
    [InlineData("<effect1_hop_a>1.5</effect1_hop_a>")]
    [InlineData("<effect1_hop_b>2147483648</effect1_hop_b>")]
    [InlineData("<effect1_hop_a><value>60</value></effect1_hop_a>")]
    [InlineData("<effect1_hop_b>1</effect1_hop_b><effect1_hop_b>2</effect1_hop_b>")]
    [InlineData("<effect1_hop_c>0</effect1_hop_c>")]
    [InlineData("<effect1_hop_a>60</effect1_hop_a><effect1_remain1>0</effect1_remain1>")]
    [InlineData("<effect1_hop_a>60</effect1_hop_a><effect1_reserved9>1</effect1_reserved9>")]
    public void MalformedMetadataAndIndependentBoundariesRemainVisible(string extra)
        =>Assert.False(Parse(Recipe.Replace("</skill_base>",extra+"</skill_base>")).Supported);

    [SkippableFact]
    public void EveryActualMetadataRecipeEqualsItsIndependentlyStrippedDamageRecipe()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(pin),"Requires pinned external sources");
        byte[] manifest=File.ReadAllBytes(path!);Assert.Equal(pin,Convert.ToHexStringLower(SHA256.HashData(manifest)));
        using var doc=JsonDocument.Parse(manifest);var rows=new List<object>();int reviewed=0,admitted=0;var accepted=new List<int>();
        foreach(var asset in doc.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] raw=File.ReadAllBytes(Path.GetFullPath(asset.GetProperty("path").GetString()!,Path.GetDirectoryName(path!)!));
            var source=RetailNpcStatSkillCombat.ParseSource(raw,asset.GetProperty("sha256").GetString()!);
            var xml=XElement.Parse(Encoding.Unicode.GetString(raw));
            if(xml.Element("effect1_type")?.Value is not ("SkillATK_Instant" or "SpellATK_Instant")
                ||!xml.Elements().Any(x=>x.Name.LocalName is "effect1_hop_a" or "effect1_hop_b"))continue;
            reviewed++;xml.Elements().Where(x=>x.Name.LocalName is "effect1_hop_a" or "effect1_hop_b").Remove();
            var stripped=Parse(xml.ToString(SaveOptions.DisableFormatting));
            Assert.Equal(stripped,source with {Sha256=stripped.Sha256});
            if(source.Supported){admitted++;accepted.Add(source.Id);}
            rows.Add(new {id=source.Id,supported=source.Supported,reason=source.UnsupportedReason});
        }
        Assert.Equal(24,reviewed);
        Assert.Equal(new[]{17459,17460,17462,17463,17519,17528,18607,19739,20215,20682,20834,20836},accepted.Order().ToArray());
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOP_AUDIT_OUTPUT") is {Length:>0} output)
        {
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);
            JsonSerializer.Serialize(stream,new {reviewed,admitted,rows});
        }
    }
}
