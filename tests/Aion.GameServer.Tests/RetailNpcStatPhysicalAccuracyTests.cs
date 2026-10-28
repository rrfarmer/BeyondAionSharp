using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

/// <summary>Offline native-order witnesses and complete source admission; no SIM.</summary>
public sealed class RetailNpcStatPhysicalAccuracyTests
{
    [Theory]
    [InlineData(100,0,2000,2100)]
    [InlineData(9000,2000,-2000,9000)]
    [InlineData(100,0,-2000,0)]
    [InlineData(100,0,9999,9999)]
    [InlineData(int.MaxValue,0,1,0)]
    [InlineData(int.MinValue,-1,0,9999)]
    [InlineData(9999,1,-2,9998)]
    [InlineData(-100,0,200,100)]
    public void NamedAccuracyAddsBeforeTheOnlyClampWithNativeInt32Wrapping(int current,int bonus,int source,int expected)
        =>Assert.Equal(expected,RetailNpcStatSkillCombat.PhysicalAccuracy(current,bonus,source));

    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] raw=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(raw,Convert.ToHexStringLower(SHA256.HashData(raw)));
    }
    [Theory]
    [InlineData("<effect1_acc_mod1>1</effect1_acc_mod1>")]
    [InlineData("<effect1_acc_mod2>1.5</effect1_acc_mod2>")]
    [InlineData("<effect1_acc_mod2>2147483648</effect1_acc_mod2>")]
    [InlineData("<effect1_acc_mod2>1</effect1_acc_mod2><effect1_acc_mod2>2</effect1_acc_mod2>")]
    [InlineData("<effect1_acc_mod2>100</effect1_acc_mod2><effect1_reserved9>1</effect1_reserved9>")]
    public void AccuracyDoesNotBypassOtherOperandsOrUnverifiedLevelRates(string extra)
        =>Assert.False(Parse("<skill_base><id>910013</id><name>AccuracyBoundary</name><effect1_type>SkillATK_Instant</effect1_type>"+extra+"</skill_base>").Supported);

    [SkippableFact]
    public void AllFiveActualPhysicalAccuracySourcesKeepIndependentDamageGuards()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(pin),"Requires pinned native data");
        byte[] raw=File.ReadAllBytes(path!);Assert.Equal(pin,Convert.ToHexStringLower(SHA256.HashData(raw)));
        using var manifest=JsonDocument.Parse(raw);var rows=new List<object>();var reviewed=new List<int>();var admitted=new List<int>();
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(Path.GetFullPath(asset.GetProperty("path").GetString()!,Path.GetDirectoryName(path!)!));
            var source=RetailNpcStatSkillCombat.ParseSource(bytes,asset.GetProperty("sha256").GetString()!);
            var xml=XElement.Parse(Encoding.Unicode.GetString(bytes));
            if(xml.Element("effect1_type")?.Value!="SkillATK_Instant"||((int?)xml.Element("effect1_acc_mod2")??0)==0)continue;
            int flat=(int)xml.Element("effect1_acc_mod2")!;reviewed.Add(source.Id);
            xml.SetElementValue("effect1_acc_mod2",0);var zero=Parse(xml.ToString(SaveOptions.DisableFormatting));
            Assert.Equal(zero.Supported,source.Supported);
            if(source.Supported)
            {
                admitted.Add(source.Id);Assert.Equal(flat,source.AccuracyFlat);
                Assert.Equal(zero,source with {AccuracyFlat=0,Sha256=zero.Sha256});
            }
            else Assert.Equal(20742,source.Id); // Independent R9/mask/condition limits, not accuracy.
            rows.Add(new {id=source.Id,flat,supported=source.Supported,reason=source.UnsupportedReason,source=source.Sha256});
        }
        Assert.Equal(new[]{17815,17816,20742,20834,20836},reviewed.Order().ToArray());
        Assert.Equal(new[]{17815,17816,20834,20836},admitted.Order().ToArray());
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_ACCURACY_AUDIT_OUTPUT") is {Length:>0} output)
        {
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,rows);
        }
    }
}
