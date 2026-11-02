using System.Text.Json;
using System.Reflection;
using Aion.Bots.Protocol;
using Aion.Commons.Nio;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.TestKit;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class PlayerHeightPortTests
{
    [Theory]
    [InlineData(0x3f4ccccd)]
    [InlineData(0x3f800001)]
    [InlineData(0x3f99999a)] // 1.2, the upper bound of the incoming slider after a519ffc7b
    public void CharacterEditRetainsTheRawHeightScaleFloat(int bits)
    {
        var body = GameClientPackets.EditCharacter(49151, new CharacterCreationData
        {
            CharacterName = "Heighttest", Gender = 1, Race = 1, PlayerClass = 0,
            Height = BitConverter.Int32BitsToSingle(bits)
        }).Body.ToArray();
        var packet = new CM_CHARACTER_EDIT(0, new HashSet<AionConnection.State> { AionConnection.State.IN_GAME });
        packet.SetConnection(new PvpExpPortTests.Connection());
        packet.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN));
        Assert.True(packet.Read()); Assert.Equal(0, packet.GetRemainingBytes());
        var appearance = (PlayerAppearance)typeof(AbstractCharacterEditPacket)
            .GetField("playerAppearance", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(packet)!;
        Assert.Equal(bits, BitConverter.SingleToInt32Bits(appearance.GetHeightScale()));
    }

    [Fact]
    public void RealDrowningTaskStartsAndStopsAtCompiledJavaFloatBoundaries()
    {
        using var fixture = Fixture();
        using var h = BossAiHarness.For().Build();
        var map = h.World.GetWorldMap(BossAiHarness.DefaultMapId).GetTemplate();
        map.WaterLevel = 100;
        var player = h.SpawnPlayer(z: 100);
        var cases = fixture.RootElement.GetProperty("cases");
        Assert.Equal(30, cases.GetArrayLength());
        foreach (var c in cases.EnumerateArray())
        {
            player.GetCommonData().SetGender(Enum.Parse<Gender>(c.GetProperty("gender").GetString()!));
            player.GetPlayerAppearance().SetHeightScale(BitConverter.Int32BitsToSingle(c.GetProperty("scaleBits").GetInt32()));
            player.GetPosition().SetZ(BitConverter.Int32BitsToSingle(c.GetProperty("zBits").GetInt32()));
            ZoneLevelService.CheckZoneLevels(player);
            Assert.Equal(c.GetProperty("drown").GetBoolean(), player.GetController().HasTask(TaskId.DROWN));
            // Above water must cancel the real scheduled task before the next vector.
            player.GetPosition().SetZ(110);
            ZoneLevelService.CheckZoneLevels(player);
            Assert.False(player.GetController().HasTask(TaskId.DROWN));
        }
    }

    [Theory]
    [InlineData(Gender.MALE)]
    [InlineData(Gender.FEMALE)]
    public void BoundsRemainFixedBeforeAccountCreationAndAcrossAppearanceReplacement(Gender gender)
    {
        using var fixture = Fixture();
        var expected = fixture.RootElement.GetProperty("boundRadiusBits").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var common = new PlayerCommonData(49151); common.SetGender(gender);
        Assert.Same(BoundRadius.PLAYER, common.GetBoundRadius());
        foreach (float scale in new[] {0f, 0.5f, 1f, 1.5f, 2f})
        {
            var appearance = new PlayerAppearance(); appearance.SetHeightScale(scale);
            var account = new PlayerAccountData(common, appearance);
            var replacement = new PlayerAppearance(); replacement.SetHeightScale(scale + 0.25f);
            account.SetAppearance(replacement);
            Assert.Equal(scale + 0.25f, account.GetAppearance().GetHeightScale());
            var bounds = common.GetBoundRadius();
            Assert.Same(BoundRadius.PLAYER, bounds);
            Assert.Equal(expected, new[] {BitConverter.SingleToInt32Bits(bounds.GetFront()),
                BitConverter.SingleToInt32Bits(bounds.GetSide()), BitConverter.SingleToInt32Bits(bounds.GetUpper())});
        }
    }

    private static JsonDocument Fixture()
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(),
            "parity-artifacts/golden/formulas/PlayerHeight.183723078.json")));
        Assert.Equal("183723078bd11225af6e13fbf7376a0ed3a825ab", document.RootElement.GetProperty("sourceJavaCommit").GetString());
        return document;
    }
}
