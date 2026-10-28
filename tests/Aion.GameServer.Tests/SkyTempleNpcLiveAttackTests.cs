using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Tests;

/// <summary>Actual controller/HP integration, with explicit headless fixture stats.
/// The ordinary boss harness player is invulnerable and cannot prove damage delivery.
/// These players are mortal. This is not client play or an observation of native execution.</summary>
[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcLiveAttackTests
{
    private static byte[] Source()
    {
        string? path = Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_TEST_DATA")
            ?? Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_DATA");
        Skip.If(string.IsNullOrWhiteSpace(path), "The pinned private25-profile artifact is required.");
        return File.ReadAllBytes(path!);
    }

    private static Player MortalPlayer(BossAiHarness h)
    {
        var common = new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR); common.SetRace(Race.ELYOS);
        common.SetGender(Gender.MALE); common.SetName("RetailHpFixture" + common.GetPlayerObjId()); common.SetNote("");
        common.SetExp(89979199); // Explicit level38, matching the copied normal-play character.
        var player = new Player(new PlayerAccountData(common, new PlayerAppearance()), new Account(1));
        player.SetKnownlist(new KnownList(player));
        player.SetEffectController(new PlayerEffectController(player));
        player.SetFlyController(new Aion.GameServer.Controllers.FlyController(player));
        PlayerStatFunctions.AddPredefinedStatFunctions(player); // Production PlayerService setup.
        player.SetPosition(h.World.CreatePosition(SkyTempleNpcProfileCatalog.MapId, 101, 100, 200, 0, 1));
        h.World.StoreObject(player); h.World.Spawn(player);
        // Explicit controlled-test capacity and accuracy, not an item/quest or production change.
        player.GetGameStats().AddEffectOnly(null!, new IStatFunction[] {
            new StatSetFunction(StatEnum.MAXHP, 50000),
            new StatSetFunction(StatEnum.EVASION, 0), new StatSetFunction(StatEnum.PARRY, 0),
            new StatSetFunction(StatEnum.PHYSICAL_ACCURACY, 10000),
            new StatSetFunction(StatEnum.MAGICAL_ACCURACY, 10000) });
        player.GetLifeStats().SetCurrentHp(50000);
        Assert.False(player.IsInvulnerable()); Assert.False(player.IsDead()); Assert.Equal(38, (int)player.GetLevel());
        return player;
    }

    private sealed class CalculationCapture : AttackCalcObserver
    {
        internal int Calls, Damage;
        internal AttackStatus Status;
        public override void CheckShield(List<AttackResult> results, Effect effect, Creature attacker)
        {
            Calls++; Damage = results.Sum(r => r.GetDamage());
            Status = results[0].GetAttackStatus();
        }
    }
    private sealed class HpCapture() : ActionObserver(ObserverType.HP_CHANGED)
    {
        internal int Calls;
        public override void HpChanged(int hp) => Calls++;
    }

    [SkippableFact]
    public void EveryActualNpcNormalAttackUsesOneCalculationAndOneActualHpLoss()
    {
        byte[] bytes = Source(); var profiles = SkyTempleNpcProfileCatalog.Parse(bytes);
        using var enabled = SkyTempleNpcProfileCatalog.InstallForTests(bytes);
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var player = MortalPlayer(h); var calc = new CalculationCapture(); var hp = new HpCapture();
        player.GetObserveController().AddAttackCalcObserver(calc);
        player.GetObserveController().AddObserver(hp);
        foreach (var profile in profiles.Values.OrderBy(p => p.NpcId))
        {
            var npc = h.SpawnWithAi(profile.NpcId, "general", 100, 100, 200);
            h.Engage(npc, player);
            int before = player.GetLifeStats().GetCurrentHp(), calculations = calc.Calls, notifications = hp.Calls;
            npc.GetController().AttackTarget(player, 0, false);
            Assert.Equal(calculations + 1, calc.Calls);
            Assert.True(calc.Damage is > 0, $"NPC{profile.NpcId}: no landed normal attack ({calc.Status})");
            float maximum = profile.MaxDamage * (profile.Attacks[0].Type == "physical" && calc.Status.IsCritical() ? 1.5f : 1f);
            Assert.True(calc.Damage <= maximum, $"NPC{profile.NpcId}: damage exceeds source max/critical coefficient without a buff");
            Assert.Equal(before - calc.Damage, player.GetLifeStats().GetCurrentHp());
            Assert.Equal(notifications + 1, hp.Calls);
        }
        Assert.Equal(25, calc.Calls); Assert.Equal(25, hp.Calls); Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void OrdinaryPlayerNormalAttacksReachAll25SelectedNpcHpOwnersOnce()
    {
        byte[] bytes = Source(); var profiles = SkyTempleNpcProfileCatalog.Parse(bytes);
        using var enabled = SkyTempleNpcProfileCatalog.InstallForTests(bytes);
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        foreach (var profile in profiles.Values.OrderBy(p => p.NpcId))
        {
            var player = MortalPlayer(h);
            var npc = h.SpawnWithAi(profile.NpcId, "general", 100, 100, 200);
            h.Engage(npc, player);
            var calc = new CalculationCapture(); var hp = new HpCapture();
            npc.GetObserveController().AddAttackCalcObserver(calc); npc.GetObserveController().AddObserver(hp);
            Assert.True(Aion.GameServer.Restrictions.PlayerRestrictions.CanAttack(player, npc), $"NPC{profile.NpcId}: actual player restrictions reject target");
            int before = npc.GetLifeStats().GetCurrentHp();
            player.GetController().AttackTarget(npc, 0, false);
            Assert.Equal(1, calc.Calls); Assert.True(calc.Damage > 0, $"NPC{profile.NpcId}: no ordinary player HP loss ({calc.Status})");
            Assert.Equal(before - calc.Damage, npc.GetLifeStats().GetCurrentHp()); Assert.Equal(1, hp.Calls);
        }
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void SelectedAnimationDefersActualHpUntilImpactAndDoesNotReplayIt()
    {
        using var enabled = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var player = MortalPlayer(h); var npc = h.SpawnWithAi(212761, "general", 100, 100, 200);
        h.Engage(npc, player);
        var calc = new CalculationCapture(); var hp = new HpCapture();
        player.GetObserveController().AddAttackCalcObserver(calc); player.GetObserveController().AddObserver(hp);
        Assert.True(SkyTempleNpcCombat.TryGetNormalTiming(npc, player, out var timing));
        Assert.True(timing.ImpactDelayMs > 0); Assert.True(timing.CadenceMs >= 2300);
        int before = player.GetLifeStats().GetCurrentHp();
        npc.GetController().AttackTarget(player, timing.ImpactDelayMs, false);
        Assert.Equal(1, calc.Calls); Assert.Equal(before, player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(timing.ImpactDelayMs - 1));
        Assert.Equal(before, player.GetLifeStats().GetCurrentHp()); Assert.Equal(0, hp.Calls);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(before - calc.Damage, player.GetLifeStats().GetCurrentHp()); Assert.Equal(1, hp.Calls);
        h.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(before - calc.Damage, player.GetLifeStats().GetCurrentHp()); Assert.Equal(1, hp.Calls);
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void DeferredAttackCannotHitTheSameTargetObjectAfterItsOldLifeWasRetired()
    {
        using var enabled = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var player = MortalPlayer(h); var npc = h.SpawnWithAi(212761, "general", 100, 100, 200);
        h.Engage(npc, player);
        int before = player.GetLifeStats().GetCurrentHp();
        npc.GetController().AttackTarget(player, 800, false);
        long old = player.GetController().RetailLifeEpoch;
        player.GetController().OnDespawn();
        Assert.NotEqual(old, player.GetController().RetailLifeEpoch);
        Assert.True(player.IsSpawned()); // Deliberately isolate epoch retirement from the position flag.
        // Controller retirement is sufficient: same object/position remain, so checking
        // object identity or taking an epoch snapshot only inside the callback is unsafe.
        h.Clock.Advance(TimeSpan.FromMilliseconds(900));
        Assert.Equal(before, player.GetLifeStats().GetCurrentHp()); Assert.Empty(h.Clock.Faults);
    }
}
