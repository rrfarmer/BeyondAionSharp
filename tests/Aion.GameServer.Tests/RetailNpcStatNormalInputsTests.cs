using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatNormalInputsTests
{
    private sealed class Owner : IStatOwner { }
    private sealed class OwnedCondition(IStatOwner expected) : Condition
    {
        internal int Calls;
        public override bool Validate(Aion.GameServer.SkillEngine.Model.Skill skill) => true;
        public override bool Validate(Stat2 stat, IStatFunction function)
        {
            Calls++;
            return ReferenceEquals(expected, function.GetOwner());
        }
    }
    private sealed class UnsupportedFinalRate : StatFunction
    {
        internal bool Applied;
        public UnsupportedFinalRate() : base(StatEnum.PHYSICAL_ATTACK, 25, false) { }
        public override void Apply(Stat2 stat, ISet<CalculationType> types)
        { Applied = true; stat.SetFinalRate(1.25f); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARealRateBuffAddsToTheAuthoredProfileInsteadOfMultiplyingIt(bool bonus)
    {
        using var h = BossAiHarness.For(220010000).Build();
        var npc = h.SpawnWithAi(210649, "aggressive");
        npc.GetGameStats().AddEffectOnly(new Owner(), [new StatRateFunction(StatEnum.PHYSICAL_ATTACK, 20, bonus)]);
        var current = RetailNpcStatNormalInputs.CurrentModifiers(npc, true);
        Assert.Equal(new(20, 0), current);
        // Native witness: sample100 * (profile150/100 + effect20/100) =170.
        Assert.Equal(170f, RetailNpcStatNormalInputs.CalculateBase(100, 150, current));
        Assert.Equal(180f, npc.GetGameStats().GetStat(StatEnum.PHYSICAL_ATTACK, 150).GetExactCurrent());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FlatAndPercentageChannelsRemainIndependentForBothHostBonusFlags(bool flatBonus, bool rateBonus)
    {
        using var h = BossAiHarness.For(220010000).Build();
        var npc = h.SpawnWithAi(210649, "aggressive");
        npc.GetGameStats().AddEffectOnly(new Owner(),
            [new StatAddFunction(StatEnum.PHYSICAL_ATTACK, 20, flatBonus), new StatRateFunction(StatEnum.PHYSICAL_ATTACK, 20, rateBonus)]);
        var current = RetailNpcStatNormalInputs.CurrentModifiers(npc, true);
        Assert.Equal(new(20, 20), current);
        Assert.Equal(190f, RetailNpcStatNormalInputs.CalculateBase(100, 150, current));
    }

    [Fact]
    public void MaterializingOnZeroWouldIncorrectlyScaleTheFlatBeforeTheAttack()
    {
        using var h = BossAiHarness.For(220010000).Build();
        var npc = h.SpawnWithAi(210649, "aggressive");
        npc.GetGameStats().AddEffectOnly(new Owner(),
            [new StatAddFunction(StatEnum.PHYSICAL_ATTACK, 20, false), new StatRateFunction(StatEnum.PHYSICAL_ATTACK, 20, true)]);
        Assert.Equal(24f, npc.GetGameStats().GetStat(StatEnum.PHYSICAL_ATTACK, 0).GetExactCurrent());
        Assert.Equal(20, RetailNpcStatNormalInputs.CurrentModifiers(npc, true).Flat);
    }

    [Fact]
    public void IntegerPercentagesAggregateBeforeTheirSingleBinary32Conversion()
    {
        using var h = BossAiHarness.For(220010000).Build();
        var npc = h.SpawnWithAi(210649, "aggressive");
        npc.GetGameStats().AddEffectOnly(new Owner(),
            [new StatRateFunction(StatEnum.PHYSICAL_ATTACK, 1, true), new StatRateFunction(StatEnum.PHYSICAL_ATTACK, 8, true)]);
        var current = RetailNpcStatNormalInputs.CurrentModifiers(npc, true);
        Assert.Equal(9, current.Percent);
        // Independent SSE witness: 65535 * f32(f32(9/100) + f32(1*0.01f)).
        // Separately adding 0.01f and0.08f yields6553.49951171875 instead.
        Assert.Equal(6553.5f, RetailNpcStatNormalInputs.CalculateBase(65535, 1, current));
    }

    [Fact]
    public void MagicalAndPhysicalProgramsRemainSeparateAndSubtractionIsAnIntegerFlat()
    {
        using var h = BossAiHarness.For(220010000).Build();
        var npc = h.SpawnWithAi(210649, "aggressive");
        npc.GetGameStats().AddEffectOnly(new Owner(),
            [new StatRateFunction(StatEnum.MAGICAL_ATTACK, 20, true), new StatAddFunction(StatEnum.MAGICAL_ATTACK, 35, true),
                new StatSubFunction { Stat = StatEnum.MAGICAL_ATTACK, Value = 5, _bonus = false }]);
        Assert.Equal(new(0, 0), RetailNpcStatNormalInputs.CurrentModifiers(npc, true));
        var current = RetailNpcStatNormalInputs.CurrentModifiers(npc, false);
        Assert.Equal(new(20, 30), current);
        Assert.Equal(200f, RetailNpcStatNormalInputs.CalculateBase(100, 150, current));
    }

    [Fact]
    public void ActualProxyOwnerConditionsAreEvaluatedOnceAndRejectedContributionsStayAbsent()
    {
        using var h = BossAiHarness.For(220010000).Build();
        var npc = h.SpawnWithAi(210649, "aggressive");
        var active = new Owner(); var unrelated = new Owner();
        var yes = new OwnedCondition(active); var no = new OwnedCondition(unrelated);
        npc.GetGameStats().AddEffectOnly(active,
            [new StatRateFunction(StatEnum.PHYSICAL_ATTACK, 20, true).WithConditions(new Conditions { conditions = [yes] }),
                new StatAddFunction(StatEnum.PHYSICAL_ATTACK, 999, true).WithConditions(new Conditions { conditions = [no] })]);
        Assert.Equal(new(20, 0), RetailNpcStatNormalInputs.CurrentModifiers(npc, true));
        Assert.Equal(1, yes.Calls); Assert.Equal(1, no.Calls);
    }

    [Fact]
    public void DwordAggregateOverflowAndExactNoOpAreNotFloatingPointStatEvaluation()
    {
        using var h = BossAiHarness.For(220010000).Build();
        var npc = h.SpawnWithAi(210649, "aggressive");
        npc.GetGameStats().AddEffectOnly(new Owner(),
            [new StatRateFunction(StatEnum.PHYSICAL_ATTACK, int.MaxValue, true), new StatRateFunction(StatEnum.PHYSICAL_ATTACK, 1, true),
                new StatAddFunction(StatEnum.PHYSICAL_ATTACK, int.MaxValue, true), new StatAddFunction(StatEnum.PHYSICAL_ATTACK, 1, true),
                new StatFunction(StatEnum.PHYSICAL_ATTACK, 999, true)]);
        Assert.Equal(new(int.MinValue, int.MinValue), RetailNpcStatNormalInputs.CurrentModifiers(npc, true));
    }

    [Fact]
    public void UnsupportedOperatorsAndNestedProxyShapesCannotBecomeGenericGetterFallbacks()
    {
        using var h = BossAiHarness.For(220010000).Build();
        var npc = h.SpawnWithAi(210649, "aggressive");
        var unknown = new UnsupportedFinalRate();
        npc.GetGameStats().AddEffectOnly(new Owner(), [unknown]);
        Assert.Throws<InvalidOperationException>(() => RetailNpcStatNormalInputs.CurrentModifiers(npc, true));
        Assert.False(unknown.Applied);
        var other = h.SpawnWithAi(210366, "aggressive");
        other.GetGameStats().AddEffectOnly(new Owner(),
            [new StatFunctionProxy(new Owner(), new StatRateFunction(StatEnum.PHYSICAL_ATTACK, 20, true))]);
        Assert.Throws<InvalidOperationException>(() => RetailNpcStatNormalInputs.CurrentModifiers(other, true));
    }

}
