using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcCombatReadinessTests
{
    private static SkyTempleNpcProfile[] Profiles()
    {
        string path=Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_TEST_DATA")??
            Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_DATA")??
            @"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";
        Skip.IfNot(File.Exists(path),"Pinned private selected profile artifact required");
        return SkyTempleNpcProfileCatalog.Parse(File.ReadAllBytes(path)).Values.ToArray();
    }
    private static string StaticRoot=>Path.Combine(BossAiHarness.RepoRoot(),"game-server","data","static_data");

    [SkippableFact]
    public void All25Profiles59Bindings20SkillsAndTwoOwnedChildrenValidateWithoutPublication()
    {
        using var h=BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var profiles=Profiles();bool published=SkyTempleNpcProfileCatalog.Enabled;
        Assert.Equal(25,profiles.Length);Assert.Equal(59,profiles.Sum(p=>p.Skills.Length));
        Assert.Equal(20,profiles.SelectMany(p=>p.Skills).Select(s=>s.Id48).Distinct().Count());
        SkyTempleNpcCombatReadiness.Validate(profiles,StaticRoot);
        Assert.Equal(published,SkyTempleNpcProfileCatalog.Enabled);
    }

    [SkippableFact]
    public void MissingOrDuplicateProfileCannotSatisfyTheCountGate()
    {
        using var h=BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var profiles=Profiles();
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcCombatReadiness.Validate(profiles[..24],StaticRoot));
        var duplicate=profiles[..24].Append(profiles[0]).ToArray();
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcCombatReadiness.Validate(duplicate,StaticRoot));
    }

    [SkippableFact]
    public void LoadedEffectDriftFailsEvenWhenSkillIdAndAllHeaderValuesStillMatch()
    {
        using var h=BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var profiles=Profiles();var first=DataManager.SKILL_DATA.GetSkillTemplate(17039)!.GetEffects()!.GetEffects()[0];
        int old=first.Value;
        try
        {
            first.Value=old+1;
            Assert.Throws<InvalidDataException>(()=>SkyTempleNpcCombatReadiness.Validate(profiles,StaticRoot));
        }
        finally {first.Value=old;}
    }

    [SkippableFact]
    public void LoadedOwnedChildDriftIsRejectedBeforeAHostCanPublish()
    {
        using var h=BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var profiles=Profiles();var child=DataManager.SKILL_DATA.GetSkillTemplate(8217)!.GetEffects()!.GetEffects()[0];
        int old=child.Duration1;
        try
        {
            child.Duration1=old+1;
            Assert.Throws<InvalidDataException>(()=>SkyTempleNpcCombatReadiness.Validate(profiles,StaticRoot));
        }
        finally {child.Duration1=old;}
    }

    [SkippableFact]
    public void MissingSourceAssetIsRejectedAtBootstrapInsteadOfTheFirstAttack()
    {
        using var h=BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        Assert.Throws<InvalidDataException>(()=>SkyTempleNpcCombatReadiness.Validate(Profiles(),
            Path.Combine(StaticRoot,"__missing_skytemple_source__")));
    }
}
