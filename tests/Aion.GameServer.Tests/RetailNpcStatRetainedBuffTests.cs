using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Ai;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatRetainedBuffTests
{
    [AIName("retail_retained_buff_fixture")]
    public sealed class FixtureAI(Npc owner) : NpcAI(owner)
    {
        public override bool CanThink() => false;
        protected override void HandleSpawned() => SetStateIfNot(AIState.IDLE);
    }

    [SkippableFact]
    public void ConstantHostBuffRequiresTheActualEntryAndRetiresWithOwnership()
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat: true, includeBuffs: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        using var h = BossAiHarness.For(220020000).WithAi(typeof(FixtureAI)).Build();
        var npc = h.SpawnWithAi(212961, "retail_retained_buff_fixture");
        Assert.True(RetailNpcStatCatalog.TryGet(npc, out var profile));
        Assert.False(RetailNpcStatCatalog.TryGetBuff(profile, 16522, 29, out _));
        Assert.True(RetailNpcStatCatalog.TryGetRetainedHostBuff(npc, profile, 16522, 29, out var source));
        Assert.True(RetailNpcStatTriggeredBuffs.IsConstant(source));
        Assert.False(RetailNpcStatCatalog.TryGetRetainedHostBuff(npc, profile, 16522, 30, out _));
        Assert.False(RetailNpcStatCatalog.TryGetRetainedHostBuff(npc, profile, 19125, 29, out _));
        var template = DataManager.SKILL_DATA.GetSkillTemplate(16522);
        var effect = new Effect(npc, npc, template, 29);
        effect.AddSuccessEffect(Assert.IsType<StatupEffect>(template.GetEffectTemplate(1)));
        effect.ApplyEffect();
        Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
        Assert.Equal(new(70, 0), RetailNpcStatNormalInputs.CurrentModifiers(npc, true));
        effect.EndEffect();
        Assert.Equal(new(0, 0), RetailNpcStatNormalInputs.CurrentModifiers(npc, true));
        npc.SetCreatorId(123);
        Assert.False(RetailNpcStatCatalog.TryGetRetainedHostBuff(npc, profile, 16522, 29, out _));
    }

    [SkippableFact]
    public void RetainedDragonsBlessingCorrectsAllSixElementsAndRestoresOnRemoval()
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat: true, includeBuffs: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        using var h = BossAiHarness.For(220070000).WithAi(typeof(FixtureAI)).Build();
        var npc = h.SpawnWithAi(216840, "retail_retained_buff_fixture");
        Assert.True(RetailNpcStatCatalog.TryGet(npc, out var profile));
        var entry = Enumerable.Range(0, npc.GetSkillList().Size()).Select(npc.GetSkillList().GetSkillOnPosition)
            .Single(s => s.GetSkillId() == 19125);
        Assert.False(RetailNpcStatCatalog.TryGetBuff(profile, 19125, entry.GetSkillLevel(), out _));
        Assert.True(RetailNpcStatCatalog.TryGetRetainedHostBuff(npc, profile, 19125, entry.GetSkillLevel(), out _));
        var template = DataManager.SKILL_DATA.GetSkillTemplate(19125);
        var effect = new Effect(npc, npc, template, entry.GetSkillLevel());
        effect.AddSuccessEffect(Assert.IsType<StatupEffect>(template.GetEffectTemplate(1)));
        effect.ApplyEffect();
        Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
        Assert.Equal(new(20, 0), RetailNpcStatNormalInputs.CurrentModifiers(npc, true));
        Assert.Equal(new(20, 0), RetailNpcStatNormalInputs.CurrentModifiers(npc, false));
        foreach (var element in RetailNpcStatBuffs.Elements)
        {
            var function = Assert.Single(npc.GetGameStats().GetStatsSorted(element));
            var leaf = function is StatFunctionProxy proxy ? proxy.GetProxiedFunction() : function;
            Assert.IsType<StatRateFunction>(leaf);
            Assert.Equal(10, leaf.GetValue());
        }
        effect.EndEffect();
        Assert.Equal(profile.PhysicalDefense, npc.GetGameStats().GetPDef().GetCurrent());
        foreach (var element in RetailNpcStatBuffs.Elements) Assert.Empty(npc.GetGameStats().GetStatsSorted(element));
    }
}
