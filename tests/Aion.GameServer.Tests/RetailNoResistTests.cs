using System.Reflection;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Tests;

public sealed class RetailNoResistTests
{
    [Theory]
    [MemberData(nameof(NormalizedEffectTypes))]
    public void RetailBeneficialEffectTypesAreAlwaysNoResist(EffectTemplate effectTemplate)
    {
        Normalize(effectTemplate);

        Assert.True(effectTemplate.IsNoResist());
    }

    [Fact]
    public void UnlistedEffectTypeRetainsXmlNoResistValue()
    {
        var resistibleRoot = new RootEffect();
        var explicitNoResistRoot = new RootEffect { NoResist = true };

        Normalize(resistibleRoot);
        Normalize(explicitNoResistRoot);

        Assert.False(resistibleRoot.IsNoResist());
        Assert.True(explicitNoResistRoot.IsNoResist());
    }

    [Fact]
    public void ToggleActivationNoLongerMakesUnlistedEffectsUnresistable()
    {
        var root = new RootEffect();
        Effect effect = CreateRuntimeEffect(root, ActivationAttribute.TOGGLE);

        Assert.True(IsDodgedOrResisted(root, effect, StatEnum.ROOT_RESISTANCE));
    }

    [Fact]
    public void CannotMissSkillAttackReportsNoResist()
    {
        var effect = new SkillAttackInstantEffect { cannotmiss = true };

        Assert.True(effect.IsNoResist());
    }

    public static TheoryData<EffectTemplate> NormalizedEffectTypes => new()
    {
        new HealEffect(),
        new ShieldEffect(),
        new MPShieldEffect(),
        new ProtectEffect(),
        new ReflectorEffect(),
        new XPBoostEffect()
    };

    private static void Normalize(EffectTemplate effectTemplate)
    {
        var effects = new Effects
        {
            effects = new List<EffectTemplate> { effectTemplate }
        };
        effects.AfterUnmarshal(null!);
    }

    private static Effect CreateRuntimeEffect(EffectTemplate effectTemplate, ActivationAttribute activation)
    {
        var effects = new Effects
        {
            effects = new List<EffectTemplate> { effectTemplate }
        };
        effects.AfterUnmarshal(null!);
        return new Effect(null!, null!, new SkillTemplate
        {
            activationAttribute = activation,
            effects = effects
        }, 1);
    }

    private static bool IsDodgedOrResisted(EffectTemplate effectTemplate, Effect effect, StatEnum stat)
    {
        MethodInfo method = typeof(EffectTemplate).GetMethod("IsDodgedOrResisted", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(EffectTemplate).FullName, "IsDodgedOrResisted");
        return (bool)method.Invoke(effectTemplate, new object?[] { effect, stat })!;
    }
}
