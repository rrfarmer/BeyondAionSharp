using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Tests;

/// <summary>
/// Every selected source binding enters the actual Effect initialization, application and
/// timer owners. These are controlled headless current-owner tests, not client play or a
/// claim about the NPC skill selector, cast start, projectile travel or battle-cycle timing.
/// </summary>
[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcSkillLiveTests
{
    private static string SourcePath() => Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_TEST_DATA")
        ?? Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_DATA")
        ?? @"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";

    public static IEnumerable<object[]> SourceBindings()
    {
        if (!File.Exists(SourcePath())) return [new object[] { 0, 0, 0, 0 }];
        var profiles = SkyTempleNpcProfileCatalog.Parse(File.ReadAllBytes(SourcePath()));
        var rows = profiles.Values.OrderBy(p => p.NpcId).SelectMany(p => p.Skills.Select(s =>
            new object[] { p.NpcId, s.Ordinal, s.Id48, s.Level })).ToArray();
        if (rows.Length != 59 || rows.Select(r => (int)r[2]).Distinct().Count() != 20)
            throw new InvalidDataException("Actual Sky Temple skill tests require the exact59 bindings/20 IDs.");
        return rows;
    }

    private static Player MortalPlayer(BossAiHarness h, int level, int skill)
    {
        var common = new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR); common.SetRace(Race.ELYOS);
        common.SetGender(Gender.MALE); common.SetName("RetailSkillFixture"); common.SetNote("");
        common.SetExp(DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(level));
        var player = new Player(new PlayerAccountData(common, new PlayerAppearance()), new Account(1));
        player.SetEffectController(new PlayerEffectController(player)); player.SetKnownlist(new KnownList(player));
        player.SetFlyController(new FlyController(player)); Assert.NotNull(player.GetMoveController());
        // Front condition of16794 is deliberately reached; it must retain its real child.
        player.SetPosition(h.World.CreatePosition(SkyTempleNpcProfileCatalog.MapId, 101, 100, 200,
            skill == 16794 ? (byte)60 : (byte)0, 1));
        h.World.StoreObject(player); h.World.Spawn(player);
        // Explicit current test programs make attacks land without chosen random results.
        // Capacity and zero natural regeneration keep periodic HP observations unambiguous.
        player.GetGameStats().AddEffectOnly(null!, new IStatFunction[] {
            new StatSetFunction(StatEnum.MAXHP, 50000), new StatSetFunction(StatEnum.REGEN_HP, 0),
            new StatSetFunction(StatEnum.PHYSICAL_DEFENSE, 0), new StatSetFunction(StatEnum.MAGICAL_DEFEND, 0),
            new StatSetFunction(StatEnum.EVASION, 0), new StatSetFunction(StatEnum.PARRY, 0),
            new StatSetFunction(StatEnum.BLOCK, 0), new StatSetFunction(StatEnum.MAGICAL_RESIST, 0),
            new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL, 0),
            new StatSetFunction(StatEnum.PHYSICAL_ATTACK, 1000), new StatSetFunction(StatEnum.PHYSICAL_ACCURACY, 10000),
            new StatSetFunction(StatEnum.PHYSICAL_CRITICAL, 0) });
        // Java/current unarmed damage consumes the fixed BONUS, not the attack base.
        // Give the actual protection probe a nontrivial attack after NPC armor.
        if (skill is 16522 or 16699)
        {
            player.GetGameStats().AddEffectOnly(null!, new IStatFunction[] {
                new StatAddFunction(StatEnum.PHYSICAL_ATTACK, 1000, true) });
            Assert.Equal(1000, player.GetGameStats().GetMainHandPAttack(new HashSet<CalculationType>()).GetBonus());
        }
        player.GetLifeStats().SetCurrentHp(50000);
        Assert.Equal(level, (int)player.GetLevel()); Assert.Equal(50000, player.GetLifeStats().GetMaxHp());
        Assert.False(player.IsInvulnerable()); Assert.False(player.IsDead());
        return player;
    }

    private sealed class HpTrace(Creature actor) : ActionObserver(ObserverType.HP_CHANGED)
    {
        internal readonly List<int> Values = [];
        public override void HpChanged(int hp) => Values.Add(actor.GetLifeStats().GetCurrentHp());
    }

    [SkippableTheory]
    [MemberData(nameof(SourceBindings))]
    public void EverySourceBindingRunsActualEffectHpStatusAndTimerOwners(int npcId, int ordinal, int skillId, int level)
    {
        Skip.If(npcId == 0, "Pinned private25-profile artifact is required for all59 live Effect cases.");
        byte[] bytes = File.ReadAllBytes(SourcePath());
        using var active = SkyTempleNpcProfileCatalog.InstallForTests(bytes);
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var npc = h.SpawnWithAi(npcId, "general", 100, 100, 200);
        Assert.True(SkyTempleNpcProfileCatalog.TryGet(npc, out var profile));
        var binding = profile.Skills.Single(s => s.Ordinal == ordinal);
        Assert.Equal(skillId, binding.Id48); Assert.Equal(level, binding.Level);
        Assert.Equal(level, npc.GetSkillList().GetSkillOnPosition(ordinal).GetSkillLevel());
        var player = MortalPlayer(h, profile.Level, skillId);
        var skill = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        bool self = skillId is 16522 or 16588 or 16836 or 16677 or 16699;
        Creature target = self ? npc : player;
        bool healing = skillId is 16588 or 16836;
        if (healing) npc.GetLifeStats().SetCurrentHp(profile.MaxHp / 2);
        var hp = new HpTrace(target); target.GetObserveController().AddObserver(hp);
        int before = target.GetLifeStats().GetCurrentHp();
        var effect = new Effect(npc, target, skill, level);

        effect.Initialize();
        Assert.Equal(before, target.GetLifeStats().GetCurrentHp()); Assert.Empty(hp.Values);
        Assert.True(effect.IsInSuccessEffects(1), $"NPC{npcId}/slot{ordinal}/skill{skillId}/level{level}: primary rejected");
        Assert.Equal(skill.GetEffects()!.GetEffects().Count, effect.GetSuccessEffects().Count());
        if (skillId is 16590 or 16593 or 16595 or 16576 or 16589 or 16592 or 16594 or 16710)
        {
            Assert.True(effect.IsInSuccessEffects(2));
            Assert.Equal(effect.IsMagicalCritical(1), effect.IsMagicalCritical(2));
        }
        int reserved = healing || !self ? effect.GetReserveds(1).GetValue() : 0;
        if (healing) Assert.Equal(profile.MaxHp * (skillId == 16588 ? 25 : 20) / 100, reserved);
        if (!self) Assert.True(reserved > 0, $"NPC{npcId}/skill{skillId}: primary has no positive damage");

        effect.ApplyEffect();
        if (healing)
        {
            int expected = Math.Min(profile.MaxHp, before + reserved);
            Assert.Equal(expected, npc.GetLifeStats().GetCurrentHp());
            Assert.Single(hp.Values); Assert.Equal(expected, hp.Values[0]);
            h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
            Assert.Equal(expected, npc.GetLifeStats().GetCurrentHp()); Assert.Single(hp.Values);
        }
        else if (self)
        {
            Assert.Equal(before, npc.GetLifeStats().GetCurrentHp()); Assert.Empty(hp.Values);
            VerifySelfOwner(h, npc, player, effect, skillId, level);
        }
        else
        {
            Assert.Equal(before - reserved, player.GetLifeStats().GetCurrentHp());
            Assert.Single(hp.Values); Assert.Equal(before - reserved, hp.Values[0]);
            if (skillId is 16576 or 16589 or 16592 or 16594 or 16710)
                VerifyPeriodicOwner(h, player, effect, skillId, level, hp, before - reserved);
            else if (skillId is 16590 or 16593 or 16595)
                VerifyAbnormalOwner(h, player, effect, skillId, hp);
            else
            {
                h.Clock.Advance(TimeSpan.FromMilliseconds(100));
                Assert.Equal(before - reserved, player.GetLifeStats().GetCurrentHp()); Assert.Single(hp.Values);
            }
        }
        Assert.Empty(h.Clock.Faults);
    }

    private static void VerifyAbnormalOwner(BossAiHarness h, Player target, Effect effect, int skillId, HpTrace hp)
    {
        var state = skillId == 16595 ? AbnormalState.STUN : AbnormalState.SILENCE;
        int duration = skillId == 16595 ? 3000 : 4000;
        Assert.Equal(duration, effect.GetDuration()); Assert.True(target.GetEffectController().IsAbnormalSet(state));
        int after = target.GetLifeStats().GetCurrentHp();
        h.Clock.Advance(TimeSpan.FromMilliseconds(duration - 1));
        Assert.True(target.GetEffectController().IsAbnormalSet(state));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(target.GetEffectController().IsAbnormalSet(state)); Assert.True(effect.RetailHasEnded);
        Assert.Equal(after, target.GetLifeStats().GetCurrentHp()); Assert.Single(hp.Values);
    }

    private static void VerifyPeriodicOwner(BossAiHarness h, Player target, Effect effect, int skillId, int level,
        HpTrace hp, int afterPrimary)
    {
        bool bleed = skillId == 16594;
        int interval = bleed ? 3000 : 2000;
        int rawDuration = skillId == 16576 ? 4100 + 80 * level
            : bleed ? 6100 + 300 * level : 8100 + 200 * level;
        // Source duration uses whole intervals plus one second, before the real owner starts.
        int duration = rawDuration + 1000 - rawDuration % interval;
        Assert.Equal(duration, effect.GetDuration());
        if (bleed) Assert.True(target.GetEffectController().IsAbnormalSet(AbnormalState.BLEED));
        h.Clock.Advance(TimeSpan.FromMilliseconds(interval - 1));
        Assert.Equal(afterPrimary, target.GetLifeStats().GetCurrentHp()); Assert.Single(hp.Values);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(target.GetLifeStats().GetCurrentHp() < afterPrimary); Assert.Equal(2, hp.Values.Count);
        int elapsed = interval;
        while (elapsed + interval < duration)
        {
            int previous = target.GetLifeStats().GetCurrentHp(), notifications = hp.Values.Count;
            h.Clock.Advance(TimeSpan.FromMilliseconds(interval)); elapsed += interval;
            Assert.True(target.GetLifeStats().GetCurrentHp() < previous);
            Assert.Equal(notifications + 1, hp.Values.Count);
        }
        h.Clock.Advance(TimeSpan.FromMilliseconds(duration - elapsed));
        Assert.True(effect.RetailHasEnded);
        if (bleed) Assert.False(target.GetEffectController().IsAbnormalSet(AbnormalState.BLEED));
        Assert.Equal(1 + (duration - 1) / interval, hp.Values.Count);
        int afterExpiry = target.GetLifeStats().GetCurrentHp(), events = hp.Values.Count;
        h.Clock.Advance(TimeSpan.FromMilliseconds(interval * 2));
        Assert.Equal(afterExpiry, target.GetLifeStats().GetCurrentHp()); Assert.Equal(events, hp.Values.Count);
    }

    private static void VerifySelfOwner(BossAiHarness h, Npc npc, Player player, Effect effect, int skillId, int level)
    {
        int duration = skillId == 16677 ? 5000 + 100 * level : skillId == 16699 ? 4000 : 5000;
        int shieldedLoss = 0;
        var incoming = new HpTrace(npc); var returned = new HpTrace(player);
        npc.GetObserveController().AddObserver(incoming); player.GetObserveController().AddObserver(returned);
        Assert.Equal(duration, effect.GetDuration());
        if (skillId == 16677)
        {
            Assert.True(npc.GetEffectController().IsAbnormalSet(AbnormalState.HIDE));
            Assert.False(effect.TryGetCalculatedMagicalCritical(out _));
        }
        else
        {
            // Exercise actual protection observer -> existing normal HP commit, not a chosen
            // shield answer. Setting FIGHT first prevents this headless probe starting AI attacks.
            h.Engage(npc, player);
            int npcBefore = npc.GetLifeStats().GetCurrentHp(), playerBefore = player.GetLifeStats().GetCurrentHp();
            player.GetController().AttackTarget(npc, 0, false);
            Assert.True(npc.GetLifeStats().GetCurrentHp() < npcBefore);
            Assert.Single(incoming.Values);
            if (skillId == 16699)
            {
                Assert.True(player.GetLifeStats().GetCurrentHp() < playerBefore); Assert.Single(returned.Values);
            }
            else
            {
                Assert.Equal(playerBefore, player.GetLifeStats().GetCurrentHp());
                Assert.Empty(returned.Values);
                shieldedLoss = npcBefore - npc.GetLifeStats().GetCurrentHp();
                Assert.True(shieldedLoss > 1, $"Shield probe needs damage above armor floor; actual={shieldedLoss}");
                Assert.Equal(170, npc.GetGameStats().GetStat(StatEnum.PHYSICAL_ATTACK, 100).GetCurrent());
            }
        }
        h.Clock.Advance(TimeSpan.FromMilliseconds(duration - 1));
        Assert.False(effect.RetailHasEnded);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(effect.RetailHasEnded);
        Assert.DoesNotContain(effect, npc.GetEffectController().GetAbnormalEffects());
        Assert.False(npc.GetEffectController().IsAbnormalSet(AbnormalState.HIDE));
        if (skillId == 16522) Assert.Equal(100, npc.GetGameStats().GetStat(StatEnum.PHYSICAL_ATTACK, 100).GetCurrent());
        if (skillId == 16699)
        {
            int before = player.GetLifeStats().GetCurrentHp();
            player.GetController().AttackTarget(npc, 0, false);
            Assert.Equal(before, player.GetLifeStats().GetCurrentHp()); // Expired reflector cannot replay HP.
            Assert.Equal(2, incoming.Values.Count); Assert.Single(returned.Values);
        }
        else if (skillId == 16522)
        {
            int before = npc.GetLifeStats().GetCurrentHp();
            player.GetController().AttackTarget(npc, 0, false);
            // Actual minimum shield percentage exceeds the controlled weapon's random
            // spread; removal must produce greater loss, without supplying either roll.
            int unshieldedLoss = before - npc.GetLifeStats().GetCurrentHp();
            Assert.True(unshieldedLoss > shieldedLoss,
                $"Expired shield must absorb nothing: shielded={shieldedLoss}, expired={unshieldedLoss}");
            Assert.Equal(2, incoming.Values.Count); Assert.Empty(returned.Values);
        }
    }
}
