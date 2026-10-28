using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatPhysicalRandomMaskTests
{
    private static RetailNpcStatSkillCombat.Source Parse(string mask)
    {
        string xml="<skill_base><id>910035</id><name>PhysicalRandomMask</name><first_target>Target</first_target>"
            +"<sub_type>Attack</sub_type><target_range>OnlyOne</target_range><target_maxcount>1</target_maxcount>"
            +"<effect1_type>SkillATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
            +"<effect1_reserved2>60</effect1_reserved2><effect1_reserved6>100</effect1_reserved6>"
            +"<effect1_reserved8>1</effect1_reserved8><effect1_reserved10>71</effect1_reserved10>"+mask+"</skill_base>";
        byte[] bytes=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Theory]
    [InlineData("",1)] [InlineData("0",1)] [InlineData("BasicDMG",1)]
    [InlineData("bASICdMG",1)] [InlineData("AddDMG",2)] [InlineData("aDDdMG",2)] [InlineData("All",3)]
    public void NativeNamedMasksAreCaseInsensitiveWithExplicitConstructorDefault(string text,int mask)
    {
        var s=Parse(text.Length==0?"":"<effect1_reserved12>"+text+"</effect1_reserved12>");
        Assert.True(s.Supported,s.UnsupportedReason);Assert.Equal(mask,s.RandomMask);
    }

    [Theory]
    [InlineData("All ")] [InlineData("2")] [InlineData("Other")]
    public void UnknownMaskNamesStayVisible(string text)
        =>Assert.False(Parse("<effect1_reserved12>"+text+"</effect1_reserved12>").Supported);

    [Theory]
    [InlineData(1,0,535.75f)] [InlineData(1,34,535.75f)]
    [InlineData(1,35,571.25f)] [InlineData(1,64,571.25f)]
    [InlineData(1,65,606.75f)] [InlineData(1,99,606.75f)]
    [InlineData(2,0,542.85f)] [InlineData(2,70,642.25f)]
    public void IndependentAdditionalTableKeepsFloatFractionUntilLaterTruncation(int type,int word,float expected)
    {
        var calls=new List<(int,int)>();
        int Draw(int low,int high){calls.Add((low,high));return high==100?100:word;}
        var result=RetailNpcStatPhysicalBonus.Apply(500.25f,71,true,type,2,Draw);
        Assert.Equal(new[]{(1,100),(0,99)},calls);
        Assert.Equal(expected,result.Damage);Assert.Equal(100,result.ChanceDraw);Assert.Equal(word,result.RandomDraw);
        // A spell-style integer bonus would discard the authored fraction early.
        if(type==1&&word<35)Assert.NotEqual(535.25f,result.Damage);
    }

    [Theory]
    [InlineData(0,1,false)] [InlineData(0,3,false)] [InlineData(1,1,false)]
    [InlineData(1,2,true)] [InlineData(1,3,true)] [InlineData(4,2,true)]
    public void MaskBitTwoAndNonzeroTypeOwnTheAdditionalDraw(int type,int mask,bool random)
    {
        int count=0;
        var r=RetailNpcStatPhysicalBonus.Apply(500,71,true,type,mask,(low,high)=>{count++;return high;});
        Assert.Equal(random?2:1,count);Assert.Equal(random?99:null,r.RandomDraw);
        if(type==4)Assert.Equal(571f,r.Damage); // Constant100 table still consumes its draw.
    }

    [Theory]
    [InlineData(false,71)] [InlineData(true,0)]
    public void FalseConditionAndZeroBonusConsumeNeitherAdditionalDraw(bool applies,int additional)
    {
        var r=RetailNpcStatPhysicalBonus.Apply(500,additional,applies,1,3,(_,_)=>throw new Exception("No draw expected"));
        Assert.Equal(500f,r.Damage);Assert.Null(r.ChanceDraw);Assert.Null(r.RandomDraw);
    }

    [SkippableFact]
    public void ActualAddDmgSkillAdmitsOnlyItsFlatProgramAndAllMaskSkillKeepsOtherGuards()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned external source records");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path!));int seen=0;
        foreach(var a in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            int id=a.GetProperty("id").GetInt32();if(id is not (18198 or 20742))continue;seen++;
            var source=RetailNpcStatSkillCombat.ParseSource(File.ReadAllBytes(a.GetProperty("path").GetString()!),a.GetProperty("sha256").GetString()!);
            if(id==18198)
            {
                Assert.True(source.Supported,source.UnsupportedReason);Assert.Equal(2,source.RandomMask);
                Assert.Equal(1000,source.PhysicalBonus!.ValueAtLevel(1));Assert.Equal(65,source.Percent);
                Assert.Equal(2000f,RetailNpcStatPhysicalBonus.Apply(500,1000,true,source.RandomType,source.RandomMask,(_,h)=>h).Damage);
            }
            else
            {
                Assert.False(source.Supported); // Its Pulled/status and R6 program is not proved by a random-mask correction.
                Assert.Equal("Unreviewed conditional target, child, sibling or random context",source.UnsupportedReason);
            }
        }
        Assert.Equal(2,seen);
    }

    [SkippableFact]
    public void EveryActualPhysicalMaskKeepsItsOtherOperandsAndIndependentGuards()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(pin),"Requires pinned complete source corpus");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(pin,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);int supported=0;
        foreach(var a in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] sourceBytes=File.ReadAllBytes(a.GetProperty("path").GetString()!);
            var xml=XElement.Parse(Encoding.Unicode.GetString(sourceBytes));
            if(xml.Element("effect1_type")?.Value!="SkillATK_Instant")continue;
            var source=RetailNpcStatSkillCombat.ParseSource(sourceBytes,a.GetProperty("sha256").GetString()!);
            string? mask=xml.Element("effect1_reserved12")?.Value;
            if(mask is not null&&mask!="0"&&!new[]{"BasicDmg","AddDmg","All"}.Contains(mask,StringComparer.OrdinalIgnoreCase))
            {Assert.False(source.Supported);continue;}
            xml.SetElementValue("effect1_reserved12","BasicDMG");
            byte[] bytes=Encoding.Unicode.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
            var control=RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
            Assert.Equal(control,source with {Sha256=control.Sha256,RandomMask=1});
            if(source.Supported)supported++;
        }
        Assert.Equal(RetailNpcStatReviewedSourceCensus.Physical,supported);
    }
}
