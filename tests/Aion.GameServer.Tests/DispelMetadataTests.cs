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
