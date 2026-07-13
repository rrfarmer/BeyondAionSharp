using System.Reflection;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Tests;

public sealed class EffectConflictTypeTests
{
    [Fact]
    public void SameSubtypeDoesNotConflictAcrossDifferentTargetSlots()
    {
        Effect buff = CreateEffect(SkillTargetSlot.BUFF, new RootEffect());
        Effect boost = CreateEffect(SkillTargetSlot.BOOST, new RootEffect());

        Assert.False(CanConflict(buff, boost));
    }

    [Theory]
    [MemberData(nameof(CrossSlotConflictEffects))]
    public void SelectedEffectTypesConflictAcrossDifferentTargetSlots(EffectTemplate first, EffectTemplate second)
    {
        Effect buff = CreateEffect(SkillTargetSlot.BUFF, first);
        Effect boost = CreateEffect(SkillTargetSlot.BOOST, second);

        Assert.True(CanConflict(buff, boost));
    }

    [Fact]
    public void DifferentConflictTypesDoNotConflictAcrossTargetSlots()
    {
        Effect shield = CreateEffect(SkillTargetSlot.BUFF, new ShieldEffect());
        Effect protector = CreateEffect(SkillTargetSlot.BOOST, new ProtectEffect());

        Assert.False(CanConflict(shield, protector));
    }

    public static TheoryData<EffectTemplate, EffectTemplate> CrossSlotConflictEffects => new()
    {
        { new ShieldEffect(), new ShieldEffect() },
        { new ProtectEffect(), new ProtectEffect() },
        { new ReflectorEffect(), new ReflectorEffect() }
    };

    private static Effect CreateEffect(SkillTargetSlot targetSlot, EffectTemplate effectTemplate)
    {
        var effects = new Effects
        {
            effects = new List<EffectTemplate> { effectTemplate }
        };
        effects.AfterUnmarshal(null!);
        var skillTemplate = new SkillTemplate
        {
            targetSlot = targetSlot,
            subType = SkillSubType.BUFF,
            effects = effects
        };
        return new Effect(null!, null!, skillTemplate, 1);
    }

    private static bool CanConflict(Effect first, Effect second)
    {
        MethodInfo method = typeof(EffectController).GetMethod("CanConflict", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(EffectController).FullName, "CanConflict");
        return (bool)method.Invoke(null, new object[] { first, second })!;
    }
}
