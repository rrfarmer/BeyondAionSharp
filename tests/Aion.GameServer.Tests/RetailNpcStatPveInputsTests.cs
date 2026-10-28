using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Model;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatPveInputsTests
{
    private static JsonElement Npc(string fields = "") => JsonDocument.Parse(
        "[\"npc\",[],[" + fields + "]]").RootElement.Clone();

    [Fact]
    public void PhysicalMagicalAndFactionChannelsStayDistinctAndWorldValuesClampBeforeUse()
    {
        var input = RetailNpcStatPveInputs.FromXmlForTests(
            "<world_id><data id='1' pve_attack_ratio_physical_light='150' pve_attack_ratio_physical_dark='300' pve_attack_ratio_magical_dark='12000' pve_defend_ratio_magical_dark='-12000'/></world_id>"u8.ToArray(),
            "<pve_adjusts/>"u8.ToArray(), 1, 2);
        Assert.Equal(150, input.PlayerBase(1, Race.ELYOS, true, true));
        Assert.Equal(300, input.PlayerBase(1, Race.ASMODIANS, true, true));
        Assert.Equal(10000, input.PlayerBase(1, Race.ASMODIANS, false, true));
        Assert.Equal(-10000, input.PlayerBase(1, Race.ASMODIANS, false, false));
        Assert.Equal(0, input.PlayerBase(1, Race.ELYOS, false, true));
        Assert.Equal(0, input.PlayerBase(2, Race.ASMODIANS, true, true));
        Assert.False(input.HasMapSource(2));
        Assert.Throws<InvalidDataException>(() => input.PlayerBase(3, Race.ASMODIANS, true, true));
        Assert.Throws<InvalidDataException>(() => input.PlayerBase(1, Race.BEAST, true, true));
    }

    [Fact]
    public void DisabledMapUsesNativeZeroGlobalsRatherThanItsTableValues()
    {
        var input = RetailNpcStatPveInputs.FromXmlForTests(
            "<world_id><data id='1' apply_pve_adjust='FALSE' pve_attack_ratio_physical_dark='150'/></world_id>"u8.ToArray(),
            "<pve_adjusts/>"u8.ToArray(), 1);
        Assert.True(input.HasMapSource(1));
        Assert.Equal(0, input.PlayerBase(1, Race.ASMODIANS, true, true));
    }

    [Fact]
    public void CounterpartyAdjustmentKeepsNativeDirectionAndSubtractsTheOwnedTemplateField()
    {
        var input = RetailNpcStatPveInputs.FromXmlForTests(
            "<world_id><data id='1' pve_attack_ratio_physical_dark='150' pve_defend_ratio_physical_dark='50'/></world_id>"u8.ToArray(),
            "<pve_adjusts><name name='mob'><pve_attack_ratio_physical_dark>500</pve_attack_ratio_physical_dark><pve_defend_ratio_physical_dark>-100</pve_defend_ratio_physical_dark><pve_defend_ratio_magical_light>75</pve_defend_ratio_magical_light></name></pve_adjusts>"u8.ToArray(), 1);
        var npc = Npc("[\"npc_pve_attack_ratio_physical\",[],[\"20\"]],[\"npc_pve_defend_ratio_physical\",[],[\"30\"]]");
        // NPC attack: user defense gets DEFEND row - NPC ATTACK template, after user effects.
        Assert.Equal(-120, input.NpcCounterpartyDelta("mob", npc, Race.ASMODIANS, true, true));
        // Player attack: user attack gets ATTACK row - NPC DEFEND template, after user effects.
        Assert.Equal(470, input.NpcCounterpartyDelta("mob", npc, Race.ASMODIANS, true, false));
        Assert.Equal(75, input.NpcCounterpartyDelta("mob", npc, Race.ELYOS, false, true));
        Assert.Equal(150, input.PlayerBase(1, Race.ASMODIANS, true, true)); // Delta is not folded into this base.
        Assert.Equal(50, input.PlayerBase(1, Race.ASMODIANS, true, false));
        Assert.Equal(-20, input.NpcCounterpartyDelta("unlisted", npc, Race.ASMODIANS, true, true));
    }

    [Fact]
    public void NpcTableWritesAreOrderedSigned32AndDoNotUseTheWorldChannelClamp()
    {
        var input = RetailNpcStatPveInputs.FromXmlForTests("<world_id><data id='1'/></world_id>"u8.ToArray(),
            "<pve_adjusts><name name='mob'><pve_attack_ratio_physical_dark>1</pve_attack_ratio_physical_dark><pve_attack_ratio_physical_dark>20000</pve_attack_ratio_physical_dark></name></pve_adjusts>"u8.ToArray(), 1);
        Assert.Equal(20000, input.NpcCounterpartyDelta("mob", Npc(), Race.ASMODIANS, true, false));
        Assert.Equal(0, RetailNpcStatPveInputs.TemplateRatio(Npc(), "npc_pve_attack_ratio_physical"));
        var repeated = Npc("[\"npc_pve_attack_ratio_physical\",[],[\"1\"]],[\"npc_pve_attack_ratio_physical\",[],[\"2\"]]");
        Assert.Throws<InvalidDataException>(() => RetailNpcStatPveInputs.TemplateRatio(repeated, "npc_pve_attack_ratio_physical"));
    }

    [Fact]
    public void MissingDigestChangedBytesAndDuplicateSourceIdentitiesAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => RetailNpcStatPveInputs.Parse("{}"u8, ""));
        string original = Convert.ToHexStringLower(SHA256.HashData("{}"u8));
        Assert.Throws<InvalidDataException>(() => RetailNpcStatPveInputs.Parse("{ }"u8, original));
        Assert.Throws<InvalidDataException>(() => RetailNpcStatPveInputs.FromXmlForTests(
            "<world_id><data id='1'/><data id='1'/></world_id>"u8.ToArray(), "<pve_adjusts/>"u8.ToArray(), 1));
        Assert.Throws<InvalidDataException>(() => RetailNpcStatPveInputs.FromXmlForTests(
            "<world_id><data id='1'/></world_id>"u8.ToArray(), "<pve_adjusts><name name='mob'/><name name='mob'/></pve_adjusts>"u8.ToArray(), 1));
    }

    [SkippableFact]
    public void PinnedSelectedSourcesGiveIshalgenAndAltgardTheirActualCombatBases()
    {
        string? path = Environment.GetEnvironmentVariable("AION_RETAIL_STAT_PVE_TEST_SOURCE");
        string? hash = Environment.GetEnvironmentVariable("AION_RETAIL_STAT_PVE_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(hash), "Requires selected external combat-only source and separate SHA256");
        var input = RetailNpcStatPveInputs.Parse(File.ReadAllBytes(path!), hash!);
        Assert.Equal(118, input.SupportedMapCount);
        foreach (int map in new[] { 220010000, 220030000 })
        foreach (Race race in new[] { Race.ELYOS, Race.ASMODIANS })
        foreach (bool physical in new[] { true, false })
        {
            Assert.Equal(150, input.PlayerBase(map, race, physical, true));
            Assert.Equal(50, input.PlayerBase(map, race, physical, false));
        }
        Assert.Equal(0, input.PlayerBase(320050000, Race.ASMODIANS, true, true));
        Assert.Equal(0, input.PlayerBase(320050000, Race.ASMODIANS, true, false));
        Assert.False(input.HasMapSource(301240000));
        Assert.Equal(0, input.PlayerBase(301240000, Race.ASMODIANS, true, true));
        Assert.True(input.HasNpcOverride("Ab1_1131_Boss_Li_1"));
        Assert.Equal(500, input.NpcCounterpartyDelta("Ab1_1131_Boss_Li_1", Npc(), Race.ASMODIANS, true, false));
        Assert.Equal(0, input.NpcCounterpartyDelta("Ab1_1131_Boss_Li_1", Npc(), Race.ASMODIANS, true, true));
    }
}
