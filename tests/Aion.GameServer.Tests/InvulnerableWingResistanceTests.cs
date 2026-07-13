using System.Reflection;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Tests;

public sealed class InvulnerableWingResistanceTests
{
    [Theory]
    [MemberData(nameof(WingBlockedEffects))]
    public void InvulnerableWingAlwaysResistsFlightDisruption(EffectTemplate effectTemplate, StatEnum? resistanceStat)
    {
        var creature = new TestCreature();
        var controller = new EffectController(creature);
        creature.SetEffectController(controller);
        controller.SetAbnormal(AbnormalState.INVULNERABLE_WING);
        Effect effect = CreateRuntimeEffect(creature, effectTemplate);

        Assert.True(IsDodgedOrResisted(effectTemplate, effect, resistanceStat));
    }

    public static TheoryData<EffectTemplate, StatEnum?> WingBlockedEffects => new()
    {
        { new FallEffect(), null },
        { new NoFlyEffect(), StatEnum.NOFLY_RESISTANCE }
    };

    private static Effect CreateRuntimeEffect(Creature creature, EffectTemplate effectTemplate)
    {
        var effects = new Effects
        {
            effects = new List<EffectTemplate> { effectTemplate }
        };
        effects.AfterUnmarshal(null!);
        return new Effect(creature, creature, new SkillTemplate { effects = effects }, 1);
    }

    private static bool IsDodgedOrResisted(EffectTemplate effectTemplate, Effect effect, StatEnum? stat)
    {
        MethodInfo method = effectTemplate.GetType().GetMethod("IsDodgedOrResisted", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(effectTemplate.GetType().FullName, "IsDodgedOrResisted");
        return (bool)method.Invoke(effectTemplate, new object?[] { effect, stat })!;
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
