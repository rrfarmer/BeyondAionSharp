using System.Reflection;
using System.Text.Json;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcVampireLethalFixtureTests
{
    // Suppress unrelated PvP/death services in this constructed player only.
    // The actual HP reduction and effect loop are still exercised.
    private sealed class FixturePlayerController : PlayerController
    {
        public bool DeathReported { get; private set; }
        public override void OnDie(Creature lastAttacker) => DeathReported = true;
    }

    [SkippableTheory]
    [InlineData(16596, 214435, 38, 100, true)]
    [InlineData(18459, 214583, 41, 130, false)]
    [InlineData(18460, 214583, 41, 100, false)]
    public void LethalPrimaryStopsDependentCurseAndSchedulesCasterHealing(int skillId, int npcId, int skillLevel, int healPercent, bool lowHealth)
    {
        string? path = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path) || string.IsNullOrEmpty(hash), "Requires pinned retail source package");
        byte[] bytes = File.ReadAllBytes(path!);
        using var manifest = JsonDocument.Parse(bytes);
        var asset = manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == skillId);
        var candidate = RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(skillId, File.ReadAllBytes(asset.GetProperty("path").GetString()!));
        using var active = RetailNpcStatCatalog.InstallForTests(bytes, hash!, Path.GetDirectoryName(path!)!);
        using var h = BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc = h.SpawnWithAi(npcId, "retail_stat_base_fixture");
        var player = RetailNpcStatPeriodicTests.Player(h, 220050000);
        var controller = new FixturePlayerController();
        controller.SetOwner(player);
        typeof(VisibleObject).GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, controller);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);
        player.GetCommonData().SetLevel(38);
        player.GetGameStats().UpdateStatsTemplate();
        PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(lowHealth ? 1 : player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);
        npc.GetGameStats().AddEffectOnly(null!, [new StatSetFunction(StatEnum.MAGICAL_ACCURACY, 10000)]);
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCapture(player, out var primary));
        Assert.NotNull(primary);
        var equipment = RetailNpcCurseEquipmentObservation.Capture(player);
        Assert.Empty(equipment.Items);
        var equipmentWitness = RetailNpcCurseEquipmentObservation.EquipmentWitness(player, equipment, RetailNpcCurseEquipmentObservation.Capture);
        int newHp = RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp, primary.HpBasis, primary.HpBasis, -35);
        int newMp = RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp, primary.MpBasis, primary.MpBasis, -35);
        var template = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        var skill = new Skill(template, npc, skillLevel, player, null!);
        var effect = new Effect(skill, player);
        var witness = RetailNpcCurseStatWitness.Create(player, effect, p => equipmentWitness(p) && primary.HasEquivalentInputs(p),
            newHp - primary.HpBasis, newMp - primary.MpBasis);
        var plan = new RetailNpcCurseResourceOwner.Plan(primary.HpBasis, primary.HpBasis, 0, primary.MpBasis, primary.MpBasis, 0,
            witness.Before, witness.After, witness.CommitStamp);
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect, candidate, plan, (_, hi) => hi);
        effect.Initialize();
        Assert.True(effect.IsInSuccessEffects(2));
        int damage = effect.GetReserveds(1).GetValue();
        Assert.True(damage >= player.GetLifeStats().GetCurrentHp());
        effect.ApplyEffect();
        Assert.True(controller.DeathReported);
        Assert.True(player.IsDead());
        Assert.Equal(0, player.GetLifeStats().GetCurrentHp());
        Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.Equal(primary.HpBasis, player.GetLifeStats().GetMaxHp());
        Assert.Equal(primary.MpBasis, player.GetLifeStats().GetMaxMp());
        effect.EndEffect();
        h.Clock.Advance(TimeSpan.FromMilliseconds(999));
        Assert.Equal(100, npc.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(), 100 + RetailNpcStatSpellDrain.HealAmount(damage, healPercent)), npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }
}



