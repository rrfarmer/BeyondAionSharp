using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Tests;

public sealed class DispelMetadataTests
{
    [Fact]
    public void EffectPowerComesFromRequiredDispelCount()
    {
        var template = new SkillTemplate
        {
            name = "Protective Shield",
            group = "KN_INVINSIBLEPROTECT",
            activationAttribute = ActivationAttribute.MAINTAIN,
            reqDispelCount = 37
        };

        var effect = new Effect(null!, null!, template, 1);

        Assert.Equal(37, effect.GetPower());
    }

    [Fact]
    public void TargetSlotLevelDoesNotPreventDispellingMetadataQualifiedBuff()
    {
        var owner = new TestCreature();
        var controller = new TestEffectController(owner);
        var protectedEffect = new Effect(owner, owner, new SkillTemplate
        {
            skillId = 1,
            stack = "protected-effect",
            targetSlot = SkillTargetSlot.BUFF,
            targetSlotLevel = 2,
            dispelCategory = DispelCategoryType.BUFF,
            reqDispelLevel = 1,
            reqDispelCount = 10,
            activationAttribute = ActivationAttribute.ACTIVE
        }, 1);
        var dispelEffect = new Effect(owner, owner, new SkillTemplate
        {
            skillId = 2,
            stack = "dispel-effect",
            reqDispelCount = 10,
            activationAttribute = ActivationAttribute.ACTIVE
        }, 1);
        controller.PutForTest(protectedEffect);

        int removed = controller.CalculateBuffsOrEffectorDebuffsToRemove(
            dispelEffect,
            count: 1,
            dispelLevel: 1,
            power: 10);

        Assert.Equal(1, removed);
        Assert.Same(dispelEffect, protectedEffect.GetDesignatedDispelEffect());
    }

    [Fact]
    public void CounterattackDispelCountIsSpentOnlyAfterSuccessfulPowerRemoval()
    {
        var owner = new TestCreature();
        var controller = new TestEffectController(owner);
        Effect strongEffect = CreateBuff(owner, 1, "strong-effect", 20);
        Effect weakEffect = CreateBuff(owner, 2, "weak-effect", 10);
        Effect dispelEffect = CreateBuff(owner, 3, "dispel-effect", 10);
        controller.PutForTest(strongEffect);
        controller.PutForTest(weakEffect);

        int removed = controller.CalculateBuffsOrEffectorDebuffsToRemove(
            dispelEffect,
            count: 1,
            dispelLevel: 1,
            power: 10);

        Assert.Equal(1, removed);
        Assert.Null(strongEffect.GetDesignatedDispelEffect());
        Assert.Same(dispelEffect, weakEffect.GetDesignatedDispelEffect());
    }

    [Fact]
    public void CategoryDispelCountIsNotSpentWhenPowerRemovalFails()
    {
        var owner = new TestCreature();
        var controller = new TestEffectController(owner);
        Effect firstEffect = CreateBuff(owner, 1, "first-effect", 20);
        Effect secondEffect = CreateBuff(owner, 2, "second-effect", 20);
        controller.PutForTest(firstEffect);
        controller.PutForTest(secondEffect);

        controller.RemoveEffectByDispelCat(
            DispelCategoryType.BUFF,
            SkillTargetSlot.BUFF,
            count: 1,
            dispelLevel: 1,
            power: 10);

        Assert.Equal(10, firstEffect.GetPower());
        Assert.Equal(10, secondEffect.GetPower());
    }

    private static Effect CreateBuff(TestCreature owner, int skillId, string stack, int power)
    {
        return new Effect(owner, owner, new SkillTemplate
        {
            skillId = skillId,
            stack = stack,
            targetSlot = SkillTargetSlot.BUFF,
            dispelCategory = DispelCategoryType.BUFF,
            reqDispelLevel = 1,
            reqDispelCount = power,
            activationAttribute = ActivationAttribute.ACTIVE
        }, 1);
    }

    private sealed class TestEffectController : EffectController
    {
        public TestEffectController(Creature owner) : base(owner)
        {
        }

        public void PutForTest(Effect effect) => Put(effect);
    }

    private sealed class TestCreature : Creature
    {
        public TestCreature() : base(0, null!, null!, new NpcTemplate(), null!, false)
        {
        }

        public override sbyte GetLevel() => 1;

        public override Race GetRace() => Race.ELYOS;

        public override CreatureGameStats GetGameStats() => null!;

        public override bool IsPvpTarget(Creature creature) => false;
    }
}
