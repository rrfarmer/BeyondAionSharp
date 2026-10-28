using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Xunit;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatPeriodicDrainSourceTests
{
    private static (byte[] Bytes, string Hash) SelectedSource()
    {
        string? path = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path), "Requires pinned retail source package");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(path!));
        var asset = manifest.RootElement.GetProperty("skill_sources").EnumerateArray()
            .Single(x => x.GetProperty("id").GetInt32() == 18726);
        return (File.ReadAllBytes(asset.GetProperty("path").GetString()!),
            asset.GetProperty("sha256").GetString()!);
    }

    [SkippableFact]
    public void EggDrainSourceIsExtractedButRemainsGatedUntilBothEffectsAreIntegrated()
    {
        var (bytes, hash) = SelectedSource();
        var source = RetailNpcStatPeriodicCombat.Parse(bytes, hash);
        var part = Assert.Single(source.Parts);
        Assert.Equal(18726, source.Id);
        var initial=Assert.IsType<RetailNpcStatPeriodicCombat.InstantDrainOperands>(source.InitialDrain);
        Assert.Equal((75000,0,0), (initial.Value,initial.LevelRate,initial.WeaponMode));
        Assert.Equal((0,200,0,0), (initial.HpRate,initial.HpPercent,initial.MpRate,initial.MpPercent));
        Assert.Equal((1,0), (initial.NoResist,initial.CriticalPercent));
        Assert.Equal(("Earth","Target_Only","Target","OnlyOne","Friend"),
            (initial.Element,initial.TargetType,initial.FirstTarget,initial.TargetRange,initial.TargetRelation));
        Assert.Equal((1,1,1),(initial.TargetMaxCount,initial.ApplyBoost,initial.ApplyCritical));
        Assert.Equal(50,initial.FirstTargetValidDistance);
        Assert.Equal("All",initial.TargetSpecies);
        Assert.Contains(initial.OrderedPrimaryFields,x=>x.Key=="effect1_reserved14"&&x.Value=="0");
        Assert.Equal(2, part.Position);
        Assert.Equal(RetailNpcPeriodicSpellParameters.Family.SpellDrain, part.Family);
        Assert.Equal((0, 75000, 3000), (part.Delta, part.Value, part.Interval));
        Assert.Equal(SkillElement.FIRE, part.Element);
        Assert.True(part.ApplyBoost);
        Assert.True(part.ApplyMagicalDefense);
        Assert.False(part.Supported);
        Assert.Equal("unreviewed-coherent-instant-periodic-drain", part.UnsupportedReason);
        var drain = Assert.IsType<RetailNpcStatPeriodicCombat.DrainOperands>(part.Drain);
        Assert.Equal((100, 0, 150), (drain.Radius, drain.HpRate, drain.HpPercent));
        Assert.Null(drain.MpRate);
        Assert.Null(drain.MpPercent);
        Assert.Equal((0, 3600000, 250, 1), (drain.Remain1, drain.Remain2, drain.BasicLevel, drain.NoResist));
        Assert.Equal(10187262,drain.EffectId);
        Assert.Equal("Target_Only", drain.TargetType);
        Assert.Equal("e1", drain.PreEffect);
        Assert.Equal((0, 100), (drain.PreEffectProbability1, drain.PreEffectProbability2));
        Assert.Contains(drain.OrderedFields,x=>x.Key=="effect2_reserved14"&&x.Value=="0");
    }

    [SkippableFact]
    public void MissingAndExplicitZeroDrainOperandsRemainDistinct()
    {
        var (bytes, _) = SelectedSource();
        var xml = XElement.Parse(Encoding.Unicode.GetString(bytes));
        xml.Element("effect2_reserved14")!.Remove();
        xml.SetElementValue("effect2_reserved16", 0);
        xml.Element("effect1_reserved14")!.Remove();
        byte[] changed = Encoding.Unicode.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
        string hash = Convert.ToHexStringLower(SHA256.HashData(changed));
        var parsed=RetailNpcStatPeriodicCombat.Parse(changed, hash);
        var initial=Assert.IsType<RetailNpcStatPeriodicCombat.InstantDrainOperands>(parsed.InitialDrain);
        Assert.Null(initial.HpRate);
        Assert.Equal(0,initial.MpRate);
        var part = Assert.Single(parsed.Parts);
        var drain = Assert.IsType<RetailNpcStatPeriodicCombat.DrainOperands>(part.Drain);
        Assert.Null(drain.HpRate);
        Assert.Equal(0, drain.MpRate);
        Assert.False(part.Supported);
    }
}
