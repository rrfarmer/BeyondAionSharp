using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World.Knownlist;

using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcAbnormalEffectsTests
{
    private static byte[] Source()
    {
        string path = Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_TEST_DATA")
            ?? Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_DATA")
            ?? @"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";
        Skip.IfNot(File.Exists(path), "Pinned private25-profile artifact is required.");
        return File.ReadAllBytes(path);
    }

    private static (Effect Effect, EffectTemplate Template, Npc Npc, Player Player) Secondary(
        BossAiHarness h, int id, bool critical = false)
    {
        var profile = SkyTempleNpcProfileCatalog.GetPreparedProfiles().OrderBy(p => p.NpcId)
            .First(p => p.Skills.Any(s => s.Id48 == id));
        var binding = profile.Skills.First(s => s.Id48 == id);
        var npc = h.SpawnWithAi(profile.NpcId, "general", 100, 100, 200);
        // Initialize the actual current level before stats/life constructors cache templates.
        // Harness.SpawnPlayer intentionally defaults to level0 and is invulnerable.
        var common = new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR); common.SetRace(Race.ELYOS);
        common.SetGender(Gender.MALE); common.SetName("RetailStatusFixture"); common.SetNote("");
        common.SetExp(DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(profile.Level));
        var target = new Player(new PlayerAccountData(common, new PlayerAppearance()), new Account(1));
        target.SetKnownlist(new KnownList(target)); target.SetEffectController(new PlayerEffectController(target));
        target.SetFlyController(new Aion.GameServer.Controllers.FlyController(target));
        Assert.NotNull(target.GetMoveController());
        target.SetPosition(h.World.CreatePosition(SkyTempleNpcProfileCatalog.MapId, 101, 100, 200, 0, 1));
        h.World.StoreObject(target); h.World.Spawn(target);
        target.GetLifeStats().SetCurrentHp(target.GetLifeStats().GetMaxHp());
        // Equal levels and zero magic resistance make the actual native magic threshold0.
        // These are registered current test programs, not a supplied successful RNG answer.
        target.GetGameStats().AddEffectOnly(null!, new IStatFunction[] {
            new StatSetFunction(StatEnum.MAGICAL_RESIST, 0) });
        Assert.Equal(profile.Level, (int)target.GetLevel());
        Assert.False(target.IsInvulnerable()); Assert.False(target.IsDead());
        Assert.Equal(0, target.GetGameStats().GetAbnormalResistance().GetCurrent());
        Assert.Equal(0, target.GetGameStats().GetResistance(StatEnum.SILENCE_RESISTANCE).GetCurrent());
        Assert.Equal(0, target.GetGameStats().GetResistance(StatEnum.STUN_RESISTANCE).GetCurrent());
        Assert.Equal(0, target.GetGameStats().GetResistance(StatEnum.BLEED_RESISTANCE).GetCurrent());
        var skill = DataManager.SKILL_DATA.GetSkillTemplate(id);
        var effect = new Effect(npc, target, skill, binding.Level);
        effect.AddSuccessEffect(skill.GetEffectTemplate(1)!);
        effect.SetCalculatedMagicalCritical(1, critical);
        return (effect, skill.GetEffectTemplate(2)!, npc, target);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1000, 0, 0, 1000)]
    [InlineData(1001, 0, 0, 1000)]
    [InlineData(999, 0, 0, 999)]
    [InlineData(1000, 0, 5, 300)]
    [InlineData(1001, 0, 5, 300)]
    [InlineData(999, 0, 5, 299)]
    [InlineData(20, 10, 5, 3)]
    [InlineData(10, 20, 5, 0)]
    [InlineData(65535, 0, 5, 1000)]
    [InlineData(50, 45, 4, 5)]
    public void NativeStatusThresholdUsesSeparatePointThreePoseFactorAndTruncation(
        int resistance, int penetration, byte pose, int expected)
        => Assert.Equal(expected, SkyTempleNpcAbnormalEffects.StatusThreshold(resistance, penetration, pose));

    [Theory]
    [InlineData(-1, 0, 0)] [InlineData(65536, 0, 0)]
    [InlineData(0, -1, 0)] [InlineData(0, 65536, 0)] [InlineData(0, 0, 16)]
    public void UnsupportedCurrentStatusStorageDoesNotBecomeAChosenZero(int resistance, int penetration, byte pose)
    {
        if (pose > 15)
            Assert.Throws<ArgumentOutOfRangeException>(() => SkyTempleNpcAbnormalEffects.StatusThreshold(resistance, penetration, pose));
        else
            Assert.Throws<InvalidOperationException>(() => SkyTempleNpcAbnormalEffects.StatusThreshold(resistance, penetration, pose));
    }

    [SkippableTheory]
    [InlineData(16590, false)] [InlineData(16590, true)]
    [InlineData(16593, false)] [InlineData(16593, true)]
    [InlineData(16595, false)] [InlineData(16595, true)]
    public void ActualSelectedSecondaryReusesBothCriticalOutcomesAndKeepsPrimary(int id, bool critical)
    {
        using var active = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var x = Secondary(h, id, critical);
        Assert.True(SkyTempleNpcAbnormalEffects.TryCalculate(x.Effect, x.Template, null, out bool success));
        Assert.True(success);
        Assert.True(x.Effect.IsInSuccessEffects(1)); Assert.True(x.Effect.IsInSuccessEffects(2));
        Assert.Equal(critical, x.Effect.IsMagicalCritical(1));
        Assert.Equal(critical, x.Effect.IsMagicalCritical(2));
        Assert.True(x.Effect.TryGetCalculatedMagicalCritical(out bool cached)); Assert.Equal(critical, cached);
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void MissingActualPrecedingCacheRejectsInsteadOfRollingAnotherCritical()
    {
        using var active = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var x = Secondary(h, 16590); x.Effect.ResetMagicalCritical();
        Assert.Throws<InvalidOperationException>(() => SkyTempleNpcAbnormalEffects.TryCalculate(x.Effect, x.Template, null, out _));
        Assert.True(x.Effect.IsInSuccessEffects(1)); Assert.False(x.Effect.IsInSuccessEffects(2));
        Assert.False(x.Effect.TryGetCalculatedMagicalCritical(out _));
    }

    private sealed class ForcedResist : Aion.GameServer.Controllers.Observer.AttackCalcObserver
    {
        internal int Calls;
        public override bool CheckStatus(AttackStatus status) { Calls++; return status == AttackStatus.RESIST; }
    }

    [SkippableFact]
    public void CurrentMagicResistanceObserverSkipsB0AndPreservesEarlierDamageAndCache()
    {
        using var active = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var x = Secondary(h, 16590, true); var forced = new ForcedResist();
        var b0 = new CountingSet(StatEnum.SILENCE_RESISTANCE, 1000);
        x.Player.GetGameStats().AddEffectOnly(null!, new IStatFunction[] { b0 });
        x.Player.GetObserveController().AddAttackCalcObserver(forced);
        Assert.True(SkyTempleNpcAbnormalEffects.TryCalculate(x.Effect, x.Template, null, out bool success));
        Assert.False(success); Assert.Equal(1, forced.Calls); Assert.Equal(0, b0.Reads);
        Assert.True(x.Effect.IsInSuccessEffects(1)); Assert.False(x.Effect.IsInSuccessEffects(2));
        Assert.True(x.Effect.IsMagicalCritical(2));
    }

    private sealed class CountingSet(StatEnum name, int value) : StatSetFunction(name, value)
    {
        internal int Reads;
        public override void Apply(Stat2 stat, ISet<CalculationType> types) { Reads++; base.Apply(stat, types); }
    }

    [SkippableFact]
    public void CurrentStatusFunctionsAreEvaluatedOnceAndResistanceCannotDeletePrimaryDamage()
    {
        using var active = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var x = Secondary(h, 16595, true);
        var specific = new CountingSet(StatEnum.STUN_RESISTANCE, 900);
        var all = new CountingSet(StatEnum.ABNORMAL_RESISTANCE_ALL, 100);
        var penetration = new CountingSet(StatEnum.STUN_RESISTANCE_PENETRATION, 0);
        x.Player.GetGameStats().AddEffectOnly(null!, new IStatFunction[] { specific, all });
        x.Npc.GetGameStats().AddEffectOnly(null!, new IStatFunction[] { penetration });
        Assert.True(SkyTempleNpcAbnormalEffects.TryCalculate(x.Effect, x.Template, null, out bool success));
        Assert.False(success); Assert.Equal(1, specific.Reads); Assert.Equal(1, all.Reads); Assert.Equal(1, penetration.Reads);
        Assert.True(x.Effect.IsInSuccessEffects(1)); Assert.False(x.Effect.IsInSuccessEffects(2));
        Assert.True(x.Effect.IsMagicalCritical(2));
    }

    [SkippableFact]
    public void BleedB0ExcludesAlteredAllResistanceAndNoResistSkipsCurrentChildren()
    {
        using var active = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var x = Secondary(h, 16594);
        var all = new CountingSet(StatEnum.ABNORMAL_RESISTANCE_ALL, 1000);
        var specific = new CountingSet(StatEnum.BLEED_RESISTANCE, 0);
        x.Player.GetGameStats().AddEffectOnly(null!, new IStatFunction[] { all, specific });
        Assert.True(SkyTempleNpcAbnormalEffects.CheckStatus(x.Effect, 1, StatEnum.BLEED_RESISTANCE,
            StatEnum.BLEED_RESISTANCE_PENETRATION, false));
        Assert.Equal(0, all.Reads); Assert.Equal(1, specific.Reads);
        Assert.True(SkyTempleNpcAbnormalEffects.CheckStatus(x.Effect, 1, StatEnum.BLEED_RESISTANCE,
            StatEnum.BLEED_RESISTANCE_PENETRATION, true));
        Assert.Equal(0, all.Reads); Assert.Equal(1, specific.Reads);
        Assert.Throws<ArgumentException>(() => SkyTempleNpcAbnormalEffects.CheckStatus(x.Effect, 8,
            StatEnum.BLEED_RESISTANCE, StatEnum.BLEED_RESISTANCE_PENETRATION, false));
    }

    [SkippableTheory]
    [InlineData(16590, 4000, AbnormalState.SILENCE)]
    [InlineData(16593, 4000, AbnormalState.SILENCE)]
    [InlineData(16595, 3000, AbnormalState.STUN)]
    public void ActualEffectControllerStartsAndExpiresSelectedStateAtSourceDuration(int id, int duration, AbnormalState state)
    {
        using var active = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var x = Secondary(h, id);
        Assert.True(SkyTempleNpcAbnormalEffects.TryCalculate(x.Effect, x.Template, null, out bool success)); Assert.True(success);
        x.Template.ApplyEffect(x.Effect); // Actual controller owns StartEffect and its end task.
        Assert.Equal(duration, x.Effect.GetDuration());
        Assert.True(x.Player.GetEffectController().IsInAnyAbnormalState(state));
        h.Clock.Advance(TimeSpan.FromMilliseconds(duration - 1));
        Assert.True(x.Player.GetEffectController().IsInAnyAbnormalState(state));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(x.Player.GetEffectController().IsInAnyAbnormalState(state));
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void HideUsesActualNpcOwnerWithoutMagicOrCriticalRollAndExpiresLevelScaledDuration()
    {
        using var active = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var profile = SkyTempleNpcProfileCatalog.GetPreparedProfiles().First(p => p.Skills.Any(s => s.Id48 == 16677));
        var binding = profile.Skills.First(s => s.Id48 == 16677);
        var npc = h.SpawnWithAi(profile.NpcId, "general", 100, 100, 200);
        var skill = DataManager.SKILL_DATA.GetSkillTemplate(16677); var template = skill.GetEffectTemplate(1)!;
        var effect = new Effect(npc, npc, skill, binding.Level);
        Assert.True(SkyTempleNpcAbnormalEffects.TryCalculate(effect, template, null, out bool success)); Assert.True(success);
        Assert.False(effect.TryGetCalculatedMagicalCritical(out _));
        template.ApplyEffect(effect);
        int duration = 5000 + 100 * binding.Level;
        Assert.Equal(duration, effect.GetDuration()); Assert.True(npc.GetEffectController().IsInAnyAbnormalState(AbnormalState.HIDE));
        h.Clock.Advance(TimeSpan.FromMilliseconds(duration));
        Assert.False(npc.GetEffectController().IsInAnyAbnormalState(AbnormalState.HIDE)); Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void UnboundTemplateCannotReuseASourceSkillId()
    {
        using var active = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var x = Secondary(h, 16590);
        var impostor = new SilenceEffect { Position = 2, Element = SkillElement.WATER };
        Assert.Throws<InvalidOperationException>(() => SkyTempleNpcAbnormalEffects.TryCalculate(x.Effect, impostor, null, out _));
        Assert.False(x.Effect.IsInSuccessEffects(2));
    }
}
