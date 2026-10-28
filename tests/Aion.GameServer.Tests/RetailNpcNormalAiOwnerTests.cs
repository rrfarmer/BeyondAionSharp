using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcNormalAiOwnerTests
{
    [AIName("retail_normal_ai_owner_fixture")]
    public sealed class DamageOwner(Npc owner) : OneDmgAI(owner)
    {
        internal int Calls;
        public override bool CanThink() => false;
        public override float ModifyOwnerDamage(float damage, Creature target, Effect effect)
        { Calls++; return base.ModifyOwnerDamage(damage, target, effect); }
    }

    [AIName("retail_normal_superseded_numeric_fixture")]
    public sealed class SupersededNumericOwner(Npc owner) : NpcAI(owner)
    {
        internal int Calls;
        public override bool CanThink() => false;
        public override float ModifyOwnerDamage(float damage, Creature target, Effect effect)
        { Calls++; return damage * 4.5f; }
    }

    private sealed class Protection : AttackCalcObserver
    {
        internal int Calls;
        internal readonly List<int> Received = [];
        public override void CheckShield(List<AttackResult> attacks, Effect effect, Creature attacker)
        {
            Calls++;
            foreach (var attack in attacks)
            {
                Received.Add(attack.GetDamage());
                attack.SetDamage(attack.GetDamage() - 1);
            }
        }
    }

    [SkippableTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OneDamageOwnerRunsOncePerHandBeforeProtection(bool elemental, bool dual)
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        var member = RetailNpcStatCatalog.Members.First(m => m.MapId != 0
            && m.Profile.MinDamage >= 1000 && m.Profile.Attacks.Length == 1
            && m.Profile.HasNormalAttackCorrection && !SkyTempleNpcCombat.RequiresEquipmentResponse(m.Profile)
            && (m.Profile.Source.Integers["dual"].ParsedValue is not (null or 0)) == dual
            && m.Profile.Attacks[0].Type.StartsWith("magical", StringComparison.OrdinalIgnoreCase) == elemental);
        using var h = BossAiHarness.For(member.MapId).WithAi(typeof(DamageOwner)).Build();
        var npc = RetailNpcNormalRuntimeTests.FreshNpc(h, member.MapId, member.Profile.NpcId, "retail_normal_ai_owner_fixture");
        Assert.NotNull(npc.GetGameStats().StatisticsProfile);
        var ai = Assert.IsType<DamageOwner>(npc.GetAi());
        var target = RetailNpcNormalRuntimeTests.MortalPlayer(h, member.MapId);
        var protection = new Protection(); target.GetObserveController().AddAttackCalcObserver(protection);
        var results = elemental
            ? AttackUtil.CalculateMagAttackResult(npc, target, SkyTempleCombatInputs.Element(member.Profile.Attacks[0].Type), new HashSet<CalculationType>())
            : AttackUtil.CalculatePhysAttackResult(npc, target, new HashSet<CalculationType>());
        Assert.Equal(dual ? 2 : 1, results.Count);
        Assert.Equal(results.Count, ai.Calls);
        Assert.Equal(results.Count, protection.Calls);
        Assert.All(protection.Received, value => Assert.Equal(1, value));
        Assert.All(results, result => Assert.Equal(0, result.GetDamage()));
        Assert.Empty(h.Clock.Faults);
    }
    [SkippableFact]
    public void RetailOrderedAttackModifiersDoNotReapplyLegacyNumericAiBoosts()
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        var member = RetailNpcStatCatalog.Members.First(m => m.MapId != 0 && m.Profile.NpcId == 231073);
        Assert.Contains(member.Profile.Attacks, a => a.Modifier == 450);
        using var h = BossAiHarness.For(member.MapId).WithAi(typeof(SupersededNumericOwner)).Build();
        var npc = RetailNpcNormalRuntimeTests.FreshNpc(h, member.MapId, member.Profile.NpcId, "retail_normal_superseded_numeric_fixture");
        var ai = Assert.IsType<SupersededNumericOwner>(npc.GetAi());
        var target = RetailNpcNormalRuntimeTests.MortalPlayer(h, member.MapId);
        var results = AttackUtil.CalculatePhysAttackResult(npc, target, new HashSet<CalculationType>());
        Assert.Equal(0, ai.Calls);
        Assert.Single(results);
        Assert.True(results[0].GetDamage() > 0);
        Assert.Empty(h.Clock.Faults);
    }

}
