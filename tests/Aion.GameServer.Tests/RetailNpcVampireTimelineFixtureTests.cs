using System.Reflection;
using System.Security.Cryptography;
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
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcVampireTimelineFixtureTests
{
    private sealed class TimedSkill(SkillTemplate template, Creature caster, Creature target, int level)
        : Skill(template, caster, level, target, null!)
    {
        internal void SetWord(int word) { SetClientHitTime(word); UpdateHitTime(false); }
    }

    private sealed class FixturePlayerController : PlayerController
    {
        internal bool DeathReported;
        public override void OnDie(Creature lastAttacker) => DeathReported = true;
    }

    [SkippableTheory]
    [InlineData(16596, 214435, 38, 100, 491, false, "vampire-ordered-impact-01/motion-16596.json")]
    [InlineData(18459, 214583, 41, 130, 700, false, "vampire-ordered-impact-01/motion-18459.json")]
    [InlineData(18460, 214583, 41, 100, 700, true, "vampire-ordered-impact-01/motion-18460.json")]
    [InlineData(18459, 214603, 41, 130, 400, false, "drain-pc-appearance-01/npc-214603.json")]
    public void SourceMarkerDrivesIndependentHitCurseAndHeal(int skillId, int npcId, int level, int healPercent,
        int word, bool lethal, string timingFile)
    {
        string? manifestPath = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? timingRoot = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TIMING_TEST_ROOT");
        Skip.If(string.IsNullOrEmpty(manifestPath) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(timingRoot),
            "Requires the pinned retail package and timing sources");
        using var markerDoc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(timingRoot!, timingFile)));
        var marker = markerDoc.RootElement;
        Assert.Equal(hash, marker.GetProperty("manifest_sha256").GetString());
        Assert.Equal(npcId, marker.GetProperty("npc").GetInt32());
        Assert.Equal(skillId, marker.GetProperty("skill").GetInt32());
        var markerBytes = File.ReadAllBytes(marker.GetProperty("marker_path").GetString()!);
        Assert.Equal(marker.GetProperty("marker_sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(markerBytes)));
        Assert.Equal(word, marker.GetProperty("markers").EnumerateArray().Single().GetProperty("hit_word_ms").GetInt32());
        byte[] bytes = File.ReadAllBytes(manifestPath!);
        using var manifest = JsonDocument.Parse(bytes);
        var asset = manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == skillId);
        var candidate = RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(skillId, File.ReadAllBytes(asset.GetProperty("path").GetString()!));
        Assert.Equal(candidate.FullSourceSha256, marker.GetProperty("skill_source_sha256").GetString());
        using var active = RetailNpcStatCatalog.InstallForTests(bytes, hash!, Path.GetDirectoryName(manifestPath!)!);
        using var h = BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc = h.SpawnWithAi(npcId, "retail_stat_base_fixture");
        Assert.True(RetailNpcStatCatalog.TryGet(npc, out var profile));
        Assert.Equal(profile.SourceRecordSha256, marker.GetProperty("npc_source_sha256").GetString());
        var player = RetailNpcStatPeriodicTests.Player(h, 220050000);
        FixturePlayerController? controller = null;
        if (lethal)
        {
            controller = new(); controller.SetOwner(player);
            typeof(VisibleObject).GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, controller);
        }
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);
        player.GetCommonData().SetLevel(lethal ? 38 : 65);
        player.GetGameStats().UpdateStatsTemplate();
        PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
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
        var skill = new TimedSkill(template, npc, player, level); skill.SetWord(word);
        var effect = new Effect(skill, player);
        var witness = RetailNpcCurseStatWitness.Create(player, effect, p => equipmentWitness(p) && primary.HasEquivalentInputs(p),
            newHp - primary.HpBasis, newMp - primary.MpBasis);
        var plan = new RetailNpcCurseResourceOwner.Plan(primary.HpBasis, primary.HpBasis, 0, primary.MpBasis, primary.MpBasis, 0,
            witness.Before, witness.After, witness.CommitStamp);
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect, candidate, plan, (_, hi) => hi);
        effect.Initialize();
        Assert.True(effect.IsInSuccessEffects(2));
        int damage = effect.GetReserveds(1).GetValue();
        int initialHp = player.GetLifeStats().GetCurrentHp();
        Assert.Equal(lethal, damage >= initialHp);
        RetailNpcStatSpellDrain.AttachVampireTimelineForOfflineFixture(effect);
        effect.ApplyEffect();
        effect.ApplyEffect(); // Re-entry must not schedule a second hit, curse or heal.
        Assert.Equal(initialHp, player.GetLifeStats().GetCurrentHp());
        Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        int impact = Math.Max(word - 50, 100);
        h.Clock.Advance(TimeSpan.FromMilliseconds(impact - 1));
        Assert.Equal(initialHp, player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(Math.Max(initialHp - damage, 0), player.GetLifeStats().GetCurrentHp());
        Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        h.Clock.Advance(TimeSpan.FromMilliseconds(word - impact));
        if (lethal)
        {
            Assert.True(controller!.DeathReported); Assert.True(player.IsDead());
            Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
            Assert.Equal(primary.HpBasis, player.GetLifeStats().GetMaxHp());
        }
        else
        {
            Assert.False(player.IsDead());
            Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
            Assert.Equal(newHp, player.GetLifeStats().GetMaxHp());
            Assert.Equal(newMp, player.GetLifeStats().GetMaxMp());
        }
        effect.EndEffect();
        h.Clock.Advance(TimeSpan.FromMilliseconds(999));
        Assert.Equal(100, npc.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(), 100 + RetailNpcStatSpellDrain.HealAmount(damage, healPercent)), npc.GetLifeStats().GetCurrentHp());
        int healed = npc.GetLifeStats().GetCurrentHp();
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(healed, npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }
}
