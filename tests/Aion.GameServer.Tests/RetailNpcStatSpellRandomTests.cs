using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

/// <summary>Native post-CVTT randomization; detached data and arithmetic only.</summary>
public sealed class RetailNpcStatSpellRandomTests
{
    [Theory]
    [InlineData(1,34,1000,500)]
    [InlineData(1,35,1000,1000)]
    [InlineData(1,64,1000,1000)]
    [InlineData(1,65,1000,1500)]
    [InlineData(2,69,1000,600)]
    [InlineData(2,70,1000,2000)]
    [InlineData(3,34,1000,900)]
    [InlineData(3,35,1000,1000)]
    [InlineData(3,65,1000,1100)]
    [InlineData(6,69,1000,1000)]
    [InlineData(6,70,1000,2000)]
    [InlineData(4,0,123,123)]
    [InlineData(1,0,0,0)]
    [InlineData(1,0,1,0)]
    [InlineData(2,0,3,1)]
    [InlineData(1,70,16777217,25165824)]
    public void RandomizationUsesTheQuantizedIntegerBaseAndStillDrawsForAConstantTableRow(int type,int word,int input,int expected)
    {
        int calls=0;
        Assert.Equal(expected,RetailNpcStatSkillCombat.SpellRandomBasic(input,type,1,(lo,hi)=>
        {calls++;Assert.Equal((0,99),(lo,hi));return word;}));
        Assert.Equal(1,calls);
    }
    [Fact]
    public void DisabledAndBonusOnlyMasksDoNotConsumeABasicDraw()
    {
        int Forbidden(int lo,int hi)=>throw new Exception("No native basic-table draw expected");
        for(int mask=1;mask<=3;mask++)Assert.Equal(123,RetailNpcStatSkillCombat.SpellRandomBasic(123,0,mask,Forbidden));
        for(int type=1;type<=10;type++)Assert.Equal(123,RetailNpcStatSkillCombat.SpellRandomBasic(123,type,2,Forbidden));
    }
    [Theory]
    [InlineData(-1,0,1)]
    [InlineData(1,-1,1)]
    [InlineData(1,11,1)]
    [InlineData(1,1,0)]
    [InlineData(1,1,4)]
    public void UnadmittedArithmeticInputsCannotBecomeADefault(int amount,int type,int mask)
        =>Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcStatSkillCombat.SpellRandomBasic(amount,type,mask,(_,_)=>0));
    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void ADrawOutsideTheNativeEndpointsCannotBeUsed(int draw)
        =>Assert.Throws<InvalidOperationException>(()=>RetailNpcStatSkillCombat.SpellRandomBasic(123,1,1,(_,_)=>draw));

    private static string Recipe(string mask,string extra="")=>"<skill_base><id>910016</id><name>SpellRandomWitness</name>"
        +"<sub_type>Attack</sub_type><apply_magical_skill_boost_bonus>0</apply_magical_skill_boost_bonus>"
        +"<apply_magical_critical>0</apply_magical_critical><effect1_type>SpellATK_Instant</effect1_type>"
        +"<effect1_reserved2>100</effect1_reserved2><effect1_reserved3>3</effect1_reserved3>"
        +"<effect1_reserved10>Fire</effect1_reserved10>"+mask+extra+"</skill_base>";
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] bytes=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
    [Theory]
    [InlineData("",1)]
    [InlineData("<effect1_reserved4>0</effect1_reserved4>",1)]
    [InlineData("<effect1_reserved4>BasicDMG</effect1_reserved4>",1)]
    [InlineData("<effect1_reserved4>basicdmg</effect1_reserved4>",1)]
    [InlineData("<effect1_reserved4>AddDMg</effect1_reserved4>",2)]
    [InlineData("<effect1_reserved4>ALL</effect1_reserved4>",3)]
    public void ConstructorAndNamedMasksKeepTheirSeparateNativeBits(string field,int expected)
    {
        var source=Parse(Recipe(field));Assert.True(source.Supported,source.UnsupportedReason);
        Assert.Equal(3,source.RandomType);Assert.Equal(expected,source.RandomMask);
    }
    [Theory]
    [InlineData("<effect1_reserved4>Anything</effect1_reserved4>","")]
    [InlineData("<effect1_reserved4>All </effect1_reserved4>","")]
    [InlineData("<effect1_reserved4>All</effect1_reserved4>","<effect1_reserved8>1</effect1_reserved8><effect1_reserved16>unproved</effect1_reserved16>")]
    [InlineData("<effect1_reserved4>AddDMG</effect1_reserved4>","<effect1_reserved9>1</effect1_reserved9><effect1_reserved16>unproved</effect1_reserved16>")]
    [InlineData("<effect1_reserved4>BasicDMG</effect1_reserved4>","<effect1_reserved7>2</effect1_reserved7>")]
    [InlineData("<effect1_reserved4>All</effect1_reserved4><effect1_reserved4>BasicDMG</effect1_reserved4>","")]
    public void UnknownMasksAndUnprovedBonusProgramsRemainVisible(string mask,string extra)
        =>Assert.False(Parse(Recipe(mask,extra)).Supported);
    [SkippableFact]
    public void AllTenSelectedNativeTableRowsMatchEveryDrawForBothBasicMasks()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_RANDOM_TABLE_SOURCE");
        Skip.If(string.IsNullOrEmpty(path),"Requires selected native table");
        byte[] raw=File.ReadAllBytes(path!);
        Assert.Equal("1901847166c70e03258072a31737d3e5d43ec7bf0eb39634acfabb2e5d53eacb",Convert.ToHexStringLower(SHA256.HashData(raw)));
        var rows=XDocument.Parse(Encoding.Unicode.GetString(raw).TrimStart('\ufeff')).Root!.Elements().ToArray();Assert.Equal(10,rows.Length);
        foreach(var row in rows)
        {
            int type=(int)row.Attribute("random_type")!;Assert.Equal(20,row.Elements().Count());
            for(int word=0;word<100;word++)
            {
                int expected=10*(int)row.Element("range"+(word/5))!;
                foreach(int mask in new[]{1,3})
                {
                    int count=0;Assert.Equal(expected,RetailNpcStatSkillCombat.SpellRandomBasic(1000,type,mask,(lo,hi)=>
                    {count++;Assert.Equal((0,99),(lo,hi));return word;}));Assert.Equal(1,count);
                }
            }
        }
    }
    [SkippableFact]
    public void EveryActualRandomSourceRetainsItsOtherIndependentRecipeGuards()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(pin),"Requires pinned native sources");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(pin,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();int physical=0,spell=0;
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(Path.GetFullPath(asset.GetProperty("path").GetString()!,Path.GetDirectoryName(path!)!));
            var source=RetailNpcStatSkillCombat.ParseSource(bytes,asset.GetProperty("sha256").GetString()!);
            if(source.Supported&&source.Family==RetailNpcStatSkillCombat.Kind.PhysicalInstant)physical++;
            if(source.Supported&&source.Family==RetailNpcStatSkillCombat.Kind.SpellInstant)spell++;
            var xml=XElement.Parse(Encoding.Unicode.GetString(bytes));
            if(xml.Element("effect1_type")?.Value!="SpellATK_Instant")continue;
            string? type=xml.Element("effect1_reserved3")?.Value,mask=xml.Element("effect1_reserved4")?.Value;
            if(type is null or "0" && mask is null or "0")continue;
            xml.SetElementValue("effect1_reserved3",0);xml.SetElementValue("effect1_reserved4",0);
            var control=Parse(xml.ToString(SaveOptions.DisableFormatting));
            Assert.Equal(control,source with {RandomType=0,RandomMask=1,Sha256=control.Sha256});
            rows.Add(new {id=source.Id,type,mask,supported=source.Supported,reason=source.UnsupportedReason,source=source.Sha256});
        }
        Assert.Equal(122,rows.Count);Assert.Equal(RetailNpcStatReviewedSourceCensus.Physical,physical);Assert.Equal(RetailNpcStatReviewedSourceCensus.Spell,spell);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SPELL_RANDOM_OUTPUT") is {Length:>0} output)
        {
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,new {physical,spell,rows});
        }
    }
}
